# Character Animation Setup Guide

This guide explains how to set up skinned mesh character animations for the Flow Field Pathfinding system.

## Overview

The system now supports velocity-based animations with three states:
- **Idle**: Plays when the agent is stationary (velocity < threshold)
- **Walk**: Plays when the agent is moving (velocity ≥ threshold)
- **Attack**: Plays when the agent is attacking (requires AgentAttack component)

## Quick Setup

### 1. Prepare Your Character Prefab

Your character prefab must have:
- **SkinnedMeshRenderer** component with your character mesh
- **Animator** component with an Animator Controller configured
- All required ECS authoring components (see below)

### 2. Configure the Animator Controller

Create or configure your Animator Controller with these states:

1. **Idle** state (default state)
   - Name: "Idle" (or customize in inspector)
   - Animation clip: Your idle animation

2. **Walk** state
   - Name: "Walk" (or customize in inspector)
   - Animation clip: Your walk/run animation

3. **Attack** state (optional)
   - Name: "Attack" (or customize in inspector)
   - Animation clip: Your attack animation

**IMPORTANT:** The state names in the Animator Controller must match the names you enter in the SkinnedMeshAnimationAuthoring component (see step 3).

### 3. Add Required Components to Your Character Prefab

Add these components to your character prefab GameObject:

#### A. SkinnedMeshAnimationAuthoring
- **Animator**: Assign your Animator component (auto-detected if on same GameObject)
- **Idle Animation State**: Name of idle state in Animator Controller (default: "Idle")
- **Walk Animation State**: Name of walk state in Animator Controller (default: "Walk")
- **Attack Animation State**: Name of attack state in Animator Controller (default: "Attack")
- **Animation Speed**: Animation playback speed multiplier (default: 1.0)
- **Walk Speed Threshold**: Minimum velocity to trigger walk animation (default: 0.1)

#### B. Agent Components (from FlowFieldPathfinding namespace)
You need the standard agent components. Your prefab should have authoring scripts that bake these:
- **Agent**: Movement speed and behavior weights
- **AgentVelocity**: Current velocity (initialized by spawner)
- **AgentCellIndex**: Grid cell index (initialized by spawner)
- **AgentActive**: Marks the agent as active

If you don't have authoring scripts, you can create a simple one:

```csharp
using Unity.Entities;
using UnityEngine;
using FlowFieldPathfinding;

public class AgentAuthoring : MonoBehaviour
{
    public float speed = 5f;
    public float avoidanceWeight = 1f;
    public float flowFollowWeight = 1f;
    public float cohesionWeight = 0.5f;

    class Baker : Baker<AgentAuthoring>
    {
        public override void Bake(AgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Agent
            {
                Speed = authoring.speed,
                AvoidanceWeight = authoring.avoidanceWeight,
                FlowFollowWeight = authoring.flowFollowWeight,
                CohesionWeight = authoring.cohesionWeight
            });

            AddComponent(entity, new AgentVelocity { Value = Unity.Mathematics.float3.zero });
            AddComponent(entity, new AgentCellIndex { Value = -1 });
            AddComponent(entity, new AgentActive());
        }
    }
}
```

#### C. (Optional) AgentAttack Component
To enable attack animations, you can add the AgentAttack component programmatically:

```csharp
// Example: Trigger an attack
var attack = new AgentAttack
{
    IsAttacking = true,
    AttackTimer = 1.0f,  // Duration in seconds
    AttackDuration = 1.0f
};
EntityManager.AddComponentData(entity, attack);
```

### 4. Configure Your Spawner

Make sure your AgentSpawnerConfigAuthoring references your character prefab:
- **Agent Prefab**: Drag your configured character prefab here

## How It Works

### Animation State Logic

The system automatically switches animations based on:

1. **Attack Priority** (highest)
   - If entity has AgentAttack component AND IsAttacking == true AND AttackTimer > 0
   - Plays Attack animation
   - Timer automatically decrements each frame
   - Returns to Idle/Walk when timer expires

2. **Movement-Based** (normal)
   - Calculates horizontal velocity magnitude (XZ plane)
   - If velocity > Walk Speed Threshold: Plays Walk animation
   - If velocity ≤ Walk Speed Threshold: Plays Idle animation

