using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteSheetRenderSystem : SystemBase
{
    private Matrix4x4[] matrixCache;
    private Vector4[] uvCache;
    private MaterialPropertyBlock materialPropertyBlock;
    private int uvPropertyId;

    protected override void OnCreate()
    {
        RequireForUpdate<SpriteSheetAnimationData>();
        matrixCache = new Matrix4x4[1023];
        uvCache = new Vector4[1023];
        materialPropertyBlock = new MaterialPropertyBlock();
        uvPropertyId = Shader.PropertyToID("_MainTex_UV");
    }

    protected override void OnUpdate()
    {
        var handler = GameHandler.GetInstance();
        if (handler == null) return;

        Mesh quadMesh = handler.quadMesh;
        Material material = handler.walkingSpriteSheetMaterial;

        // Reset bộ đếm để lấp đầy cache
        int currentIndex = 0;

        // Chạy trực tiếp trên các thực thể mà không copy mảng lớn
        Entities
            .ForEach((in SpriteSheetAnimationData data) =>
            {
                matrixCache[currentIndex] = data.matrix;
                uvCache[currentIndex] = data.uv;
                currentIndex++;

                // Khi đạt tới giới hạn 1023, vẽ ngay lập tức và reset bộ đếm
                if (currentIndex == 1023)
                {
                    DrawBatch(quadMesh, material, 1023);
                    currentIndex = 0;
                }
            }).WithoutBurst().Run(); // Phải dùng WithoutBurst vì truy cập mảng managed

        // Vẽ phần dư còn lại
        if (currentIndex > 0)
        {
            DrawBatch(quadMesh, material, currentIndex);
        }
    }

    private void DrawBatch(Mesh mesh, Material mat, int count)
    {
        materialPropertyBlock.SetVectorArray(uvPropertyId, uvCache);
        Graphics.DrawMeshInstanced(mesh, 0, mat, matrixCache, count, materialPropertyBlock);
    }
}