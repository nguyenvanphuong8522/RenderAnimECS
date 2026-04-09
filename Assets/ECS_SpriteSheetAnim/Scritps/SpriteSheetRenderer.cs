using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

// Lớp lưu trữ Buffer riêng cho TỪNG ATLAS
public class AtlasBatchData
{
    public ComputeBuffer matrixBuffer;
    public ComputeBuffer uvBuffer;
    public int maxInstances = 0;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetIndirectRenderSystem : SystemBase
{
    private ComputeBuffer argsBuffer;
    private NativeArray<uint> args;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;

    private AtlasBatchData[] atlasBatches;
    private bool isInitialized = false;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        args = new NativeArray<uint>(5, Allocator.Persistent);
        mpb = new MaterialPropertyBlock();
        mesh = MeshUtils.CreateQuad();
    }

    private void InitializeBatches(int atlasCount)
    {
        atlasBatches = new AtlasBatchData[atlasCount];
        for (int i = 0; i < atlasCount; i++)
        {
            atlasBatches[i] = new AtlasBatchData();
        }
        isInitialized = true;
    }

    protected override void OnUpdate()
    {
        var gameHandler = GameHandler.GetInstance();
        if (gameHandler == null || gameHandler.atlasTextures == null) return;

        int atlasCount = gameHandler.atlasTextures.Length;
        if (atlasCount == 0) return;

        if (!isInitialized) InitializeBatches(atlasCount);

        // Tạo mảng các List để chứa data phân loại theo Atlas
        var matricesArray = new NativeArray<NativeList<float4x4>>(atlasCount, Allocator.TempJob);
        var uvsArray = new NativeArray<NativeList<float4>>(atlasCount, Allocator.TempJob);

        for (int i = 0; i < atlasCount; i++)
        {
            matricesArray[i] = new NativeList<float4x4>(Allocator.TempJob);
            uvsArray[i] = new NativeList<float4>(Allocator.TempJob);
        }

        // Chạy qua tất cả Entity và đẩy nó vào đúng giỏ (List) của Atlas đó
        // Dùng .Run() thay vì ScheduleParallel vì chúng ta đang add vào mảng NativeList động
        Entities.WithAll<VisibleTag>().ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            int aIndex = sprite.atlasIndex;
            if (aIndex >= 0 && aIndex < atlasCount)
            {
                float3 actualScale = new float3(sprite.currentSize.x * transform.Scale, sprite.currentSize.y * transform.Scale, 1f);
                matricesArray[aIndex].Add(float4x4.TRS(transform.Position, transform.Rotation, actualScale));
                uvsArray[aIndex].Add(sprite.currentUV);
            }
        }).Run();

        // Duyệt qua từng giỏ Atlas và nạp lên GPU vẽ
        for (int i = 0; i < atlasCount; i++)
        {
            int count = matricesArray[i].Length;
            if (count > 0)
            {
                var batch = atlasBatches[i];

                // Mở rộng Buffer nếu thiếu chỗ
                if (count > batch.maxInstances)
                {
                    batch.matrixBuffer?.Release();
                    batch.uvBuffer?.Release();
                    batch.maxInstances = count + 1000;
                    batch.matrixBuffer = new ComputeBuffer(batch.maxInstances, 64);
                    batch.uvBuffer = new ComputeBuffer(batch.maxInstances, 16);
                }

                // Gán dữ liệu cho Buffer của Atlas này
                batch.matrixBuffer.SetData(matricesArray[i].AsArray(), 0, 0, count);
                batch.uvBuffer.SetData(uvsArray[i].AsArray(), 0, 0, count);

                // Cấu hình Material với Texture của Atlas tương ứng
                mpb.SetBuffer("_Matrices", batch.matrixBuffer);
                mpb.SetBuffer("_UVData", batch.uvBuffer);
                mpb.SetTexture("_MainTex", gameHandler.atlasTextures[i]);

                // Vẽ
                DrawMesh(count, gameHandler.baseWalkingMaterial);
            }

            // Giải phóng bộ nhớ tạm
            matricesArray[i].Dispose();
            uvsArray[i].Dispose();
        }

        matricesArray.Dispose();
        uvsArray.Dispose();
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
        if (args.IsCreated) args.Dispose();

        if (atlasBatches != null)
        {
            foreach (var batch in atlasBatches)
            {
                batch.matrixBuffer?.Release();
                batch.uvBuffer?.Release();
            }
        }
    }
}