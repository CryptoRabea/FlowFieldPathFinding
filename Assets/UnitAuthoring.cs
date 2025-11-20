using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Rendering;           // ← critical for RenderMeshUnmanaged & MaterialMeshInfo
using UnityEngine;

public struct UnitPrefab : IComponentData
{
    public Entity Value;
}

public class UnitPrefabAuthoring : MonoBehaviour
{
    public GameObject prefab; // drag your unit prefab here (converted or not)
}

public class UnitPrefabBaker : Baker<UnitPrefabAuthoring>
{
    public override void Bake(UnitPrefabAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new UnitPrefab
        {
            Value = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic)
        });
    }
}