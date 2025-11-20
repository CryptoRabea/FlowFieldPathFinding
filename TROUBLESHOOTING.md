# Troubleshooting Guide - Common Errors and Solutions

## Quick Diagnostics

**If agents don't spawn:**
→ Jump to Section 1

**If agents spawn but don't move:**
→ Jump to Section 2

**If performance is terrible (<30 FPS with 1k agents):**
→ Jump to Section 3

**If you see compilation errors:**
→ Jump to Section 4

**If you see runtime exceptions:**
→ Jump to Section 5

---

## Section 1: Agents Don't Spawn

### 1.1 No Agents Visible in Scene

**Symptom:** Press Play, GUI shows "0 active agents"

**Checklist:**
- [ ] `FlowFieldBootstrap` component attached to GameObject in scene
- [ ] `Initial Spawn Count` > 0 in Inspector
- [ ] `Pool Size` > `Initial Spawn Count`
- [ ] Agent prefab assigned in `Agent Prefab` field
- [ ] Agent prefab has `AgentRenderingAuthoring` component

**Fix 1: Verify Bootstrap Setup**
```
1. Select FlowFieldManager GameObject
2. Inspector → FlowFieldBootstrap
3. Check "Initial Spawn Count" = 5000 (or >0)
4. Check "Pool Size" = 20000 (or > spawn count)
```

**Fix 2: Check Prefab**
```
1. Open agent prefab in Project window
2. Verify it has AgentRenderingAuthoring component
3. Verify Mesh and Material are assigned
4. If missing, add AgentRenderingAuthoring and configure
```

**Fix 3: Check Entity Query**
Open Window → Entities → Systems, verify:
- `AgentSpawnerSystem` exists and is running
- `AgentSpawnerState` singleton created (check Entities → Hierarchy)

---

### 1.2 Agents Spawn But Are Invisible

**Symptom:** GUI shows "5000 active agents" but nothing visible

**Cause 1: Rendering Not Setup**

**Fix:**
```
1. Open Frame Debugger (Window → Analysis → Frame Debugger)
2. Enable and step through frames
3. Look for "Draw Mesh" calls with agent mesh
4. If missing → rendering components not added
```

Verify agent prefab has:
- MeshFilter (or mesh in AgentRenderingAuthoring)
- Material compatible with render pipeline (URP/HDRP/Built-in)

**Cause 2: Agents Spawned Off-Screen**

**Fix:**
```
1. In Scene view during Play mode, zoom out
2. Check if agents are at y=-1000 (pooled inactive position)
3. If yes → spawner not enabling agents properly
```

Check console for errors during spawning.

**Cause 3: Material Not Visible**

**Fix:**
```
1. Check agent material uses correct render pipeline shader:
   - Built-in: Standard
   - URP: Universal Render Pipeline/Lit
   - HDRP: HDRP/Lit
2. Check material color is not black (0,0,0)
3. Verify lighting exists (Directional Light in scene)
```

---

### 1.3 "Pool Size Exceeded" Warning

**Symptom:** Console shows: `Cannot spawn 10000 agents: pool full`

**Cause:** Trying to spawn more agents than pre-allocated pool

**Fix:**
```
1. Increase Pool Size in FlowFieldBootstrap Inspector
   - Example: Pool Size = 50000 (for up to 50k agents)
2. Restart Play mode (pool allocated at startup)
```

**Note:** Pool size determines memory usage (~100 bytes/agent)

---

## Section 2: Agents Don't Move

### 2.1 Agents Spawn But Stay Still

**Symptom:** Agents visible, but frozen in spawn positions

**Cause 1: Flow Field Not Generated**

**Check:**
```
Window → Entities → Hierarchy
Look for entity with FlowFieldDirectionBuffer component
```

**Fix:**
```
1. Verify FlowFieldConfigAuthoring exists in scene, OR
2. FlowFieldBootstrap creates FlowFieldConfig singleton
3. Check console for flow field generation errors
```

**Cause 2: Target Not Set**

**Fix:**
```
1. In FlowFieldBootstrap, check Target Position is within grid bounds
2. Grid bounds: [gridOrigin, gridOrigin + gridSize * cellSize]
3. Example: Grid at (-100, 0, -100), size 100x100, cell 2.0
   Valid target: (-100 to 100, 0 to 100)
   Invalid: (500, 0, 500) <- outside grid
```

**Cause 3: Movement System Not Running**

