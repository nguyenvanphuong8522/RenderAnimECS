using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.U2D;

public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;
    public int AmountEntity;
    public float XRange, YRange;

    public Material baseWalkingMaterial;

    // ĐỔI THÀNH MẢNG ATLAS
    public SpriteAtlas[] spriteAtlases;
    public Texture2D[] atlasTextures;

    public ScriptableSpriteSheet enemyConfigs;
    public BlobAssetReference<SpriteUVBlob>[] uvBlobs;

    private void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;

        // 1. Trích xuất Texture từ tất cả các Atlas
        atlasTextures = new Texture2D[spriteAtlases.Length];
        for (int i = 0; i < spriteAtlases.Length; i++)
        {
            // Tạm thời lấy texture gốc bằng cách tạo 1 sprite giả từ atlas (cách an toàn trong code)
            Sprite[] tempSprites = new Sprite[1];
            spriteAtlases[i].GetSprites(tempSprites);
            if (tempSprites[0] != null)
            {
                atlasTextures[i] = tempSprites[0].texture;
            }
        }

        uvBlobs = new BlobAssetReference<SpriteUVBlob>[enemyConfigs.enemyAnimConfigs.Count];

        for (int i = 0; i < enemyConfigs.enemyAnimConfigs.Count; i++)
        {
            // Lấy ID atlas trực tiếp từ config bạn đã setup ở Inspector
            int atlasID = enemyConfigs.enemyAnimConfigs[i].atlasIndex;
            uvBlobs[i] = CreateUVBlobFromSprites(enemyConfigs.enemyAnimConfigs[i], atlasID);
        }
    }


    private void Start()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(LocalTransform), typeof(SpriteSheetAnimationData),
            typeof(VisibleTag), typeof(SpatialCell), typeof(AtlasSharedTag));

        NativeArray<Entity> entityArray = new NativeArray<Entity>(AmountEntity, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);

        foreach (Entity entity in entityArray)
        {
            int indexEnemy = UnityEngine.Random.Range(0, enemyConfigs.enemyAnimConfigs.Count);
            var enemyConfig = enemyConfigs.enemyAnimConfigs[indexEnemy];

            // Lấy ra Atlas Index của enemy này
            int myAtlasIndex = enemyConfig.atlasIndex;

            entityManager.SetSharedComponent(entity, new AtlasSharedTag { atlasIndex = myAtlasIndex });
            float x = UnityEngine.Random.Range(-XRange, XRange);
            float y = UnityEngine.Random.Range(-YRange, YRange);

            entityManager.SetComponentData(entity, new LocalTransform { Position = new float3(x, y, 0), Scale = 1 });

            int FrameCount = enemyConfig.sprites.Length;
            int startFrame = UnityEngine.Random.Range(0, FrameCount);

            entityManager.SetComponentData(entity,
                new SpriteSheetAnimationData
                {
                    textureIndex = indexEnemy,
                    atlasIndex = myAtlasIndex, // LƯU ATLAS INDEX VÀO ĐÂY
                    currentFrame = startFrame,
                    frameCount = FrameCount,
                    frameTimer = 0,
                    frameTimerMax = enemyConfig.frameTimerMax,
                    invFrameTimerMax = 1f / enemyConfig.frameTimerMax,
                    currentUV = uvBlobs[indexEnemy].Value.uvs[startFrame],
                    currentSize = uvBlobs[indexEnemy].Value.sizes[startFrame],
                    uvArrayBlob = uvBlobs[indexEnemy]
                }
            );
        }
        entityArray.Dispose();
    }

    private BlobAssetReference<SpriteUVBlob> CreateUVBlobFromSprites(EnemyAnimConfig config, int atlasIndex)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref SpriteUVBlob uvBlob = ref builder.ConstructRoot<SpriteUVBlob>();

        int frameCount = config.sprites.Length;
        var arrayBuilder = builder.Allocate(ref uvBlob.uvs, frameCount);
        var sizeBuilder = builder.Allocate(ref uvBlob.sizes, frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            Sprite spr = config.sprites[i];
            Vector2[] uvs = spr.uv;

            float minX = uvs[0].x, minY = uvs[0].y, maxX = uvs[0].x, maxY = uvs[0].y;
            for (int j = 1; j < uvs.Length; j++)
            {
                minX = math.min(minX, uvs[j].x);
                minY = math.min(minY, uvs[j].y);
                maxX = math.max(maxX, uvs[j].x);
                maxY = math.max(maxY, uvs[j].y);
            }

            arrayBuilder[i] = new float4(minX, minY, maxX - minX, maxY - minY);
            sizeBuilder[i] = new float2(spr.rect.width / spr.pixelsPerUnit, spr.rect.height / spr.pixelsPerUnit);
        }

        var result = builder.CreateBlobAssetReference<SpriteUVBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    public static GameHandler GetInstance() => instance;
    private void OnDestroy() { /* ... như cũ ... */ }
}