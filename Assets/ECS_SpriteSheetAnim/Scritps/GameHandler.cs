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
    public SpriteAtlas spriteAtlas; // KÉO ATLAS VÀO ĐÂY TRONG INSPECTOR
    public ScriptableSpriteSheet enemyConfigs;
    public BlobAssetReference<SpriteUVBlob>[] uvBlobs;
    public Texture2D mainAtlasTexture;
    private void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;
        Texture2D[] atlasTextures = new Texture2D[spriteAtlas.spriteCount];
        // Cách nhanh nhất để lấy Texture là lấy từ một Sprite bất kỳ trong đó
        Sprite firstSprite = enemyConfigs.enemyAnimConfigs[0].sprites[0];
        mainAtlasTexture = firstSprite.texture;
        uvBlobs = new BlobAssetReference<SpriteUVBlob>[enemyConfigs.enemyAnimConfigs.Count];

        for (int i = 0; i < enemyConfigs.enemyAnimConfigs.Count; i++)
        {
            uvBlobs[i] = CreateUVBlobFromSprites(enemyConfigs.enemyAnimConfigs[i]);
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
            int indexEnemy = UnityEngine.Random.Range(0, enemyConfigs.enemyAnimConfigs.Count);
            var enemyConfig = enemyConfigs.enemyAnimConfigs[indexEnemy];

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
        var arrayBuilder = builder.Allocate(ref uvBlob.uvs, frameCount);
        var sizeBuilder = builder.Allocate(ref uvBlob.sizes, frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            Sprite spr = config.sprites[i];

            // Lấy UV trực tiếp từ Sprite (Unity đã tính toán sẵn vị trí trong Atlas)
            // uv[0] thường là bottom-left, uv[2] là top-right
            Vector2[] uvs = spr.uv;
            float minX = uvs[0].x;
            float minY = uvs[0].y;
            float maxX = uvs[0].x;
            float maxY = uvs[0].y;

            for (int j = 1; j < uvs.Length; j++)
            {
                minX = math.min(minX, uvs[j].x);
                minY = math.min(minY, uvs[j].y);
                maxX = math.max(maxX, uvs[j].x);
                maxY = math.max(maxY, uvs[j].y);
            }

            // uvData.xy là offset (min UV), uvData.zw là scale (width/height của UV)
            arrayBuilder[i] = new float4(minX, minY, maxX - minX, maxY - minY);

            // Kích thước thật của Sprite dựa trên pixelsPerUnit
            sizeBuilder[i] = new float2(spr.rect.width / spr.pixelsPerUnit, spr.rect.height / spr.pixelsPerUnit);
        }

        var result = builder.CreateBlobAssetReference<SpriteUVBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    public static GameHandler GetInstance() => instance;

    private void OnDestroy()
    {
        if (uvBlobs != null)
            foreach (var b in uvBlobs) if (b.IsCreated) b.Dispose();
    }
}