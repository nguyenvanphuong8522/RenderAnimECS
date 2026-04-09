using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class BatchData
{
    public ComputeBuffer matrixBuffer;
    public ComputeBuffer uvBuffer;
    public Texture2D texture;
    public int maxInstances;
}


[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetIndirectRenderSystem : SystemBase
{
    private ComputeBuffer argsBuffer;
    private NativeArray<uint> args;
    private Material baseMaterial;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;
    private GameHandler gameHandler;

    private List<BatchData> batches = new List<BatchData>();

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
        argsBuffer.Release();
        if (args.IsCreated) args.Dispose();

        foreach (var batch in batches)
        {
            batch.matrixBuffer?.Release();
            batch.uvBuffer?.Release();
        }
    }

    protected override void OnUpdate()
    {
        if (gameHandler == null)
        {
            gameHandler = GameHandler.GetInstance();
            baseMaterial = gameHandler.baseWalkingMaterial;
            InitBatches(gameHandler);
        }

        int batchCount = batches.Count;
        if (batchCount == 0) return;

        var matricesArray = new NativeArray<NativeList<float4x4>>(batchCount, Allocator.Temp);
        var uvsArray = new NativeArray<NativeList<float4>>(batchCount, Allocator.Temp);

        for (int i = 0; i < batchCount; i++)
        {
            matricesArray[i] = new NativeList<float4x4>(Allocator.Temp);
            uvsArray[i] = new NativeList<float4>(Allocator.Temp);
        }



        Entities.WithAll<VisibleTag>()
        .ForEach((in LocalTransform transform, in SpriteSheetAnimationData sprite) =>
        {
            int index = sprite.textureIndex;
            if (index >= 0 && index < batchCount)
            {
                // Tính toán lại Scale dựa trên kích thước thực của Sprite frame
                // transform.Scale là scale chung (ví dụ to lên 1.5 lần)
                // sprite.currentSize là tỉ lệ gốc của frame đó
                float3 actualScale = new float3(sprite.currentSize.x * transform.Scale, sprite.currentSize.y * transform.Scale, transform.Scale);
                matricesArray[index].Add(float4x4.TRS(transform.Position, transform.Rotation, actualScale));
                uvsArray[index].Add(sprite.currentUV);
            }
        }).Run();

        for (int i = 0; i < batchCount; i++)
        {
            int count = matricesArray[i].Length;
            if (count > 0)
            {
                var batch = batches[i];

                if (count > batch.maxInstances)
                {
                    batch.matrixBuffer?.Release();
                    batch.uvBuffer?.Release();

                    batch.maxInstances = count + 500;
                    batch.matrixBuffer = new ComputeBuffer(batch.maxInstances, 64);
                    batch.uvBuffer = new ComputeBuffer(batch.maxInstances, 16);
                }

                batch.matrixBuffer.SetData(matricesArray[i].AsArray(), 0, 0, count);
                batch.uvBuffer.SetData(uvsArray[i].AsArray(), 0, 0, count);

                // QUAN TRỌNG: Đẩy TẤT CẢ mọi thứ vào MaterialPropertyBlock (mpb)
                mpb.SetBuffer("_Matrices", batch.matrixBuffer);
                mpb.SetBuffer("_UVData", batch.uvBuffer);
                mpb.SetTexture("_MainTex", batch.texture); // Ghi đè Texture cho batch này

                // Truyền baseMaterial và mpb vào hàm vẽ
                DrawMesh(mesh, baseMaterial, argsBuffer, mpb, count, args);
            }

            matricesArray[i].Dispose();
            uvsArray[i].Dispose();
        }

        matricesArray.Dispose();
        uvsArray.Dispose();
    }

    private void InitBatches(GameHandler handler)
    {
        for (int i = 0; i < handler.enemyConfigs.enemyAnimConfigs.Count; i++)
        {
            batches.Add(new BatchData
            {
                texture = handler.enemyConfigs.enemyAnimConfigs[i].texture,
                maxInstances = 0
            });
        }
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