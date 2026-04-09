using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// --- DÀNH CHO ECS COMPONENT ---
public struct SpriteSheetAnimationData : IComponentData
{
    public int textureIndex;
    public int atlasIndex;

    // Quản lý trạng thái Anim hiện tại
    public int currentAnimIndex; // 0 = Idle, 1 = Run...
    public int currentFrame;
    public float frameTimer;

    public float4 currentUV;
    public float2 currentSize;

    // Tham chiếu đến cấu trúc Blob mới
    public BlobAssetReference<EnemyAnimationsBlob> animsBlob;
}

public struct SpriteUVBlob
{
    public BlobArray<float4> uvs;
    public BlobArray<float2> sizes;
}


[BurstCompile]
public partial struct SpriteSheetAnimationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        AnimationJob job = new AnimationJob
        {
            deltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(SpriteSheetAnimationData))]
[WithAll(typeof(VisibleTag))]
public partial struct AnimationJob : IJobEntity
{
    public float deltaTime;

    [BurstCompile]
    public void Execute(ref SpriteSheetAnimationData sprite, ref LocalTransform transform)
    {
        // Trỏ thẳng tới Animation đang chạy (Idle, Run, v.v...)
        ref var currentSeq = ref sprite.animsBlob.Value.sequences[sprite.currentAnimIndex];

        sprite.frameTimer += deltaTime;
        bool frameChanged = false;

        // Dùng while tự bù frame khi lag, dựa vào frameTimerMax CỦA RIÊNG ANIM NÀY
        while (sprite.frameTimer >= currentSeq.frameTimerMax)
        {
            sprite.frameTimer -= currentSeq.frameTimerMax;
            sprite.currentFrame = (sprite.currentFrame + 1) % currentSeq.frameCount;
            frameChanged = true;
        }

        if (frameChanged)
        {
            sprite.currentUV = currentSeq.uvs[sprite.currentFrame];
            sprite.currentSize = currentSeq.sizes[sprite.currentFrame];
        }

        transform.Position.z = transform.Position.y * 0.01f;
    }
}