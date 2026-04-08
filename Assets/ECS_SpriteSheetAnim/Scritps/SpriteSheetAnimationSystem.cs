using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct SpriteSheetAnimationData : IComponentData
{
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
        int advance = (int)(sprite.frameTimer * sprite.invFrameTimerMax);

        if (advance > 0)
        {
            sprite.frameTimer -= advance * sprite.frameTimerMax;
            sprite.currentFrame = (sprite.currentFrame + advance) % sprite.frameCount;

            // Cập nhật cả UV và Size mới
            sprite.currentUV = sprite.uvArrayBlob.Value.uvs[sprite.currentFrame];
            sprite.currentSize = sprite.uvArrayBlob.Value.sizes[sprite.currentFrame];
        }

        transform.Position.z = transform.Position.y * 0.01f;
    }
}