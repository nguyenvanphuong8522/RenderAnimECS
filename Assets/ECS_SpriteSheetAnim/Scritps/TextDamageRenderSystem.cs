using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Chứa thông tin tổng quát của cụm số
public struct TextDamageData : IComponentData
{
    public float digitSpacing; // Khoảng cách giữa các chữ số (ví dụ: 0.5f)
    public int atlasIndex;     // Nếu bạn có nhiều font chữ khác nhau

    public float timer;        // Bộ đếm thời gian
    public float lifetime;
    public float4x4 matrix;
    public float4 uv;
}

// Chứa mảng các chữ số. [InternalBufferCapacity] giúp tối ưu RAM cho các số có độ dài dưới 4 chữ số (ví dụ 9999).
[InternalBufferCapacity(4)]
public struct DamageDigitElement : IBufferElementData
{
    public int digitValue; // Lưu số 0, 1, 2... 9
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class TextDamageRenderSystem : SystemBase
{
    private ComputeBuffer matrixBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer argsBuffer;
    private NativeArray<uint> args;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;
    private int maxInstances = 0;

    protected override void OnCreate()
    {
        RequireForUpdate<TextDamageData>();
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        args = new NativeArray<uint>(5, Allocator.Persistent);
        mpb = new MaterialPropertyBlock();
        mesh = MeshUtils.CreateQuad();
    }

    protected override void OnUpdate()
    {
        var gameHandler = GameHandler.GetInstance();
        if (gameHandler == null || gameHandler.fontAtlasTexture == null) return;

        var fontUVBlob = gameHandler.numberUVBlob;

        // 2. SỬ DỤNG NATIVE QUEUE THAY VÌ NATIVE LIST
        // NativeQueue cực kỳ an toàn khi nhiều luồng CPU cùng ghi dữ liệu vào
        var instanceQueue = new NativeQueue<TextDamageData>(Allocator.TempJob);

        // Tạo "người ghi" (writer) đa luồng để truyền vào ForEach
        var queueWriter = instanceQueue.AsParallelWriter();

        Entities.ForEach((in LocalTransform transform, in TextDamageData textData, in DynamicBuffer<DamageDigitElement> digits) =>
        {
            int digitCount = digits.Length;
            if (digitCount == 0) return;

            float totalWidth = (digitCount - 1) * textData.digitSpacing;
            float startX = transform.Position.x - (totalWidth / 2f);

            for (int i = 0; i < digitCount; i++)
            {
                float3 digitPos = transform.Position;
                digitPos.x = startX + (i * textData.digitSpacing);

                // Tính toán Matrix và UV
                float4x4 mat = float4x4.TRS(digitPos, transform.Rotation, transform.Scale);
                int numValue = digits[i].digitValue;
                float4 uv = fontUVBlob.Value.uvs[numValue];

                // 3. ĐẨY VÀO QUEUE AN TOÀN TRÊN ĐA LUỒNG
                queueWriter.Enqueue(new TextDamageData { matrix = mat, uv = uv });
            }
        }).ScheduleParallel(); // THAY ĐỔI THÀNH SCHEDULE PARALLEL Ở ĐÂY

        // Bắt buộc phải đợi tất cả các luồng CPU làm xong việc trước khi đẩy lên GPU
        this.Dependency.Complete();

        int totalInstances = instanceQueue.Count;
        if (totalInstances > 0)
        {
            if (totalInstances > maxInstances)
            {
                matrixBuffer?.Release();
                uvBuffer?.Release();
                maxInstances = totalInstances + 500;
                matrixBuffer = new ComputeBuffer(maxInstances, 64);
                uvBuffer = new ComputeBuffer(maxInstances, 16);
            }

            // 4. TÁCH DỮ LIỆU TỪ QUEUE SANG MẢNG ĐỂ GPU ĐỌC ĐƯỢC
            var matricesArray = new NativeArray<float4x4>(totalInstances, Allocator.Temp);
            var uvsArray = new NativeArray<float4>(totalInstances, Allocator.Temp);

            for (int i = 0; i < totalInstances; i++)
            {
                // Rút từng phần tử ra khỏi Queue
                var data = instanceQueue.Dequeue();
                matricesArray[i] = data.matrix;
                uvsArray[i] = data.uv;
            }

            matrixBuffer.SetData(matricesArray, 0, 0, totalInstances);
            uvBuffer.SetData(uvsArray, 0, 0, totalInstances);

            mpb.SetBuffer("_Matrices", matrixBuffer);
            mpb.SetBuffer("_UVData", uvBuffer);
            mpb.SetTexture("_MainTex", gameHandler.fontAtlasTexture);

            DrawMesh(totalInstances, gameHandler.baseWalkingMaterial);

            matricesArray.Dispose();
            uvsArray.Dispose();
        }

        instanceQueue.Dispose();
    }

    private void DrawMesh(int count, Material material)
    {
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, Vector3.one * 10000), argsBuffer, 0, mpb);
    }

    protected override void OnDestroy()
    {
        argsBuffer?.Release();
        matrixBuffer?.Release();
        uvBuffer?.Release();
        if (args.IsCreated) args.Dispose();
    }
}