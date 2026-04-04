using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetIndirectRenderSystem : SystemBase
{
    ComputeBuffer matrixBuffer;
    ComputeBuffer uvBuffer;
    ComputeBuffer argsBuffer;



    Material material;
    Mesh mesh;

    Camera cam;
    const int MAX = 1_000_000;
    public float height;
    public float width;
    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();


        matrixBuffer = new ComputeBuffer(MAX, 64);

        uvBuffer = new ComputeBuffer(MAX, 16);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    protected override void OnUpdate()
    {
        var handler = GameHandler.GetInstance();

        if (handler == null) return;

        if (cam == null)
        {
            cam = Camera.main;

            height = cam.orthographicSize;

            width = height * cam.aspect;
        }

        mesh = handler.quadMesh;
        material = handler.walkingSpriteSheetMaterial;



        Vector3 camPos = cam.transform.position;

        float minX = camPos.x - width;
        float maxX = camPos.x + width;

        float minY = camPos.y - height;
        float maxY = camPos.y + height;

        int index = 0;
        Matrix4x4[] matrixArray;
        Vector4[] uvArray;

        matrixArray = new Matrix4x4[MAX];
        uvArray = new Vector4[MAX];
        Entities.ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
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

        uint[] args = new uint[5]
        {
            mesh.GetIndexCount(0),
            (uint)index,
            mesh.GetIndexStart(0),
            mesh.GetBaseVertex(0),
            0
        };

        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, Vector3.one * 10000), argsBuffer);
    }

    protected override void OnDestroy()
    {
        matrixBuffer.Release();
        uvBuffer.Release();
        argsBuffer.Release();
    }
}