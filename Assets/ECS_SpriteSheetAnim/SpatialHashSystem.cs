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
        float3 pos = transform.Position;

        int2 newCell;
        newCell.x = (int)math.floor(pos.x / cellSize);

        newCell.y = (int)math.floor(pos.y / cellSize);

        cell.cell = newCell;
    }
}