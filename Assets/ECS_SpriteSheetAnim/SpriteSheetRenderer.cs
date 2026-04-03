using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct SpriteSheetRenderSystem : ISystem
{


    public void OnUpdate(ref SystemState state)
    {
        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        Vector4[] uv = new Vector4[1];

        Mesh quadMesh = GameHandler.GetInstance().quadMesh;
        Material material = GameHandler.GetInstance().walkingSpriteSheetMaterial;
        int shaderPropertyId = Shader.PropertyToID("_MainTex_UV");

        EntityQuery entityQuery = state.GetEntityQuery(typeof(SpriteSheetAnimationData));
        NativeArray<SpriteSheetAnimationData> animationDataArray = entityQuery.ToComponentDataArray<SpriteSheetAnimationData>(Allocator.Temp);

       


        int sliceCount = 1023;

        for(int i = 0; i < animationDataArray.Length; i+= sliceCount)
        {
            int sliceSize = math.min(animationDataArray.Length - i, sliceCount);
            List<Matrix4x4> matrixList = new List<Matrix4x4>();
            List<Vector4> uvList = new List<Vector4>();
            for (int j = 0; j < sliceSize; j++)
            {
                SpriteSheetAnimationData spriteSheeetAnimationData = animationDataArray[i + j];
                matrixList.Add(spriteSheeetAnimationData.matrix);
                uvList.Add(spriteSheeetAnimationData.uv);
            }
            materialPropertyBlock.SetVectorArray(shaderPropertyId, uvList);

            Graphics.DrawMeshInstanced(quadMesh, 0, material, matrixList, materialPropertyBlock);
        }

    }
}