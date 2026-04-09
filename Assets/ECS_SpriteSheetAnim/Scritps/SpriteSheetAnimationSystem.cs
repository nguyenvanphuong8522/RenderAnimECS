using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct SpriteSheetAnimationData : IComponentData
{
    public int atlasIndex;
    public int textureIndex;
    public int currentFrame;
    public int frameCount;
    public float frameTimer;
    public float frameTimerMax;
    public float invFrameTimerMax;

    // Lưu UV hiện tại để Render System lấy ra
    public float4 currentUV;
    public float2 currentSize;
    // Tham chiếu đến dữ liệu mảng UV (BlobAsset giúp truy cập cực nhanh trong Burst)
    public BlobAssetReference<SpriteUVBlob> uvArrayBlob;
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
        sprite.frameTimer += deltaTime;

        // Biến cờ để kiểm tra xem frame có thực sự thay đổi trong frame update này không
        bool frameChanged = false;

        // Dùng while an toàn hơn float division. 
        // Nếu game giật lag (deltaTime lớn), nó sẽ tự bù frame chuẩn xác.
        while (sprite.frameTimer >= sprite.frameTimerMax)
        {
            sprite.frameTimer -= sprite.frameTimerMax;
            sprite.currentFrame = (sprite.currentFrame + 1) % sprite.frameCount;
            frameChanged = true;
        }

        if (frameChanged)
        {
            sprite.currentUV = sprite.uvArrayBlob.Value.uvs[sprite.currentFrame];
            sprite.currentSize = sprite.uvArrayBlob.Value.sizes[sprite.currentFrame];
        }

        // Giữ nguyên logic sorting Z
        transform.Position.z = transform.Position.y * 0.01f;
    }
}