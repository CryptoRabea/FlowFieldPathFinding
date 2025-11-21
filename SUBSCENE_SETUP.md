# SubScene Setup Guide

This guide explains how to set up the SubScene for the Flow Field Pathfinding system.

## What is a SubScene?

A SubScene is a Unity DOTS container that bakes GameObjects into ECS entities. All authoring components must be inside a SubScene to be converted to entities.

## SubScene Contents

Your SubScene should contain **4 types of GameObjects**:

### 1. FlowFieldConfig (Required)
- **Create:** Empty GameObject named `FlowFieldConfig`
- **Add Component:** `FlowFieldConfigAuthoring`
- **Settings:**
  - Grid Width: `100`
  - Grid Height: `100`
  - Cell Size: `2.0`
  - Grid Origin: `(-100, 0, -100)`
  - Target Position: `(50, 0, 50)`

### 2. AgentSpawnerConfig (Required)
- **Create:** Empty GameObject named `AgentSpawnerConfig`
- **Add Component:** `AgentSpawnerConfigAuthoring`
- **Settings:**
  - **Agent Prefab:** Drag in the Agent prefab (see below)
  - Pool Size: `20000`
  - Initial Spawn Count: `5000`
  - Spawn Center: `(0, 0, 0)`
  - Spawn Radius: `20`

### 3. Agent Prefab (Required)
- **Create:** GameObject with a visible mesh (Cube/Sphere)
- **Add Component:** `AgentRenderingAuthoring`
- **Requirements:**
  - Must have `MeshFilter` with a mesh assigned
  - Must have `MeshRenderer` with a material
- **Save as Prefab:** Drag to Project folder
- **Assign:** Reference this prefab in `AgentSpawnerConfigAuthoring`

### 4. Obstacles (Optional)
- **Create:** Any GameObject positioned within the grid
- **Add Component:** `FlowFieldObstacleAuthoring`
- **Settings:**
  - Radius: Size of impassable area around obstacle

## Hierarchy Example

```
SubScene
├── FlowFieldConfig          (FlowFieldConfigAuthoring)
├── AgentSpawnerConfig       (AgentSpawnerConfigAuthoring)
├── Obstacle1                (FlowFieldObstacleAuthoring)
└── Obstacle2                (FlowFieldObstacleAuthoring)

Project/Prefabs
└── AgentPrefab              (AgentRenderingAuthoring + MeshFilter + MeshRenderer)
```

## Creating the SubScene

1. **Create SubScene:** Right-click in Hierarchy > New Sub Scene > Empty Scene
2. **Name it:** `FlowFieldSubScene`
3. **Add GameObjects:** Create the required objects inside the SubScene
4. **Assign Prefab:** Make sure `AgentSpawnerConfigAuthoring.agentPrefab` references your agent prefab

## Common Mistakes

| Problem | Solution |
|---------|----------|
| "Agent component not found" | Ensure agent prefab has `AgentRenderingAuthoring` |
| No agents visible | Check prefab has `MeshFilter` + `MeshRenderer` |
| Agents don't move | Verify `FlowFieldConfigAuthoring` exists |
| "Singleton not found" | Config GameObjects must be inside SubScene |

## Verification

1. Enter Play mode
2. Open **Window > Entities > Hierarchy**
3. You should see:
   - `FlowFieldConfig` singleton entity
   - `AgentSpawnerConfig` singleton entity
   - `FlowFieldTarget` singleton entity
   - Thousands of agent entities (pooled)
