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


    // --- PHẦN THÊM CHO TEXT DAMAGE ---
    [Header("Text Damage Settings")]
    public SpriteAtlas fontAtlas; // Kéo SpriteAtlas chứa các chữ số vào đây
    public Texture2D fontAtlasTexture; // Hệ thống tự động lấy
    [Tooltip("Kéo 10 Sprite chữ số vào đây THEO THỨ TỰ từ 0 đến 9")]
    public Sprite[] numberSprites = new Sprite[10];
    public BlobAssetReference<SpriteUVBlob> numberUVBlob;


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

        // --- 2. KHỞI TẠO DỮ LIỆU FONT TEXT DAMAGE ---
        if (fontAtlas != null && numberSprites.Length == 10 && numberSprites[0] != null)
        {
            fontAtlasTexture = numberSprites[0].texture;
            numberUVBlob = CreateFontUVBlob(numberSprites);
        }
        else
        {
            Debug.LogWarning("Chưa cấu hình xong Text Damage trong GameHandler!");
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

    private BlobAssetReference<SpriteUVBlob> CreateFontUVBlob(Sprite[] sprites)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref SpriteUVBlob uvBlob = ref builder.ConstructRoot<SpriteUVBlob>();

        int count = sprites.Length; // Luôn là 10 số (0-9)
        var arrayBuilder = builder.Allocate(ref uvBlob.uvs, count);
        var sizeBuilder = builder.Allocate(ref uvBlob.sizes, count);

        for (int i = 0; i < count; i++)
        {
            Sprite spr = sprites[i];
            if (spr == null) continue;

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


    // --- HÀM TIỆN ÍCH ĐỂ GỌI SPAWN TEXT DAMAGE ---
    // Bạn có thể gọi GameHandler.GetInstance().SpawnDamageText(pos, 123) từ bất kỳ đâu!
    public static void SpawnDamageText(float3 spawnPosition, int damageValue)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        Entity textEntity = entityManager.CreateEntity(
            typeof(LocalTransform),
            typeof(TextDamageData),
            typeof(DamageDigitElement)
        );

        entityManager.SetComponentData(textEntity, new LocalTransform { Position = spawnPosition, Scale = 1f });

        // Cài đặt khoảng cách giữa các số (bạn có thể tinh chỉnh số 0.3f này tùy kích thước font)
        entityManager.SetComponentData(textEntity, new TextDamageData { digitSpacing = 2f });

        DynamicBuffer<DamageDigitElement> buffer = entityManager.GetBuffer<DamageDigitElement>(textEntity);

        // Tách số ra thành từng chữ số và nhét vào Buffer (Ví dụ: 123 -> 1, 2, 3)
        string dmgString = damageValue.ToString();
        for (int i = 0; i < dmgString.Length; i++)
        {
            int digit = int.Parse(dmgString[i].ToString());
            buffer.Add(new DamageDigitElement { digitValue = digit });
        }
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


[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TestAnimationSwitchSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Nhấn phím Space để đổi tất cả quái vật sang Animation tiếp theo
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Sử dụng ScheduleParallel để đổi hàng chục ngàn Entity trong chớp mắt
            Entities.ForEach((ref SpriteSheetAnimationData animData) =>
            {
                // Lấy tổng số lượng Animation mà con quái này đang có
                int totalAnims = animData.animsBlob.Value.sequences.Length;

                if (totalAnims <= 1) return; // Nếu chỉ có 1 Anim thì không cần đổi

                // Chuyển sang Anim tiếp theo (nếu vượt quá thì quay vòng lại 0)
                int nextAnimIndex = (animData.currentAnimIndex + 1) % totalAnims;

                // Cập nhật trạng thái
                animData.currentAnimIndex = nextAnimIndex;
                animData.currentFrame = 0; // Reset về frame đầu tiên
                animData.frameTimer = 0f;  // Reset bộ đếm thời gian

                // QUAN TRỌNG: Ép cập nhật UV và Size ngay lập tức để Frame tiếp theo Render đúng luôn, không bị chớp đen
                ref var currentSeq = ref animData.animsBlob.Value.sequences[nextAnimIndex];
                animData.currentUV = currentSeq.uvs[0];
                animData.currentSize = currentSeq.sizes[0];

            }).ScheduleParallel();
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            GameHandler.SpawnDamageText(float3.zero, 123);
        }
        // --- HOẶC BẠN CÓ THỂ TEST BẰNG CÁCH NHẤN PHÍM SỐ ---
        // Nhấn phím 1 để ép TẤT CẢ về Anim 0 (Idle)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ForceAnimation(0);
        }
        
        // Nhấn phím 2 để ép TẤT CẢ về Anim 1 (Run)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ForceAnimation(1);
        }
    }

    // Hàm tiện ích để ép một Index cụ thể
    private void ForceAnimation(int targetIndex)
    {
        Entities.ForEach((ref SpriteSheetAnimationData animData) =>
        {
            int totalAnims = animData.animsBlob.Value.sequences.Length;

            // Kiểm tra xem con quái này có đủ số lượng Anim không (tránh lỗi Out of Range)
            if (targetIndex < totalAnims && animData.currentAnimIndex != targetIndex)
            {
                animData.currentAnimIndex = targetIndex;
                animData.currentFrame = 0;
                animData.frameTimer = 0f;

                ref var currentSeq = ref animData.animsBlob.Value.sequences[targetIndex];
                animData.currentUV = currentSeq.uvs[0];
                animData.currentSize = currentSeq.sizes[0];
            }
        }).ScheduleParallel();
    }
}