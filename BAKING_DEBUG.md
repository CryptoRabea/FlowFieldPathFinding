# Baking Stuck? Debug Steps

If initialization is taking more than a few seconds, follow these steps:

## Step 1: Verify Authoring Components in Scene

1. **Open your scene**
2. **Check Hierarchy** for these GameObjects:
   - GameObject with `FlowFieldConfigAuthoring` component
   - GameObject with `AgentSpawnerConfigAuthoring` component

3. **Verify they are ENABLED:**
   - GameObject checkbox should be checked (not grayed out)
   - Component should not be disabled

## Step 2: Check Console for Errors

Look for these messages in Console:
- `[FlowFieldBootstrap] Waiting for baking... Total entities in world: X`
- `[FlowFieldBootstrap] FlowFieldTarget not found`

**If you see "Total entities: 0":**
- Baking system is not running at all
- Unity might be in safe mode or ECS packages not loaded

## Step 3: Force Baking Manually

1. **Window → Entities → Baking**
2. Click **"Force Rebake"** or **"Bake All"**
3. Check if errors appear in console

## Step 4: Verify Components Are Correct

Select the GameObject with `FlowFieldConfigAuthoring`:
- Inspector should show the component with all fields
- Make sure it's not a missing script (shows "Script" instead of "FlowFieldConfigAuthoring")

## Step 5: Common Issues

### Issue: "Missing Script" in Inspector
**Solution:**
```
1. Remove the missing component
2. Add FlowFieldConfigAuthoring again
3. Configure all fields
```

### Issue: GameObject is Disabled
**Solution:**
```
Enable the GameObject in Hierarchy (check the checkbox)
```

### Issue: Baking Window Shows Errors
**Solution:**
```
Read the error message - often points to:
- Missing references
- Compilation errors
- Package version conflicts
```

## Step 6: Restart Unity

If still stuck:
```
1. Stop Play mode
2. File → Save Project
3. Close Unity
4. Delete Library folder (forces full reimport)
5. Reopen Unity
6. Wait for compilation to finish
7. Press Play
```

## Step 7: Check Package Versions

Window → Package Manager:
- Entities: 1.4.3+
- Burst: 1.8.25+
- Collections: 2.6.3+

If version mismatches exist, update packages.

## Step 8: Create Fresh Scene

If nothing works, try minimal setup:
```
1. File → New Scene → Basic
2. Create GameObject "FlowFieldConfig"
3. Add FlowFieldConfigAuthoring component
4. Set grid parameters:
   - Grid Width: 100
   - Grid Height: 100
   - Cell Size: 2.0
   - Grid Origin: (-100, 0, -100)
   - Target Position: (50, 0, 50)
5. Create GameObject "AgentSpawnerConfig"
6. Add AgentSpawnerConfigAuthoring component
7. Set spawner parameters:
   - Pool Size: 1000 (start small!)
   - Initial Spawn Count: 100
   - Spawn Center: (0, 0, 0)
   - Spawn Radius: 10
8. Press Play
```

## Debug Output Analysis

When you run with the debug code, you'll see:

**Every 2 seconds:**
```
[FlowFieldBootstrap] Waiting for baking... Total entities in world: X
  - Entity: [entity names]
```

**What to look for:**
- If X = 0: No entities baked at all → Check Step 1
- If X > 0 but no FlowFieldTarget: Baker not running → Check component names
- If entities listed but wrong types: Wrong authoring components

## Still Stuck?

Post the console output showing:
1. The entity count message
2. The list of entities
3. Any errors or warnings

This will tell us exactly what's wrong!