**Check:**
```
Window → Entities → Systems
Verify "AgentMovementSystem" is listed and enabled
```

If missing, check for compilation errors.

---

### 2.2 Agents Move Randomly, Not Toward Target

**Symptom:** Agents wander aimlessly instead of pathfinding

**Cause:** Flow field directions invalid or not sampling correctly

**Debug:**
```
1. Enable "Show Flow Field" in FlowFieldBootstrap Inspector
2. In Scene view, flow vectors should point toward target (red sphere)
3. If vectors are random → flow field generation broken
```

**Fix:**
```
1. Check destination cell is valid:
   - In FlowFieldGenerationSystem, log destCell value
   - Should be within [0, gridWidth-1] and [0, gridHeight-1]
2. Verify integration field has values:
   - Add debug log in BuildIntegrationFieldJob
   - Check IntegrationBuffer[destIndex].Value == 0 (destination)
```

---

### 2.3 Agents Jitter/Vibrate in Place

**Symptom:** Agents shake or teleport erratically

**Cause 1: Avoidance Too Strong**

**Fix:**
```
In Agent component (or AgentRenderingAuthoring):
- Reduce AvoidanceWeight from 0.5 to 0.2
- Or increase FlowFollowWeight from 1.0 to 2.0
```

**Cause 2: Time.deltaTime Issues**

**Check:**
```
1. In Profiler, verify Time.deltaTime is stable (~0.016s @ 60 FPS)
2. If spiking wildly → VSync or frame pacing issue
```

**Fix:**
```
Edit → Project Settings → Quality
V-Sync Count: Don't Sync (for testing)
```

**Cause 3: Spatial Hash Collisions**

**Fix:**
```
In AgentMovementSystem:
- Increase SpatialCellSize from 2.0 to 3.0
- This reduces hash collisions
```

---

## Section 3: Performance Issues

### 3.1 Low FPS (<30) with 1000 Agents

**Symptom:** Even small agent counts struggle

**Cause 1: Burst Not Enabled**

**Fix:**
```
1. Edit → Project Settings → Player → Other Settings
2. Allow 'unsafe' Code: ✓ ENABLED
3. Scripting Backend: IL2CPP (not Mono)
4. Restart Unity (Burst needs recompile)
```

**Cause 2: Safety Checks Enabled**

**Fix:**
```
1. Jobs → Burst → Burst AOT Settings
2. Enable Burst Compilation: ✓
3. Safety Checks: Off (for release)
4. Optimizations: Force On
```

**Cause 3: Deep Profiling Enabled**

**Fix:**
```
Window → Analysis → Profiler
Uncheck "Deep Profile" (causes 10-100x slowdown)
```

**Cause 4: Editor Overhead**

**Test:**
```
1. Make Development Build
2. Run standalone (not in Editor)
3. If much faster → Editor overhead (normal)
4. Editor is 2-3x slower than builds
```

---

### 3.2 GC Allocations Causing Stutters

**Symptom:** Periodic frame drops every few seconds, red spikes in Profiler

**Diagnosis:**
```
1. Window → Analysis → Profiler → Memory module
2. Look at "GC Alloc in Frame" (should be 0)
3. If >0, click spike → see call stack
```

**Common Causes:**
- `Debug.Log()` in update loops
- LINQ queries (`Where`, `Select`)
- `foreach` on managed collections
- Temporary `List<T>` / `Array` creation

**Fix:**
```csharp
// BAD:
Debug.Log($"Agent count: {count}"); // Allocates string

// GOOD:
// Remove Debug.Log from hot paths

// BAD:
var filtered = agents.Where(a => a.active).ToList();

// GOOD:
// Use NativeArray or EntityQuery
```

---

### 3.3 Jobs Not Parallelizing

**Symptom:** CPU usage low (~25% on 4-core), jobs run sequentially

**Diagnosis:**
```
Window → Analysis → Profiler → Jobs module
Blue bars should overlap (parallel), not stack (sequential)
```

**Cause 1: Using `Schedule()` Instead of `ScheduleParallel()`**

**Fix:**
```csharp
// BAD:
state.Dependency = job.Schedule(state.Dependency);

// GOOD:
state.Dependency = job.ScheduleParallel(state.Dependency);
```

**Cause 2: Job Dependencies Too Strict**

