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
        // Giả sử GameHandler của bạn có lưu Atlas chứa font chữ số và mảng UV của nó
        var gameHandler = GameHandler.GetInstance();
        if (gameHandler == null || gameHandler.fontAtlasTexture == null) return;

        // BÍ QUYẾT Ở ĐÂY: Vì 1 Entity đẻ ra N Matrix, ta không biết trước tổng số Matrix là bao nhiêu.
        // Nên ta phải dùng NativeList thay vì NativeArray.
        var matrices = new NativeList<float4x4>(Allocator.TempJob);
        var uvs = new NativeList<float4>(Allocator.TempJob);

        // Lấy mảng UV của các chữ số từ 0-9 (Lưu sẵn trong GameHandler)
        var fontUVBlob = gameHandler.numberUVBlob;
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

                matrices.Add(float4x4.TRS(digitPos, transform.Rotation, transform.Scale));

                int numValue = digits[i].digitValue;

                // SỬA Ở ĐÂY: Trỏ tới .Value trước, sau đó trỏ tới mảng .uvs, rồi mới lấy index [numValue]
                uvs.Add(fontUVBlob.Value.uvs[numValue]);
            }
        }).Run();

        int totalInstances = matrices.Length;
        if (totalInstances > 0)
        {
            // Mở rộng Buffer nếu cần
            if (totalInstances > maxInstances)
            {
                matrixBuffer?.Release();
                uvBuffer?.Release();
                maxInstances = totalInstances + 500;
                matrixBuffer = new ComputeBuffer(maxInstances, 64);
                uvBuffer = new ComputeBuffer(maxInstances, 16);
            }

            // Đẩy dữ liệu lên GPU
            matrixBuffer.SetData(matrices.AsArray(), 0, 0, totalInstances);
            uvBuffer.SetData(uvs.AsArray(), 0, 0, totalInstances);

            mpb.SetBuffer("_Matrices", matrixBuffer);
            mpb.SetBuffer("_UVData", uvBuffer);
            mpb.SetTexture("_MainTex", gameHandler.fontAtlasTexture); // Dùng Atlas chứa Font chữ

            // Lệnh vẽ 1 Draw Call cho TẤT CẢ các con số trên màn hình
            DrawMesh(totalInstances, gameHandler.baseWalkingMaterial);
        }

        matrices.Dispose();
        uvs.Dispose();
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