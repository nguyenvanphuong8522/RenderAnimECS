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
    public Matrix4x4 matrix;
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

        job.ScheduleParallel();
    }
}





[BurstCompile]
public partial struct AnimationJob : IJobEntity
{
    public float deltaTime;

    public void Execute(ref SpriteSheetAnimationData spriteData, ref LocalTransform transform)
    {
        spriteData.frameTimer += deltaTime;

        while (spriteData.frameTimer >= spriteData.frameTimerMax)
        {
            spriteData.frameTimer -= spriteData.frameTimerMax;



            spriteData.currentFrame = (spriteData.currentFrame + 1) % spriteData.frameCount;

            float uvWidth = 1f / spriteData.frameCount;

            spriteData.uv = new Vector4(uvWidth, 1f, uvWidth * spriteData.currentFrame, 0f);
        }
        float3 position = transform.Position;

        position.z = position.y * .01f;

        spriteData.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
    }
}