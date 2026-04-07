using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct CameraCullingSystem : ISystem
{
    const float cellSize = 5f;

    // cache camera
    static Camera cachedCamera;

    public void OnUpdate(ref SystemState state)
    {
        // nếu chưa có, tìm camera 1 lần
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null)
                return;
        }

        float heightHalf = cachedCamera.orthographicSize;
        float widthHalf = heightHalf * cachedCamera.aspect;

        float3 camPos = cachedCamera.transform.position;

        int2 minCell;
        int2 maxCell;

        minCell.x = (int)math.floor((camPos.x - widthHalf) / cellSize);
        maxCell.x = (int)math.floor((camPos.x + widthHalf) / cellSize);

        minCell.y = (int)math.floor((camPos.y - heightHalf) / cellSize);
        maxCell.y = (int)math.floor((camPos.y + heightHalf) / cellSize);

        int padding = 2;
        minCell -= padding;
        maxCell += padding;

        state.Dependency =
            new CullingJob
            {
                minCell = minCell,
                maxCell = maxCell
            }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct CullingJob : IJobEntity
{
    public int2 minCell;
    public int2 maxCell;

    public void Execute(in SpatialCell cell, EnabledRefRW<VisibleTag> visible)
    {
        int2 c = cell.cell;

        bool inside = math.all(c >= minCell) && math.all(c <= maxCell);

        if (visible.ValueRO != inside)
        {
            visible.ValueRW = inside;
        }
    }
}