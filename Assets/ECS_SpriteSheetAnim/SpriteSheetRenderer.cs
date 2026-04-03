using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct SpriteSheetRenderSystem : ISystem
{


    public void OnUpdate(ref SystemState state)
    {
        Camera _camera = Camera.main;

        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        Vector4[] uv = new Vector4[1];

        Mesh quadMesh = GameHandler.GetInstance().quadMesh;
        Material material = GameHandler.GetInstance().walkingSpriteSheetMaterial;



        foreach (var (transform, spriteData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<SpriteSheetAnimationData>>())
        {
            uv[0] = spriteData.ValueRO.uv;

            materialPropertyBlock.SetVectorArray("_MainTex_UV", uv);

            Graphics.DrawMesh(quadMesh, spriteData.ValueRO.matrix, material, 0, _camera, 0, materialPropertyBlock);
        }


    }
}