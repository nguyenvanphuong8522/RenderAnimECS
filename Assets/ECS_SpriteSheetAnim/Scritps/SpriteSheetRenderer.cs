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
    private MaterialPropertyBlock mpb;

    private GameHandler gameHandler;

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
        matrixBuffer.Release();
        uvBuffer.Release();
        argsBuffer.Release();

        if (args.IsCreated)
            args.Dispose();
    }

    protected override void OnUpdate()
    {
        if (gameHandler == null)
        {
            gameHandler = GameHandler.GetInstance();
            int amountEntity = gameHandler.AmountEntity;
            matrixBuffer = new ComputeBuffer(amountEntity, 64);
            uvBuffer = new ComputeBuffer(amountEntity, 16);
            material = gameHandler.walkingSpriteSheetMaterial;
        }

        var matrices = new NativeList<float4x4>(Allocator.Temp);
        var uvs = new NativeList<float4>(Allocator.Temp);

        Entities.WithAll<VisibleTag>()
        .ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            float3 pos = transform.Position;
            float3 scale = Vector3.one * transform.Scale;
            matrices.Add(float4x4.TRS(transform.Position, transform.Rotation, scale));
            uvs.Add(sprite.uv);
        }).Run();

        int count = matrices.Length;
        if (count > 0)
        {
            matrixBuffer.SetData(matrices.AsArray());
            uvBuffer.SetData(uvs.AsArray());

            material.SetBuffer("_Matrices", matrixBuffer);
            material.SetBuffer("_UVData", uvBuffer);

            mpb.SetTexture("_MainTex", gameHandler.SheetData.Texture);

            DrawMesh(mesh, material, argsBuffer, mpb, count, args);
        }
        matrices.Dispose();
        uvs.Dispose();
    }


    private static void DrawMesh(Mesh mesh, Material material, ComputeBuffer argsBuffer, MaterialPropertyBlock mpb, int count, NativeArray<uint> args)
    {
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer, 0, mpb);
    }
}