using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetIndirectRenderSystem : SystemBase
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
        RequireForUpdate<SpriteSheetAnimationData>();
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        args = new NativeArray<uint>(5, Allocator.Persistent);
        mpb = new MaterialPropertyBlock();
        mesh = MeshUtils.CreateQuad();
    }

    protected override void OnDestroy()
    {
        argsBuffer?.Release();
        matrixBuffer?.Release();
        uvBuffer?.Release();
        if (args.IsCreated) args.Dispose();
    }

    protected override void OnUpdate()
    {
        var gameHandler = GameHandler.GetInstance();
        if (gameHandler == null || gameHandler.mainAtlasTexture == null) return;

        // 1. Tạo Query để lấy toàn bộ Entity có VisibleTag
        EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<SpriteSheetAnimationData>(), ComponentType.ReadOnly<VisibleTag>());
        int count = query.CalculateEntityCount();
        if (count == 0) return;

        // 2. Kiểm tra và mở rộng Buffer nếu số lượng Entity tăng lên
        UpdateBufferCapacity(count);

        // 3. Thu thập dữ liệu dùng NativeArray (Sử dụng TempJob để xử lý trong Job)
        var matrices = new NativeArray<float4x4>(count, Allocator.TempJob);
        var uvs = new NativeArray<float4>(count, Allocator.TempJob);

        // Sử dụng ScheduleParallel để copy dữ liệu cực nhanh từ ECS sang mảng render
        Entities.WithAll<VisibleTag>().ForEach((int entityInQueryIndex, in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            float3 actualScale = new float3(sprite.currentSize.x * transform.Scale, sprite.currentSize.y * transform.Scale, 1f);
            matrices[entityInQueryIndex] = float4x4.TRS(transform.Position, transform.Rotation, actualScale);
            uvs[entityInQueryIndex] = sprite.currentUV;
        }).ScheduleParallel();

        // Đợi Job hoàn thành để nạp dữ liệu lên GPU
        this.Dependency.Complete();

        // 4. Đẩy dữ liệu lên GPU (1 lần duy nhất)
        matrixBuffer.SetData(matrices);
        uvBuffer.SetData(uvs);

        // 5. Cài đặt MaterialPropertyBlock với Atlas duy nhất
        mpb.SetBuffer("_Matrices", matrixBuffer);
        mpb.SetBuffer("_UVData", uvBuffer);
        // Thay vì lấy biến lưu sẵn, hãy lấy trực tiếp từ config để đảm bảo đúng trang Atlas
        if (gameHandler.enemyConfigs.enemyAnimConfigs.Count > 0)
        {
            var tex = gameHandler.enemyConfigs.enemyAnimConfigs[0].sprites[0].texture;
            mpb.SetTexture("_MainTex", tex);
        }
        // 6. Lệnh vẽ DUY NHẤT
        DrawMesh(count, gameHandler.baseWalkingMaterial);

        matrices.Dispose();
        uvs.Dispose();
    }

    private void UpdateBufferCapacity(int count)
    {
        if (count > maxInstances)
        {
            matrixBuffer?.Release();
            uvBuffer?.Release();
            maxInstances = count + 1000; // Dự phòng để không tạo lại liên tục
            matrixBuffer = new ComputeBuffer(maxInstances, 64);
            uvBuffer = new ComputeBuffer(maxInstances, 16);
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
}