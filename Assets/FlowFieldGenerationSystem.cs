using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// System responsible for generating flow fields using a 3-stage algorithm:
    /// 1. Build Cost Field - Mark obstacles and traversable cells
    /// 2. Build Integration Field - Dijkstra wavefront expansion from destination
    /// 3. Build Flow Direction Field - Calculate gradient-based movement directions
    ///
    /// Only regenerates when FlowFieldTarget.HasChanged is true for performance.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct FlowFieldGenerationSystem : ISystem
    {
        private Entity _flowFieldEntity;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldConfig>();
            state.RequireForUpdate<FlowFieldTarget>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<FlowFieldConfig>();
            var target = SystemAPI.GetSingleton<FlowFieldTarget>();

            // Initialize flow field entity on first run
            if (!_initialized)
            {
                InitializeFlowFieldEntity(ref state, config);
                _initialized = true;
            }

            // Only regenerate when target changes
            if (!target.HasChanged)
                return;

            // Reset the changed flag
            target.HasChanged = false;
            SystemAPI.SetSingleton(target);

            // Get or ensure buffers exist
            var costBuffer = SystemAPI.GetBuffer<FlowFieldCostBuffer>(_flowFieldEntity);
            var integrationBuffer = SystemAPI.GetBuffer<FlowFieldIntegrationBuffer>(_flowFieldEntity);
            var directionBuffer = SystemAPI.GetBuffer<FlowFieldDirectionBuffer>(_flowFieldEntity);

            int gridSize = config.GridWidth * config.GridHeight;

            // Resize buffers if needed
            if (costBuffer.Length != gridSize)
            {
                costBuffer.ResizeUninitialized(gridSize);
                integrationBuffer.ResizeUninitialized(gridSize);
                directionBuffer.ResizeUninitialized(gridSize);
            }

            // Convert target position to grid cell
            int2 destCell = WorldToGrid(target.Position, config.GridOrigin, config.CellSize, config.GridWidth, config.GridHeight);

            // Update FlowFieldData
            var flowFieldData = SystemAPI.GetComponent<FlowFieldData>(_flowFieldEntity);
            flowFieldData.DestinationCell = destCell;
            flowFieldData.GridWidth = config.GridWidth;
            flowFieldData.GridHeight = config.GridHeight;
            flowFieldData.CellSize = config.CellSize;
            flowFieldData.GridOrigin = config.GridOrigin;
            SystemAPI.SetComponent(_flowFieldEntity, flowFieldData);

            // Stage 1: Build Cost Field
            var buildCostJob = new BuildCostFieldJob
            {
                GridWidth = config.GridWidth,
                GridHeight = config.GridHeight,
                CellSize = config.CellSize,
                GridOrigin = config.GridOrigin,
                DefaultCost = config.DefaultCost,
                ObstacleCost = config.ObstacleCost,
                CostField = costBuffer.AsNativeArray()
            };

            // Query obstacles
            var obstacleQuery = SystemAPI.QueryBuilder().WithAll<FlowFieldObstacle, LocalTransform>().Build();
            var obstacles = obstacleQuery.ToComponentDataArray<FlowFieldObstacle>(Allocator.TempJob);
            var obstaclePositions = obstacleQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            buildCostJob.Obstacles = obstacles;
            buildCostJob.ObstaclePositions = obstaclePositions;

            var costHandle = buildCostJob.Schedule();
            costHandle.Complete();

            obstacles.Dispose();
            obstaclePositions.Dispose();

            // Stage 2: Build Integration Field (Dijkstra)
            var buildIntegrationJob = new BuildIntegrationFieldJob
            {
                GridWidth = config.GridWidth,
                GridHeight = config.GridHeight,
                DestinationCell = destCell,
                CostField = costBuffer.AsNativeArray(),
                IntegrationField = integrationBuffer.AsNativeArray()
            };

            var integrationHandle = buildIntegrationJob.Schedule();
            integrationHandle.Complete();

            // Stage 3: Build Flow Direction Field
            var buildDirectionJob = new BuildFlowDirectionFieldJob
            {
                GridWidth = config.GridWidth,
                GridHeight = config.GridHeight,
                IntegrationField = integrationBuffer.AsNativeArray(),
                DirectionField = directionBuffer.AsNativeArray()
            };

            var directionHandle = buildDirectionJob.ScheduleParallel(gridSize, 64, state.Dependency);
            state.Dependency = directionHandle;
        }

        private void InitializeFlowFieldEntity(ref SystemState state, FlowFieldConfig config)
        {
            // Try to find existing flow field entity
            foreach (var (data, entity) in SystemAPI.Query<RefRO<FlowFieldData>>().WithEntityAccess())
            {
                _flowFieldEntity = entity;
                return;
            }

            // Create new flow field entity
            _flowFieldEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(_flowFieldEntity, "FlowFieldEntity");

            var flowFieldData = new FlowFieldData
            {
                GridWidth = config.GridWidth,
                GridHeight = config.GridHeight,
                CellSize = config.CellSize,
                GridOrigin = config.GridOrigin,
                DestinationCell = new int2(0, 0),
                NeedsUpdate = true
            };

            state.EntityManager.AddComponentData(_flowFieldEntity, flowFieldData);
            state.EntityManager.AddBuffer<FlowFieldCostBuffer>(_flowFieldEntity);
            state.EntityManager.AddBuffer<FlowFieldIntegrationBuffer>(_flowFieldEntity);
            state.EntityManager.AddBuffer<FlowFieldDirectionBuffer>(_flowFieldEntity);
        }

        private static int2 WorldToGrid(float3 worldPos, float3 gridOrigin, float cellSize, int gridWidth, int gridHeight)
        {
            int x = (int)math.floor((worldPos.x - gridOrigin.x) / cellSize);
            int y = (int)math.floor((worldPos.z - gridOrigin.z) / cellSize);
            x = math.clamp(x, 0, gridWidth - 1);
            y = math.clamp(y, 0, gridHeight - 1);
            return new int2(x, y);
        }

        /// <summary>
        /// Stage 1: Initialize cost field with obstacles.
        /// Time complexity: O(cells + obstacles)
        /// </summary>
        [BurstCompile]
        private struct BuildCostFieldJob : IJob
        {
            public int GridWidth;
            public int GridHeight;
            public float CellSize;
            public float3 GridOrigin;
            public byte DefaultCost;
            public byte ObstacleCost;

            [ReadOnly] public NativeArray<FlowFieldObstacle> Obstacles;
            [ReadOnly] public NativeArray<LocalTransform> ObstaclePositions;

            public NativeArray<FlowFieldCostBuffer> CostField;

            public void Execute()
            {
                // Initialize all cells to default cost
                for (int i = 0; i < CostField.Length; i++)
                {
                    CostField[i] = new FlowFieldCostBuffer { Value = DefaultCost };
                }

                // Mark obstacle cells
                for (int i = 0; i < Obstacles.Length; i++)
                {
                    var obstacle = Obstacles[i];
                    var position = ObstaclePositions[i].Position;

                    // Convert obstacle position to grid cell
                    int cellX = (int)math.floor((position.x - GridOrigin.x) / CellSize);
                    int cellY = (int)math.floor((position.z - GridOrigin.z) / CellSize);

                    // Mark cells within obstacle radius
                    int radiusCells = (int)math.ceil(obstacle.Radius / CellSize);
                    for (int dy = -radiusCells; dy <= radiusCells; dy++)
                    {
                        for (int dx = -radiusCells; dx <= radiusCells; dx++)
                        {
                            int x = cellX + dx;
                            int y = cellY + dy;

                            if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                            {
                                float3 cellCenter = new float3(
                                    GridOrigin.x + (x + 0.5f) * CellSize,
                                    0,
                                    GridOrigin.z + (y + 0.5f) * CellSize
                                );

                                float distSq = math.distancesq(new float3(position.x, 0, position.z), cellCenter);
                                if (distSq <= obstacle.Radius * obstacle.Radius)
                                {
                                    int index = y * GridWidth + x;
                                    CostField[index] = new FlowFieldCostBuffer { Value = ObstacleCost };
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stage 2: Dijkstra wavefront expansion to calculate distance from each cell to destination.
        /// Time complexity: O(cells) typical, O(cells * log(cells)) worst case
        /// </summary>
        [BurstCompile]
        private struct BuildIntegrationFieldJob : IJob
        {
            public int GridWidth;
            public int GridHeight;
            public int2 DestinationCell;

            [ReadOnly] public NativeArray<FlowFieldCostBuffer> CostField;
            public NativeArray<FlowFieldIntegrationBuffer> IntegrationField;

            private const ushort UNREACHABLE = 65535;

            public void Execute()
            {
                // Initialize all cells to unreachable
                for (int i = 0; i < IntegrationField.Length; i++)
                {
                    IntegrationField[i] = new FlowFieldIntegrationBuffer { Value = UNREACHABLE };
                }

                // Check if destination is valid
                if (DestinationCell.x < 0 || DestinationCell.x >= GridWidth ||
                    DestinationCell.y < 0 || DestinationCell.y >= GridHeight)
                {
                    return;
                }

                int destIndex = DestinationCell.y * GridWidth + DestinationCell.x;

                // Check if destination is an obstacle
                if (CostField[destIndex].Value == 255)
                {
                    return;
                }

                // Set destination to 0
                IntegrationField[destIndex] = new FlowFieldIntegrationBuffer { Value = 0 };

                // Open list for wavefront expansion
                var openList = new NativeList<int>(GridWidth * GridHeight / 4, Allocator.Temp);
                openList.Add(destIndex);

                // 4-directional neighbors (N, E, S, W)
                var neighbors = new NativeArray<int2>(4, Allocator.Temp);
                neighbors[0] = new int2(0, 1);   // North
                neighbors[1] = new int2(1, 0);   // East
                neighbors[2] = new int2(0, -1);  // South
                neighbors[3] = new int2(-1, 0);  // West

                // Dijkstra expansion
                while (openList.Length > 0)
                {
                    // Pop first element (breadth-first)
                    int currentIndex = openList[0];
                    openList.RemoveAt(0);

                    int currentX = currentIndex % GridWidth;
                    int currentY = currentIndex / GridWidth;
                    ushort currentIntegration = IntegrationField[currentIndex].Value;

                    // Check all neighbors
                    for (int i = 0; i < 4; i++)
                    {
                        int neighborX = currentX + neighbors[i].x;
                        int neighborY = currentY + neighbors[i].y;

                        // Bounds check
                        if (neighborX < 0 || neighborX >= GridWidth || neighborY < 0 || neighborY >= GridHeight)
                            continue;

                        int neighborIndex = neighborY * GridWidth + neighborX;
                        byte neighborCost = CostField[neighborIndex].Value;

                        // Skip obstacles
                        if (neighborCost == 255)
                            continue;

                        // Calculate new integration value
                        ushort newIntegration = (ushort)(currentIntegration + neighborCost);

                        // Update if better path found
                        if (newIntegration < IntegrationField[neighborIndex].Value)
                        {
                            IntegrationField[neighborIndex] = new FlowFieldIntegrationBuffer { Value = newIntegration };
                            openList.Add(neighborIndex);
                        }
                    }
                }

                openList.Dispose();
                neighbors.Dispose();
            }
        }

        /// <summary>
        /// Stage 3: Calculate flow directions by following gradient of integration field.
        /// Time complexity: O(cells) - fully parallelizable
        /// </summary>
        [BurstCompile]
        private struct BuildFlowDirectionFieldJob : IJobFor
        {
            public int GridWidth;
            public int GridHeight;

            [ReadOnly] public NativeArray<FlowFieldIntegrationBuffer> IntegrationField;
            public NativeArray<FlowFieldDirectionBuffer> DirectionField;

            private const ushort UNREACHABLE = 65535;

            public void Execute(int index)
            {
                int x = index % GridWidth;
                int y = index / GridWidth;

                ushort currentIntegration = IntegrationField[index].Value;

                // If unreachable, no direction
                if (currentIntegration == UNREACHABLE)
                {
                    DirectionField[index] = new FlowFieldDirectionBuffer { Value = float2.zero };
                    return;
                }

                // Find neighbor with lowest integration value
                ushort lowestIntegration = currentIntegration;
                int2 bestDirection = int2.zero;

                // Check 8 neighbors
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int neighborX = x + dx;
                        int neighborY = y + dy;

                        // Bounds check
                        if (neighborX < 0 || neighborX >= GridWidth || neighborY < 0 || neighborY >= GridHeight)
                            continue;

                        int neighborIndex = neighborY * GridWidth + neighborX;
                        ushort neighborIntegration = IntegrationField[neighborIndex].Value;

                        if (neighborIntegration < lowestIntegration)
                        {
                            lowestIntegration = neighborIntegration;
                            bestDirection = new int2(dx, dy);
                        }
                    }
                }

                // Normalize direction
                float2 direction = math.normalizesafe(new float2(bestDirection.x, bestDirection.y));
                DirectionField[index] = new FlowFieldDirectionBuffer { Value = direction };
            }
        }
    }
}
