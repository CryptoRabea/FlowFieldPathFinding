using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// ============================================================================
// FLOW FIELD GENERATION SYSTEM
// ============================================================================
// Generates a flow field using:
// 1. Cost Field: Mark obstacle cells
// 2. Integration Field: Dijkstra-like wavefront from destination
// 3. Flow Field: Calculate flow direction from integration gradient
//
// Performance: Runs single-threaded because integration is inherently sequential
// (wavefront expansion). For huge grids (>200x200), consider hierarchical approach.
// ============================================================================

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct FlowFieldGenerationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowFieldConfig>();
        state.RequireForUpdate<FlowFieldTarget>();
    }

    // OnUpdate cannot be Burst compiled - it creates entities and queries (structural changes)
    public void OnUpdate(ref SystemState state)
    {
        // Only regenerate if target has changed
        var target = SystemAPI.GetSingleton<FlowFieldTarget>();
        if (!target.HasChanged)
            return;

        var config = SystemAPI.GetSingleton<FlowFieldConfig>();

        // Get or create flow field entity
        var flowFieldEntity = GetOrCreateFlowFieldEntity(ref state, config);

        // Get buffers
        var costBuffer = SystemAPI.GetBuffer<FlowFieldCostBuffer>(flowFieldEntity);
        var integrationBuffer = SystemAPI.GetBuffer<FlowFieldIntegrationBuffer>(flowFieldEntity);
        var directionBuffer = SystemAPI.GetBuffer<FlowFieldDirectionBuffer>(flowFieldEntity);

        int gridSize = config.GridWidth * config.GridHeight;

        // Ensure buffers are correct size
        if (costBuffer.Length != gridSize)
        {
            costBuffer.ResizeUninitialized(gridSize);
            integrationBuffer.ResizeUninitialized(gridSize);
            directionBuffer.ResizeUninitialized(gridSize);
        }

        // Calculate destination cell from world position
        float3 localPos = target.Position - config.GridOrigin;
        int2 destCell = new int2(
            (int)(localPos.x / config.CellSize),
            (int)(localPos.z / config.CellSize)
        );

        // Clamp to grid bounds
        destCell = math.clamp(destCell, int2.zero, new int2(config.GridWidth - 1, config.GridHeight - 1));

        // Step 1: Build cost field
        var buildCostJob = new BuildCostFieldJob
        {
            CostBuffer = costBuffer.AsNativeArray(),
            GridWidth = config.GridWidth,
            GridHeight = config.GridHeight,
            DefaultCost = config.DefaultCost
        };
        buildCostJob.Run();

        // Step 2: Mark obstacles in cost field
        // (We'll do this inline for simplicity; in production, use a separate job)
        foreach (var (transform, obstacle) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<FlowFieldObstacle>>())
        {
            MarkObstacle(costBuffer.AsNativeArray(), config, transform.ValueRO.Position, obstacle.ValueRO.Radius);
        }

        // Step 3: Build integration field via wavefront/Dijkstra
        var buildIntegrationJob = new BuildIntegrationFieldJob
        {
            CostBuffer = costBuffer.AsNativeArray(),
            IntegrationBuffer = integrationBuffer.AsNativeArray(),
            GridWidth = config.GridWidth,
            GridHeight = config.GridHeight,
            DestinationCell = destCell
        };
        buildIntegrationJob.Run(); // Single-threaded; wavefront is sequential

        // Step 4: Build flow directions from integration gradient
        var buildFlowJob = new BuildFlowDirectionFieldJob
        {
            IntegrationBuffer = integrationBuffer.AsNativeArray(),
            DirectionBuffer = directionBuffer.AsNativeArray(),
            GridWidth = config.GridWidth,
            GridHeight = config.GridHeight
        };
        buildFlowJob.ScheduleParallel(gridSize, 64, state.Dependency).Complete();

        // Update flow field data component
        SystemAPI.SetSingleton(new FlowFieldData
        {
            GridWidth = config.GridWidth,
            GridHeight = config.GridHeight,
            CellSize = config.CellSize,
            GridOrigin = config.GridOrigin,
            DestinationCell = destCell,
            NeedsUpdate = false
        });

        // Clear target changed flag
        SystemAPI.SetSingleton(new FlowFieldTarget
        {
            Position = target.Position,
            HasChanged = false
        });
    }

    private Entity GetOrCreateFlowFieldEntity(ref SystemState state, FlowFieldConfig config)
    {
        // Try to find existing flow field entity
        var query = SystemAPI.QueryBuilder().WithAll<FlowFieldCostBuffer>().Build();
        if (!query.IsEmpty)
        {
            return query.GetSingletonEntity();
        }

        // Create new entity with buffers and FlowFieldData component
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<FlowFieldCostBuffer>(entity);
        state.EntityManager.AddBuffer<FlowFieldIntegrationBuffer>(entity);
        state.EntityManager.AddBuffer<FlowFieldDirectionBuffer>(entity);

        // Add FlowFieldData component (singleton)
        state.EntityManager.AddComponentData(entity, new FlowFieldData
        {
            GridWidth = config.GridWidth,
            GridHeight = config.GridHeight,
            CellSize = config.CellSize,
            GridOrigin = config.GridOrigin,
            DestinationCell = int2.zero,
            NeedsUpdate = false
        });

        return entity;
    }

    private void MarkObstacle(NativeArray<FlowFieldCostBuffer> costBuffer, FlowFieldConfig config, float3 position, float radius)
    {
        float3 localPos = position - config.GridOrigin;
        int2 centerCell = new int2(
            (int)(localPos.x / config.CellSize),
            (int)(localPos.z / config.CellSize)
        );

        int cellRadius = (int)math.ceil(radius / config.CellSize);

        for (int y = -cellRadius; y <= cellRadius; y++)
        {
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                int2 cell = centerCell + new int2(x, y);
                if (cell.x >= 0 && cell.x < config.GridWidth && cell.y >= 0 && cell.y < config.GridHeight)
                {
                    int index = cell.y * config.GridWidth + cell.x;
                    costBuffer[index] = new FlowFieldCostBuffer { Value = config.ObstacleCost };
                }
            }
        }
    }
}

