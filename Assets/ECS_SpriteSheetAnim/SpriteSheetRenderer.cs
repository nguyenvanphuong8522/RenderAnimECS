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

    private Material material;
    private Mesh mesh;

    const int MAX = 1_000_000;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();

        matrixBuffer = new ComputeBuffer(MAX, 64);
        uvBuffer = new ComputeBuffer(MAX, 16);
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        args = new NativeArray<uint>(5, Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        matrixBuffer.Release();
        uvBuffer.Release();
        argsBuffer.Release();

        if (args.IsCreated)
            args.Dispose();
    }

    protected override void OnUpdate()
    {
        GameHandler gameHandler = GameHandler.GetInstance();

        if (gameHandler == null) return;

        mesh = gameHandler.quadMesh;
        material = gameHandler.walkingSpriteSheetMaterial;

        var matrices = new NativeList<float4x4>(Allocator.Temp);
        var uvs = new NativeList<float4>(Allocator.Temp);
        Entities.WithAll<VisibleTag>()
        .ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            float3 pos = transform.Position;
            matrices.Add(float4x4.TRS(transform.Position, Quaternion.identity, Vector3.one));
            uvs.Add(sprite.uv);

        }).Run();
        int count = matrices.Length;

        if (count == 0) return;

        matrixBuffer.SetData(matrices.AsArray());
        uvBuffer.SetData(uvs.AsArray());

        material.SetBuffer("_Matrices", matrixBuffer);
        material.SetBuffer("_UVData", uvBuffer);

        matrices.Dispose();
        uvs.Dispose();


        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);

        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);


    }
}