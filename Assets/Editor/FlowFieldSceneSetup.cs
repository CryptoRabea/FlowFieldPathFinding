using UnityEngine;
using UnityEditor;
using FlowFieldPathfinding;

/// <summary>
/// Automatic scene setup for Flow Field Pathfinding.
/// Menu: Tools → Flow Field → Setup Scene
/// </summary>
public class FlowFieldSceneSetup : EditorWindow
{
    [MenuItem("Tools/Flow Field/Setup Scene (One Click)")]
    public static void SetupScene()
    {
        // Check if components already exist
        var existingConfig = FindObjectOfType<FlowFieldConfigAuthoring>();
        var existingSpawner = FindObjectOfType<AgentSpawnerConfigAuthoring>();

        if (existingConfig != null || existingSpawner != null)
        {
            if (!EditorUtility.DisplayDialog("Setup Scene",
                "Flow Field components already exist in scene. Replace them?",
                "Yes, Replace", "Cancel"))
            {
                return;
            }

            if (existingConfig != null) DestroyImmediate(existingConfig.gameObject);
            if (existingSpawner != null) DestroyImmediate(existingSpawner.gameObject);
        }

        // Create Flow Field Config
        GameObject configGO = new GameObject("FlowFieldConfig");
        var configAuth = configGO.AddComponent<FlowFieldConfigAuthoring>();
        configAuth.gridWidth = 100;
        configAuth.gridHeight = 100;
        configAuth.cellSize = 2.0f;
        configAuth.gridOrigin = new Vector3(-100, 0, -100);
        configAuth.targetPosition = new Vector3(50, 0, 50);
        configAuth.obstacleCost = 255;
        configAuth.defaultCost = 1;
        configAuth.directionSmoothFactor = 0.5f;

        // Create Agent Spawner Config
        GameObject spawnerGO = new GameObject("AgentSpawnerConfig");
        var spawnerAuth = spawnerGO.AddComponent<AgentSpawnerConfigAuthoring>();
        spawnerAuth.poolSize = 20000;
        spawnerAuth.initialSpawnCount = 5000;
        spawnerAuth.spawnCenter = Vector3.zero;
        spawnerAuth.spawnRadius = 20f;
        spawnerAuth.defaultSpeed = 5f;
        spawnerAuth.defaultAvoidanceWeight = 0.5f;
        spawnerAuth.defaultFlowFollowWeight = 1.0f;

        // Create Runtime Controller (optional)
        GameObject managerGO = new GameObject("FlowFieldManager");
        var bootstrap = managerGO.AddComponent<FlowFieldBootstrap>();
        bootstrap.targetPosition = new Vector3(50, 0, 50);
        bootstrap.spawnCount = 1000;
        bootstrap.showFlowField = false;

        // Create Ground Plane
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(50, 1, 50);

        // Setup Camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 100, -100);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);
        }

        // Create Directional Light
        GameObject lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Select the config for user visibility
        Selection.activeGameObject = configGO;

        EditorUtility.DisplayDialog("Success!",
            "Scene setup complete!\n\n" +
            "Created:\n" +
            "- FlowFieldConfig (grid settings)\n" +
            "- AgentSpawnerConfig (spawner settings)\n" +
            "- FlowFieldManager (runtime control)\n" +
            "- Ground plane\n" +
            "- Camera positioned\n" +
            "- Directional Light\n\n" +
            "Press PLAY to spawn 5000 agents!",
            "OK");

        Debug.Log("[FlowFieldSceneSetup] Scene setup complete! Press Play to test.");
    }

    [MenuItem("Tools/Flow Field/Add Flow Field Config Only")]
    public static void AddConfigOnly()
    {
        GameObject configGO = new GameObject("FlowFieldConfig");
        var configAuth = configGO.AddComponent<FlowFieldConfigAuthoring>();
        configAuth.gridWidth = 100;
        configAuth.gridHeight = 100;
        configAuth.cellSize = 2.0f;
        configAuth.gridOrigin = new Vector3(-100, 0, -100);
        configAuth.targetPosition = new Vector3(50, 0, 50);
        configAuth.obstacleCost = 255;
        configAuth.defaultCost = 1;
        configAuth.directionSmoothFactor = 0.5f;

        Selection.activeGameObject = configGO;
        Debug.Log("[FlowFieldSceneSetup] Added FlowFieldConfig");
    }

    [MenuItem("Tools/Flow Field/Add Agent Spawner Config Only")]
    public static void AddSpawnerOnly()
    {
        GameObject spawnerGO = new GameObject("AgentSpawnerConfig");
        var spawnerAuth = spawnerGO.AddComponent<AgentSpawnerConfigAuthoring>();
        spawnerAuth.poolSize = 20000;
        spawnerAuth.initialSpawnCount = 5000;
        spawnerAuth.spawnCenter = Vector3.zero;
        spawnerAuth.spawnRadius = 20f;
        spawnerAuth.defaultSpeed = 5f;
        spawnerAuth.defaultAvoidanceWeight = 0.5f;
        spawnerAuth.defaultFlowFollowWeight = 1.0f;

        Selection.activeGameObject = spawnerGO;
        Debug.Log("[FlowFieldSceneSetup] Added AgentSpawnerConfig");
    }
}