// ============================================================================
// JOBS
// ============================================================================

/// <summary>
/// Initialize cost field with default values.
/// Parallelizable since each cell is independent.
/// </summary>
[BurstCompile]
struct BuildCostFieldJob : IJob
{
    public NativeArray<FlowFieldCostBuffer> CostBuffer;
    public int GridWidth;
    public int GridHeight;
    public byte DefaultCost;

    public void Execute()
    {
        for (int i = 0; i < CostBuffer.Length; i++)
        {
            CostBuffer[i] = new FlowFieldCostBuffer { Value = DefaultCost };
        }
    }
}

/// <summary>
/// Build integration field using Dijkstra wavefront expansion.
/// NOT parallelizable due to sequential dependency (each cell depends on neighbors).
///
/// Algorithm: Start from destination with cost 0, expand to neighbors, accumulating cost.
/// Uses open list (queue) for wavefront. Runs in O(n log n) worst case, but typically O(n).
///
/// Memory: Uses temporary NativeQueue for wavefront (allocated per-job, disposed after).
/// </summary>
[BurstCompile]
struct BuildIntegrationFieldJob : IJob
{
    [ReadOnly] public NativeArray<FlowFieldCostBuffer> CostBuffer;
    public NativeArray<FlowFieldIntegrationBuffer> IntegrationBuffer;
    public int GridWidth;
    public int GridHeight;
    public int2 DestinationCell;

    const ushort MAX_COST = 65535;

    public void Execute()
    {
        // Initialize all cells to max cost
        for (int i = 0; i < IntegrationBuffer.Length; i++)
        {
            IntegrationBuffer[i] = new FlowFieldIntegrationBuffer { Value = MAX_COST };
        }

        // Wavefront queue (Dijkstra)
        var openList = new NativeList<int2>(GridWidth * GridHeight / 4, Allocator.Temp);

        // Set destination cell to cost 0
        int destIndex = DestinationCell.y * GridWidth + DestinationCell.x;
        IntegrationBuffer[destIndex] = new FlowFieldIntegrationBuffer { Value = 0 };
        openList.Add(DestinationCell);

        // 4-directional neighbors (can extend to 8 for diagonal movement)
        var neighbors = new NativeArray<int2>(4, Allocator.Temp);
        neighbors[0] = new int2(0, 1);   // North
        neighbors[1] = new int2(1, 0);   // East
        neighbors[2] = new int2(0, -1);  // South
        neighbors[3] = new int2(-1, 0);  // West

        // Process wavefront
        while (openList.Length > 0)
        {
            // Pop first cell (breadth-first)
            int2 currentCell = openList[0];
            openList.RemoveAtSwapBack(0);

            int currentIndex = currentCell.y * GridWidth + currentCell.x;
            ushort currentCost = IntegrationBuffer[currentIndex].Value;

            // Check neighbors
            for (int i = 0; i < 4; i++)
            {
                int2 neighborCell = currentCell + neighbors[i];

                // Bounds check
                if (neighborCell.x < 0 || neighborCell.x >= GridWidth ||
                    neighborCell.y < 0 || neighborCell.y >= GridHeight)
                    continue;

                int neighborIndex = neighborCell.y * GridWidth + neighborCell.x;

                // Skip obstacles
                if (CostBuffer[neighborIndex].Value == 255)
                    continue;

                // Calculate new cost
                ushort newCost = (ushort)(currentCost + CostBuffer[neighborIndex].Value);

                // Update if better path found
                if (newCost < IntegrationBuffer[neighborIndex].Value)
                {
                    IntegrationBuffer[neighborIndex] = new FlowFieldIntegrationBuffer { Value = newCost };
                    openList.Add(neighborCell);
                }
            }
        }

        openList.Dispose();
        neighbors.Dispose();
    }
}

/// <summary>
/// Build flow direction field from integration field gradient.
/// For each cell, flow direction points toward neighbor with lowest integration cost.
/// Parallelizable via IJobFor since each cell's direction is independent.
/// </summary>
[BurstCompile]
struct BuildFlowDirectionFieldJob : IJobFor
{
    [ReadOnly] public NativeArray<FlowFieldIntegrationBuffer> IntegrationBuffer;
    public NativeArray<FlowFieldDirectionBuffer> DirectionBuffer;
    public int GridWidth;
    public int GridHeight;

    public void Execute(int index)
    {
        int x = index % GridWidth;
        int y = index / GridWidth;

        ushort currentCost = IntegrationBuffer[index].Value;

        // If unreachable, set zero direction
        if (currentCost == 65535)
        {
            DirectionBuffer[index] = new FlowFieldDirectionBuffer { Value = float2.zero };
            return;
        }

        // Sample 8 neighbors to find lowest cost direction
        float2 direction = float2.zero;
        ushort bestCost = currentCost;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight)
                    continue;

                int neighborIndex = ny * GridWidth + nx;
                ushort neighborCost = IntegrationBuffer[neighborIndex].Value;

                if (neighborCost < bestCost)
                {
                    bestCost = neighborCost;
                    direction = new float2(dx, dy);
                }
            }
        }

        // Normalize direction
        if (math.lengthsq(direction) > 0)
        {
            direction = math.normalize(direction);
        }

        DirectionBuffer[index] = new FlowFieldDirectionBuffer { Value = direction };
    }
}
