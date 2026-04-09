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

    public SpriteAtlas[] spriteAtlases;
    public Texture2D[] atlasTextures;

    public ScriptableSpriteSheet enemyConfigs;

    // ĐỔI SANG KIỂU BLOB MỚI
    public BlobAssetReference<EnemyAnimationsBlob>[] uvBlobs;

    private void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;

        atlasTextures = new Texture2D[spriteAtlases.Length];
        for (int i = 0; i < spriteAtlases.Length; i++)
        {
            Sprite[] tempSprites = new Sprite[1];
            spriteAtlases[i].GetSprites(tempSprites);
            if (tempSprites[0] != null)
            {
                atlasTextures[i] = tempSprites[0].texture;
            }
        }

        uvBlobs = new BlobAssetReference<EnemyAnimationsBlob>[enemyConfigs.enemyAnimConfigs.Count];

        for (int i = 0; i < enemyConfigs.enemyAnimConfigs.Count; i++)
        {
            uvBlobs[i] = CreateUVBlobFromSprites(enemyConfigs.enemyAnimConfigs[i]);
        }
    }

    private void Start()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(
            typeof(LocalTransform), typeof(SpriteSheetAnimationData),
            typeof(VisibleTag), typeof(SpatialCell), typeof(AtlasSharedTag)
        );

        NativeArray<Entity> entityArray = new NativeArray<Entity>(AmountEntity, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);

        foreach (Entity entity in entityArray)
        {
            int indexEnemy = UnityEngine.Random.Range(0, enemyConfigs.enemyAnimConfigs.Count);
            var enemyConfig = enemyConfigs.enemyAnimConfigs[indexEnemy];

            int myAtlasIndex = enemyConfig.atlasIndex;

            entityManager.SetSharedComponent(entity, new AtlasSharedTag { atlasIndex = myAtlasIndex });
            float x = UnityEngine.Random.Range(-XRange, XRange);
            float y = UnityEngine.Random.Range(-YRange, YRange);

            entityManager.SetComponentData(entity, new LocalTransform { Position = new float3(x, y, 0), Scale = 1 });

            // MẶC ĐỊNH LẤY ANIMATION SỐ 0 ĐỂ BẮT ĐẦU (ví dụ: Idle)
            int defaultAnimIndex = 0;
            int FrameCount = enemyConfig.sequences[defaultAnimIndex].sprites.Length;
            int startFrame = UnityEngine.Random.Range(0, FrameCount);

            entityManager.SetComponentData(entity,
                new SpriteSheetAnimationData
                {
                    textureIndex = indexEnemy,
                    atlasIndex = myAtlasIndex,

                    // Thiết lập trạng thái anim hiện tại
                    currentAnimIndex = defaultAnimIndex,
                    currentFrame = startFrame,
                    frameTimer = 0,

                    // Gán UV ban đầu dựa trên Anim 0
                    currentUV = uvBlobs[indexEnemy].Value.sequences[defaultAnimIndex].uvs[startFrame],
                    currentSize = uvBlobs[indexEnemy].Value.sequences[defaultAnimIndex].sizes[startFrame],
                    animsBlob = uvBlobs[indexEnemy]
                }
            );
        }
        entityArray.Dispose();
    }

    // HÀM TẠO BLOB LỒNG NHAU (ANIMATION -> FRAMES)
    private BlobAssetReference<EnemyAnimationsBlob> CreateUVBlobFromSprites(EnemyAnimConfig config)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref EnemyAnimationsBlob root = ref builder.ConstructRoot<EnemyAnimationsBlob>();

        int animCount = config.sequences.Length;
        // Cấp phát mảng chứa các Animation
        var seqArrayBuilder = builder.Allocate(ref root.sequences, animCount);

        for (int i = 0; i < animCount; i++)
        {
            AnimSequence seqConfig = config.sequences[i];
            int frameCount = seqConfig.sprites.Length;

            seqArrayBuilder[i].frameCount = frameCount;
            seqArrayBuilder[i].frameTimerMax = seqConfig.frameTimerMax;

            // Cấp phát mảng UV và Size bên trong Animation này
            var uvBuilder = builder.Allocate(ref seqArrayBuilder[i].uvs, frameCount);
            var sizeBuilder = builder.Allocate(ref seqArrayBuilder[i].sizes, frameCount);

            for (int j = 0; j < frameCount; j++)
            {
                Sprite spr = seqConfig.sprites[j];
                Vector2[] uvs = spr.uv;

                float minX = uvs[0].x, minY = uvs[0].y, maxX = uvs[0].x, maxY = uvs[0].y;
                for (int k = 1; k < uvs.Length; k++)
                {
                    minX = math.min(minX, uvs[k].x);
                    minY = math.min(minY, uvs[k].y);
                    maxX = math.max(maxX, uvs[k].x);
                    maxY = math.max(maxY, uvs[k].y);
                }

                uvBuilder[j] = new float4(minX, minY, maxX - minX, maxY - minY);
                sizeBuilder[j] = new float2(spr.rect.width / spr.pixelsPerUnit, spr.rect.height / spr.pixelsPerUnit);
            }
        }

        var result = builder.CreateBlobAssetReference<EnemyAnimationsBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    public static GameHandler GetInstance() => instance;

    private void OnDestroy()
    {
        if (uvBlobs != null)
        {
            foreach (var b in uvBlobs)
            {
                if (b.IsCreated) b.Dispose();
            }
        }
    }
}