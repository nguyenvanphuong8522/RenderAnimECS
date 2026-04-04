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

    Matrix4x4[] matrixArray;
    Vector4[] uvArray;

    Material material;
    Mesh mesh;

    const int MAX = 1_000_000;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();

        matrixArray = new Matrix4x4[MAX];
        uvArray = new Vector4[MAX];

        matrixBuffer =
            new ComputeBuffer(MAX, 64);

        uvBuffer =
            new ComputeBuffer(MAX, 16);

        argsBuffer =
            new ComputeBuffer(
                1,
                5 * sizeof(uint),
                ComputeBufferType.IndirectArguments);
    }

    protected override void OnUpdate()
    {
        var handler = GameHandler.GetInstance();

        if (handler == null) return;

        mesh = handler.quadMesh;
        material = handler.walkingSpriteSheetMaterial;

        int index = 0;

        Entities
        .ForEach((
            in LocalTransform transform,
            in SpriteSheetAnimationData sprite) =>
        {
            float3 pos = transform.Position;

            matrixArray[index] =
                Matrix4x4.TRS(
                    pos,
                    Quaternion.identity,
                    Vector3.one);

            uvArray[index] = sprite.uv;

            index++;

        }).WithoutBurst().Run();

        if (index == 0) return;

        matrixBuffer.SetData(
            matrixArray, 0, 0, index);

        uvBuffer.SetData(
            uvArray, 0, 0, index);

        material.SetBuffer(
            "_Matrices",
            matrixBuffer);

        material.SetBuffer(
            "_UVData",
            uvBuffer);

        uint[] args = new uint[5]
        {
            mesh.GetIndexCount(0),
            (uint)index,
            mesh.GetIndexStart(0),
            mesh.GetBaseVertex(0),
            0
        };

        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            material,
            new Bounds(
                Vector3.zero,
                Vector3.one * 10000),
            argsBuffer);
    }

    protected override void OnDestroy()
    {
        matrixBuffer.Release();
        uvBuffer.Release();
        argsBuffer.Release();
    }
}