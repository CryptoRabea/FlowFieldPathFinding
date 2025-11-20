using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Rendering;           // ← critical for RenderMeshUnmanaged & MaterialMeshInfo
using UnityEngine;

public struct UnitTag : IComponentData { }

public struct MoveSpeed : IComponentData
{
    public float Value;
}

public struct FlowFieldFollower : IComponentData { }