using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct AtlasSharedTag : ISharedComponentData
{
    public int atlasIndex;
}

// Lớp lưu trữ Buffer riêng cho TỪNG ATLAS
public class AtlasBatchData
{
    public ComputeBuffer matrixBuffer;
    public ComputeBuffer uvBuffer;
    public int maxInstances = 0;
}

[BurstCompile]
[WithAll(typeof(VisibleTag))]
public partial struct GatherAtlasDataJob : IJobEntity
{
    public NativeArray<float4x4> matrices;
    public NativeArray<float4> uvs;

    // EntityIndexInQuery là chìa khóa: Nó sẽ đánh số thứ tự từ 0 cho riêng Query của từng Atlas
    public void Execute([EntityIndexInQuery] int entityInQueryIndex, in LocalTransform transform, in SpriteSheetAnimationData sprite)
    {
        float3 actualScale = new float3(sprite.currentSize.x * transform.Scale, sprite.currentSize.y * transform.Scale, 1f);
        matrices[entityInQueryIndex] = float4x4.TRS(transform.Position, transform.Rotation, actualScale);
        uvs[entityInQueryIndex] = sprite.currentUV;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetIndirectRenderSystem : SystemBase
{
    private ComputeBuffer argsBuffer;
    private NativeArray<uint> args;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;
    private AtlasBatchData[] atlasBatches;
    private EntityQuery atlasQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        args = new NativeArray<uint>(5, Allocator.Persistent);
        mpb = new MaterialPropertyBlock();
        mesh = MeshUtils.CreateQuad();

        // Định nghĩa Query chứa Shared Component
        atlasQuery = GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<SpriteSheetAnimationData>(),
            ComponentType.ReadOnly<VisibleTag>(),
            ComponentType.ReadOnly<AtlasSharedTag>()
        );
    }

    protected override void OnUpdate()
    {
        var gameHandler = GameHandler.GetInstance();
        if (gameHandler == null || gameHandler.atlasTextures == null) return;

        int atlasCount = gameHandler.atlasTextures.Length;
        if (atlasBatches == null)
        {
            atlasBatches = new AtlasBatchData[atlasCount];
            for (int i = 0; i < atlasCount; i++) atlasBatches[i] = new AtlasBatchData();
        }

        // Đảm bảo Query sạch sẽ mỗi frame
        atlasQuery.ResetFilter();

        // Duyệt qua từng Atlas
        for (int i = 0; i < atlasCount; i++)
        {
            // BÍ QUYẾT: Chỉ lọc ra những Entity thuộc về Atlas hiện tại
            atlasQuery.SetSharedComponentFilter(new AtlasSharedTag { atlasIndex = i });
            int count = atlasQuery.CalculateEntityCount();

            if (count > 0)
            {
                var batch = atlasBatches[i];

                if (count > batch.maxInstances)
                {
                    batch.matrixBuffer?.Release();
                    batch.uvBuffer?.Release();
                    batch.maxInstances = count + 1000;
                    batch.matrixBuffer = new ComputeBuffer(batch.maxInstances, 64);
                    batch.uvBuffer = new ComputeBuffer(batch.maxInstances, 16);
                }

                // Cấp phát mảng tạm thời
                var matrices = new NativeArray<float4x4>(count, Allocator.TempJob);
                var uvs = new NativeArray<float4>(count, Allocator.TempJob);

                // Giao việc cho CPU chạy ĐA LUỒNG
                var job = new GatherAtlasDataJob
                {
                    matrices = matrices,
                    uvs = uvs
                };

                // SCHEDULE PARALLEL Ở ĐÂY
                this.Dependency = job.ScheduleParallel(atlasQuery, this.Dependency);

                // Bắt buộc phải chờ job xong mới đẩy lên GPU được
                this.Dependency.Complete();

                // Gán dữ liệu cho GPU
                batch.matrixBuffer.SetData(matrices);
                batch.uvBuffer.SetData(uvs);

                mpb.SetBuffer("_Matrices", batch.matrixBuffer);
                mpb.SetBuffer("_UVData", batch.uvBuffer);
                mpb.SetTexture("_MainTex", gameHandler.atlasTextures[i]);

                DrawMesh(count, gameHandler.baseWalkingMaterial);

                // Dọn rác
                matrices.Dispose();
                uvs.Dispose();
            }
        }
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