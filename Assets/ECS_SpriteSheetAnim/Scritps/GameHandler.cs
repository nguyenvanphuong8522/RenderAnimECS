using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

[System.Serializable]
public class EnemyAnimConfig
{
    // Không cần columns, rows và frameCount nữa vì đã có mảng sprites lo
    public Texture2D texture;
    public Sprite[] sprites; // Kéo mảng sprite đã cắt vào đây
    public float frameTimerMax;
}
public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;
    public int AmountEntity;

    public float XRange;
    public float YRange;

    public Material baseWalkingMaterial;

    public EnemyAnimConfig[] enemyConfigs;
    public BlobAssetReference<SpriteUVBlob>[] uvBlobs;

    private void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;

        uvBlobs = new BlobAssetReference<SpriteUVBlob>[enemyConfigs.Length];

        for (int i = 0; i < enemyConfigs.Length; i++)
        {
            uvBlobs[i] = CreateUVBlobFromSprites(enemyConfigs[i]);
        }
    }

    private void Start()
    {

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(LocalTransform), typeof(SpriteSheetAnimationData), typeof(VisibleTag), typeof(SpatialCell));

        NativeArray<Entity> entityArray = new NativeArray<Entity>(AmountEntity, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);
        foreach (Entity entity in entityArray)
        {
            int indexEnemy = UnityEngine.Random.Range(0, enemyConfigs.Length);
            var enemyConfig = enemyConfigs[indexEnemy];

            float x = UnityEngine.Random.Range(-XRange, XRange);
            float y = UnityEngine.Random.Range(-YRange, YRange);



            entityManager.SetComponentData(entity,
                new LocalTransform
                {
                    Position = new float3(x, y, 0),
                    Scale = 1
                }
            );


            int FrameCount = enemyConfig.sprites.Length;
            float FrameTimerMax = enemyConfig.frameTimerMax;

            int startFrame = UnityEngine.Random.Range(0, FrameCount);
            entityManager.SetComponentData(entity,
                new SpriteSheetAnimationData
                {
                    textureIndex = indexEnemy,
                    currentFrame = startFrame,
                    frameCount = FrameCount,
                    frameTimer = 0,
                    frameTimerMax = FrameTimerMax,
                    invFrameTimerMax = 1f / FrameTimerMax,

                    // KHỞI TẠO NGAY GIÁ TRỊ BAN ĐẦU
                    currentUV = uvBlobs[indexEnemy].Value.uvs[startFrame],
                    currentSize = uvBlobs[indexEnemy].Value.sizes[startFrame],

                    uvArrayBlob = uvBlobs[indexEnemy]
                }
            );
        }

        entityArray.Dispose();
    }
    private BlobAssetReference<SpriteUVBlob> CreateUVBlobFromSprites(EnemyAnimConfig config)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref SpriteUVBlob uvBlob = ref builder.ConstructRoot<SpriteUVBlob>();

        int frameCount = config.sprites.Length;
        BlobBuilderArray<float4> arrayBuilder = builder.Allocate(ref uvBlob.uvs, frameCount);
        BlobBuilderArray<float2> sizeBuilder = builder.Allocate(ref uvBlob.sizes, frameCount); // Cấp phát bộ nhớ cho mảng Size

        float texWidth = config.texture.width;
        float texHeight = config.texture.height;

        for (int i = 0; i < frameCount; i++)
        {
            Sprite spr = config.sprites[i];
            Rect rect = spr.rect;

            // Tính UV
            float x = rect.x / texWidth;
            float y = rect.y / texHeight;
            float width = rect.width / texWidth;
            float height = rect.height / texHeight;
            arrayBuilder[i] = new float4(x, y, width, height);

            // Tính Kích thước (Size) theo World Space
            // spr.pixelsPerUnit mặc định của Unity thường là 100
            float ppu = spr.pixelsPerUnit;
            sizeBuilder[i] = new float2(rect.width / ppu, rect.height / ppu);
        }

        var result = builder.CreateBlobAssetReference<SpriteUVBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }
    public static GameHandler GetInstance()
    {
        return instance;
    }

    private void OnDestroy()
    {
        if (uvBlobs != null)
        {
            for (int i = 0; i < uvBlobs.Length; i++)
            {
                if (uvBlobs[i].IsCreated)
                    uvBlobs[i].Dispose();
            }
        }
    }

}
