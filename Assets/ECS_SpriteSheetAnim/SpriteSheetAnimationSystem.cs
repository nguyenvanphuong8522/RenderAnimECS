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

    public void Execute(
        ref SpriteSheetAnimationData sprite,
        ref LocalTransform transform)
    {
        sprite.frameTimer += deltaTime;

        int advance =
            (int)(sprite.frameTimer / sprite.frameTimerMax);

        if (advance > 0)
        {
            sprite.frameTimer -=
                advance * sprite.frameTimerMax;

            sprite.currentFrame =
                (sprite.currentFrame + advance)
                % sprite.frameCount;

            sprite.uv = new Vector4(
                sprite.uvWidth,
                1,
                sprite.uvWidth * sprite.currentFrame,
                0);
        }

        float3 pos = transform.Position;

        pos.z = pos.y * .01f;

        transform.Position = pos;
    }
}