**Fix:**
```csharp
// If jobs are independent, don't chain dependencies:

// BAD:
var dep1 = jobA.Schedule(state.Dependency);
var dep2 = jobB.Schedule(dep1); // Waits for jobA

// GOOD (if independent):
var dep1 = jobA.Schedule(state.Dependency);
var dep2 = jobB.Schedule(state.Dependency); // Parallel!
state.Dependency = JobHandle.CombineDependencies(dep1, dep2);
```

---

### 3.4 Rendering Bottleneck (GPU-Bound)

**Symptom:** FPS increases when looking away from agents

**Diagnosis:**
```
Window → Analysis → Frame Debugger
Check SetPass calls and draw calls
```

**Target:** <10 SetPass, <100 draw calls for 10k agents

**If High:**
- **SetPass >50:** GPU instancing broken → check SRP Batcher enabled
- **Draw calls >1000:** No instancing → verify RenderMeshUtility.AddComponents

**Fix:**
```
1. Edit → Project Settings → Graphics
2. If URP: Select UniversalRenderPipelineAsset
3. SRP Batcher: ✓ ENABLED
4. GPU Instancing: ✓ ENABLED
```

---

## Section 4: Compilation Errors

### 4.1 "The name 'SystemAPI' does not exist"

**Cause:** Using Entities <1.0 (SystemAPI introduced in 1.0)

**Fix:**
```
1. Window → Package Manager
2. Find "Entities" package
3. Update to 1.4.3 or newer
```

**Alternative (if stuck on old version):**
Replace `SystemAPI` with manual `EntityQuery`:
```csharp
// Old (Entities <1.0):
var query = GetEntityQuery(typeof(Agent));
var agents = query.ToComponentDataArray<Agent>(Allocator.Temp);

// New (Entities 1.0+):
foreach (var agent in SystemAPI.Query<RefRO<Agent>>()) { }
```

---

### 4.2 "Unsafe code may only appear if compiling with /unsafe"

**Cause:** Burst requires unsafe code for pointers/NativeArrays

**Fix:**
```
1. Edit → Project Settings → Player → Other Settings
2. Scroll to "Allow 'unsafe' Code"
3. ✓ ENABLE
4. Restart Unity
```

---

### 4.3 "The type or namespace 'Unity.Burst' could not be found"

**Cause:** Burst package not installed

**Fix:**
```
1. Window → Package Manager
2. Search "Burst"
3. Install "Burst" (1.8.25 or newer)
```

Or manually add to `Packages/manifest.json`:
```json
"com.unity.burst": "1.8.25"
```

---

### 4.4 "IJobEntity does not exist"

**Cause:** Using Entities <1.0

**Fix:**
Update to Entities 1.0+ (see 4.1) OR replace with `IJobChunk`:

```csharp
// Modern (Entities 1.0+):
partial struct MyJob : IJobEntity
{
    void Execute(ref AgentVelocity velocity) { }
}

// Legacy (Entities 0.x):
struct MyJob : IJobChunk
{
    public ComponentTypeHandle<AgentVelocity> VelocityHandle;
    void Execute(ArchetypeChunk chunk, ...) { }
}
```

---

## Section 5: Runtime Exceptions

### 5.1 "NativeArray has not been allocated or has been deallocated"

**Cause:** Accessing disposed NativeArray or uninitialized buffer

**Common locations:**
- Flow field buffers not created yet
- Spatial hash accessed after disposal

**Fix:**
```csharp
// Add safety check:
if (!directionBuffer.IsCreated || directionBuffer.Length == 0)
    return;
```

**Prevention:**
Ensure buffers allocated before systems use them:
- Flow field buffers created in `FlowFieldGenerationSystem.OnUpdate`
- Check `RequireForUpdate<FlowFieldData>()` in dependent systems

---

### 5.2 "InvalidOperationException: JobHandle cannot be completed twice"

**Cause:** Calling `.Complete()` on same JobHandle multiple times

**Fix:**
```csharp
// BAD:
var handle = job.Schedule(state.Dependency);
handle.Complete();
state.Dependency = handle;
// ... later ...
state.Dependency.Complete(); // ERROR!

// GOOD:
state.Dependency = job.Schedule(state.Dependency);
// Don't manually Complete, let system handle it
```

**Rule:** Don't call `.Complete()` on jobs unless necessary. Let Unity's dependency system handle it.

---

### 5.3 "IndexOutOfRangeException in flow field sampling"

**Cause:** Agent's cell index outside buffer bounds

