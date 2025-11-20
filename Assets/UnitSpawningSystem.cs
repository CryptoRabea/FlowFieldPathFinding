using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Rendering;           // ← critical for RenderMeshUnmanaged & MaterialMeshInfo
using UnityEngine;

[BurstCompile]
public partial struct UnitSpawningSystem : ISystem
{
    private Entity prefab;
    private bool initialized;
    private int spawned;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitPrefab>(); // waits for singleton
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (spawned >= 20000) return;

        if (!initialized)
        {
            prefab = SystemAPI.GetSingleton<UnitPrefab>().Value;
            initialized = true;
        }

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        int batch = math.min(100, 20000 - spawned);
        for (int i = 0; i < batch; i++)
        {
            var unit = ecb.Instantiate(prefab);

            float3 pos = new float3(
                UnityEngine.Random.Range(-200f, 200f), 0,
                UnityEngine.Random.Range(-200f, 200f));

            ecb.SetComponent(unit, LocalTransform.FromPosition(pos));
        }

        spawned += batch;
    }
}