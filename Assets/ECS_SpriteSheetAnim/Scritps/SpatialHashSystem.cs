using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct SpatialCell : IComponentData
{
    public int2 cell;
}

public struct VisibleTag : IComponentData, IEnableableComponent
{
}

[BurstCompile]
public partial struct SpatialHashSystem : ISystem
{
    const float cellSize = 5f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SpatialHashJob job = new SpatialHashJob
        {
            cellSize = cellSize
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct SpatialHashJob : IJobEntity
{
    public float cellSize;

    public void Execute(ref SpatialCell cell, in LocalTransform transform)
    {
        float2 pos2D = transform.Position.xy;
        cell.cell = cell.cell = (int2)math.floor(pos2D / cellSize); ;
    }
}