**Fix:**
```csharp
// In movement job, add bounds check:
if (cellIndex.Value < 0 || cellIndex.Value >= DirectionBuffer.Length)
{
    // Agent outside grid, use default behavior
    return;
}
```

**Prevention:**
Ensure grid covers spawn area:
- Grid origin/size should include all agent positions
- Or despawn agents that leave grid

---

### 5.4 "ArgumentException: The NativeArray can not be Disposed because it was not allocated with a valid allocator"

**Cause:** Trying to dispose a NativeArray allocated with `Allocator.Temp` from wrong scope

**Fix:**
```csharp
// Temp allocations auto-dispose, don't manually dispose:

// BAD:
var array = new NativeArray<int>(10, Allocator.Temp);
array.Dispose(); // Can cause errors in some contexts

// GOOD:
var array = new NativeArray<int>(10, Allocator.Temp);
// Don't dispose, auto-disposed at end of frame
```

For persistent data, use `Allocator.TempJob` or `Allocator.Persistent` and dispose in job `OnDestroy`.

---

### 5.5 "ObjectDisposedException: NativeMultiHashMap has been deallocated"

**Cause:** Spatial hash disposed before jobs complete

**Fix:**
In `AgentMovementSystem.OnUpdate`:
```csharp
// Ensure disposal happens AFTER jobs:
state.Dependency = spatialHash.Dispose(state.Dependency);

// NOT:
spatialHash.Dispose(); // Immediate disposal, jobs still using it!
```

---

## Section 6: Burst-Specific Issues

### 6.1 "Burst error BC1091: Cannot call managed method from Burst"

**Cause:** Trying to call non-Bursted method from Burst job

**Fix:**
Remove managed calls:
```csharp
// BAD (in Burst job):
Debug.Log("Hello"); // Managed method
var obj = new MyClass(); // Managed allocation
var list = new List<int>(); // Managed collection

// GOOD:
// Remove logging from jobs
// Use NativeArray instead of List
```

---

### 6.2 "Burst error BC1071: Unsupported type"

**Cause:** Using `class` or managed reference in Burst job

**Fix:**
```csharp
// BAD:
struct MyJob : IJob
{
    public Transform transform; // Managed UnityEngine.Object
}

// GOOD:
struct MyJob : IJob
{
    public float3 position; // Unmanaged value type
}
```

**Allowed in Burst:**
- `struct` (value types)
- `NativeArray`, `NativeList`, etc.
- `float`, `int`, `bool`, `float3`, etc.

**Not allowed:**
- `class` instances
- `UnityEngine.Object` (GameObject, Transform, etc.)
- `string`

---

## Section 7: Quick Fixes Summary

| **Problem** | **Quick Fix** |
|-------------|--------------|
| Agents don't spawn | Check Pool Size > Spawn Count, verify prefab assigned |
| Agents invisible | Check material/mesh, verify render pipeline shader |
| Agents don't move | Enable "Show Flow Field", verify target in grid bounds |
| Low FPS | Enable Burst, IL2CPP, disable Safety Checks |
| GC stutters | Remove Debug.Log, avoid LINQ, use NativeArrays |
| Jobs not parallel | Use ScheduleParallel(), check dependencies |
| "Unsafe code" error | Enable "Allow unsafe Code" in Player Settings |
| "NativeArray disposed" | Add `.IsCreated` checks, dispose in correct order |

---

## Still Having Issues?

### Debug Workflow:
1. **Check Console** for errors/warnings (often shows root cause)
2. **Open Profiler** to identify slow systems
3. **Open Entities Hierarchy** to verify entities created
4. **Enable Debug Visualization** (flow field, grid) to see pathfinding
5. **Test with 100 agents first** to isolate from performance issues

### Enable Debug Logging:
Add to systems:
```csharp
#if UNITY_EDITOR
UnityEngine.Debug.Log($"AgentMovementSystem: Processing {agentCount} agents");
#endif
```

### Contact/Resources:
- **Unity DOTS Documentation:** https://docs.unity3d.com/Packages/com.unity.entities@latest
- **Unity Forums:** https://forum.unity.com/forums/dots/
- **Check package versions:** Window → Package Manager

---

**Most issues are resolved by:**
1. Enabling Burst and unsafe code
2. Verifying prefab/component setup
3. Checking grid bounds vs. spawn/target positions

Happy debugging! 🐛
