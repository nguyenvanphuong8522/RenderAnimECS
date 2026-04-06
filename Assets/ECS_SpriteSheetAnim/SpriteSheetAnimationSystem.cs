using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public struct SpriteSheetAnimationData : IComponentData
{
    public int currentFrame;
    public int frameCount;
    public float frameTimer;
    public float frameTimerMax;
    public Vector4 uv;
    public float uvWidth;
    public float invFrameTimerMax;
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
public partial struct AnimationJob : IJobEntity
{
    public float deltaTime;

    public void Execute(ref SpriteSheetAnimationData sprite, ref LocalTransform transform)
    {
        sprite.frameTimer += deltaTime;

        int advance = (int)(sprite.frameTimer * sprite.invFrameTimerMax);

        if (advance > 0)
        {
            sprite.frameTimer -= advance * sprite.frameTimerMax;

            sprite.currentFrame += advance;

            if (sprite.currentFrame >= sprite.frameCount)
                sprite.currentFrame -= sprite.frameCount;

            float x = sprite.uvWidth * sprite.currentFrame;

            sprite.uv.x = sprite.uvWidth;
            sprite.uv.y = 1;
            sprite.uv.z = x;
            sprite.uv.w = 0;
        }

        transform.Position.z =
            transform.Position.y * 0.01f;
    }
}