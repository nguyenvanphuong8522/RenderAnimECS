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


    static NativeArray<Matrix4x4> matrixArray;
    static NativeArray<Vector4> uvArray;
    private NativeArray<uint> args;


    private Material material;
    private Mesh mesh;

    const int MAX = 1_000_000;

    private Camera cam;
    public float height;
    public float width;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();

        matrixBuffer = new ComputeBuffer(MAX, 64);
        uvBuffer = new ComputeBuffer(MAX, 16);
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);



        matrixArray = new NativeArray<Matrix4x4>(MAX, Allocator.Persistent);
        uvArray = new NativeArray<Vector4>(MAX, Allocator.Persistent);
        args = new NativeArray<uint>(5, Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        matrixBuffer.Release();
        uvBuffer.Release();
        argsBuffer.Release();

        if (matrixArray.IsCreated)
            matrixArray.Dispose();

        if (uvArray.IsCreated)
            uvArray.Dispose();

        if (args.IsCreated)
            args.Dispose();
    }

    protected override void OnUpdate()
    {
        GameHandler gameHandler = GameHandler.GetInstance();

        if (gameHandler == null) return;

        if (cam == null)
        {
            cam = Camera.main;

            height = cam.orthographicSize;
            width = height * cam.aspect;
            mesh = gameHandler.quadMesh;
            material = gameHandler.walkingSpriteSheetMaterial;
        }





        Vector3 camPos = cam.transform.position;
        float minX = camPos.x - width;
        float maxX = camPos.x + width;

        float minY = camPos.y - height;
        float maxY = camPos.y + height;
        int index = 0;



        Entities
        .ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            float3 pos = transform.Position;

            if (pos.x < minX) return;
            if (pos.x > maxX) return;
            if (pos.y < minY) return;
            if (pos.y > maxY) return;

            matrixArray[index] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);

            uvArray[index] = sprite.uv;

            index++;

        }).WithoutBurst().Run();

        if (index == 0) return;

        matrixBuffer.SetData(matrixArray, 0, 0, index);

        uvBuffer.SetData(uvArray, 0, 0, index);

        material.SetBuffer("_Matrices", matrixBuffer);
        material.SetBuffer("_UVData", uvBuffer);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)index;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);


        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }
}