### System Update Order

1. **AgentAttackSystem** (SimulationSystemGroup)
   - Updates attack timers
   - Disables IsAttacking when timer expires

2. **AgentMovementSystem** (SimulationSystemGroup)
   - Calculates agent velocities based on flow field
   - Updates positions and rotations

3. **SkinnedMeshAnimationSystem** (PresentationSystemGroup)
   - Syncs GameObject transforms with entity transforms
   - Determines animation state based on velocity and attack state
   - Plays appropriate animation

## Troubleshooting

### Character appears in T-pose

**Possible causes:**
1. **Animator Controller not assigned**: Make sure the Animator component has a valid Animator Controller assigned
2. **Animation state names don't match**: Verify that the state names in SkinnedMeshAnimationAuthoring match your Animator Controller states exactly (case-sensitive)
3. **No animations in Animator Controller**: Ensure your Animator Controller has animation clips assigned to each state
4. **Animator not enabled**: Check that the Animator component is enabled

**Fix:**
- Double-click your Animator Controller to open the Animator window
- Verify all states exist: Idle, Walk, Attack (or whatever names you configured)
- Make sure each state has an animation clip assigned
- Check that state names match EXACTLY in the SkinnedMeshAnimationAuthoring inspector

### Multiple prefabs appearing in hierarchy

This is expected behavior. Each spawned agent gets its own GameObject instance for animation:
- One prefab in your project/scene (the template)
- Multiple runtime instances created when agents are spawned

The prefab template should not be active in the scene. Only spawned instances should be active.

### Animation plays but character doesn't move

**Possible causes:**
1. **Missing Agent component**: Character needs Agent component with appropriate Speed value
2. **Flow field not generated**: Make sure flow field is being generated and target is set
3. **AgentActive component disabled**: Verify the AgentActive component is enabled

**Fix:**
- Check that your prefab has all required components (Agent, AgentVelocity, AgentCellIndex, AgentActive)
- Verify Agent.Speed is > 0
- Check Unity console for flow field generation logs

### Walk animation plays when standing still (or vice versa)

**Possible cause:**
Walk Speed Threshold is set incorrectly

**Fix:**
- Adjust the "Walk Speed Threshold" in SkinnedMeshAnimationAuthoring
- Lower value = easier to trigger walk animation
- Higher value = character needs to move faster to trigger walk
- Recommended range: 0.05 - 0.5

### Attack animation doesn't play

**Possible causes:**
1. **No AgentAttack component**: Attack animation only plays if entity has AgentAttack component
2. **Attack timer not set**: AttackTimer must be > 0
3. **Animation state name mismatch**: "Attack" state name doesn't match Animator Controller

**Fix:**
- Add AgentAttack component to entity programmatically when you want to trigger attack
- Set IsAttacking = true and AttackTimer to animation duration
- Verify "Attack" state exists in Animator Controller with correct name

## Example: Triggering an Attack

```csharp
using Unity.Entities;
using FlowFieldPathfinding;

public partial class ExampleAttackTriggerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Example: Make first agent attack
        bool attacked = false;
        foreach (var (agent, entity) in
            SystemAPI.Query<RefRO<Agent>>()
            .WithAll<AgentActive>()
            .WithEntityAccess())
        {
            if (!attacked && !EntityManager.HasComponent<AgentAttack>(entity))
            {
                // Add attack component to trigger attack animation
                ecb.AddComponent(entity, new AgentAttack
                {
                    IsAttacking = true,
                    AttackTimer = 1.5f,  // 1.5 second attack
                    AttackDuration = 1.5f
                });
                attacked = true;
                UnityEngine.Debug.Log("Triggered attack animation!");
            }
            break;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
```

## Performance Notes

- The animation system runs on the main thread (cannot be Burst compiled) due to Animator being a managed component
- For maximum performance with many agents, consider:
  - Using LOD (Level of Detail) to reduce animation updates for distant agents
  - Limiting the number of simultaneously animated characters
  - Using simpler animations for distant agents

## Additional Resources

- Unity DOTS documentation: https://docs.unity3d.com/Packages/com.unity.entities@latest
- Unity Animator documentation: https://docs.unity3d.com/Manual/class-AnimatorController.html
