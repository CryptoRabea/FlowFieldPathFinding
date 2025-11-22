using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor Window for managing shadow casting in Unity scenes
/// Created by Crazy Rooster Games
/// </summary>
public class ShadowCastingManagerEditor : EditorWindow
{
    // Data storage
    private Dictionary<Renderer, ShadowCastingMode> shadowCasters = new Dictionary<Renderer, ShadowCastingMode>();
    private Vector2 scrollPosition;
    
    // Search settings
    private bool includeInactive = false;
    private bool searchAllScenes = false;
    
    // Filter settings
    private bool showMeshRenderers = true;
    private bool showSkinnedMeshRenderers = true;
    private bool showSpriteRenderers = true;
    private bool showOtherRenderers = true;
    
    // UI state
    private bool showSettings = true;
    private bool showFilters = true;
    private bool showRendererList = true;
    private string searchFilter = "";
    
    // Statistics
    private int totalRenderers = 0;
    private int enabledCount = 0;
    private int disabledCount = 0;
    
    [MenuItem("Tools/Crazy Rooster Games/Shadow Casting Manager")]
    public static void ShowWindow()
    {
        ShadowCastingManagerEditor window = GetWindow<ShadowCastingManagerEditor>("Shadow Manager");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // Header
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Shadow Casting Manager", EditorStyles.boldLabel);
        GUILayout.Label("Manage shadow casting for all renderers in your scene", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // Settings Section
        showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showSettings, "Search Settings");
        if (showSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            includeInactive = EditorGUILayout.Toggle("Include Inactive Objects", includeInactive);
            
            EditorGUI.BeginDisabledGroup(true); // Disabled for now as it's complex with multiple scenes
            searchAllScenes = EditorGUILayout.Toggle("Search All Loaded Scenes", searchAllScenes);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space(5);
        
        // Filter Section
        showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Renderer Type Filters");
        if (showFilters)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showMeshRenderers = EditorGUILayout.Toggle("Mesh Renderers", showMeshRenderers);
            showSkinnedMeshRenderers = EditorGUILayout.Toggle("Skinned Mesh Renderers", showSkinnedMeshRenderers);
            showSpriteRenderers = EditorGUILayout.Toggle("Sprite Renderers", showSpriteRenderers);
            showOtherRenderers = EditorGUILayout.Toggle("Other Renderers", showOtherRenderers);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space(5);
        
        // Action Buttons
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Find Shadow Casters", GUILayout.Height(30)))
        {
            FindAndStoreShadowCasters();
        }
        
        if (GUILayout.Button("Refresh", GUILayout.Height(30)))
        {
            FindAndStoreShadowCasters();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        EditorGUI.BeginDisabledGroup(shadowCasters.Count == 0);
        if (GUILayout.Button("Disable All Shadows", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Disable All Shadows", 
                $"Are you sure you want to disable shadow casting on {shadowCasters.Count} renderers?", 
                "Yes", "Cancel"))
            {
                DisableAllShadowCasting();
            }
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;
        
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        EditorGUI.BeginDisabledGroup(shadowCasters.Count == 0);
        if (GUILayout.Button("Enable All Shadows", GUILayout.Height(25)))
        {
            EnableAllShadowCasting();
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(shadowCasters.Count == 0);
        if (GUILayout.Button("Clear Stored Data", GUILayout.Height(20)))
        {
            if (EditorUtility.DisplayDialog("Clear Data", 
                "This will clear all stored shadow caster data. Continue?", 
                "Yes", "Cancel"))
            {
                ClearStoredData();
            }
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // Statistics
        if (shadowCasters.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Total Stored:", shadowCasters.Count.ToString());
            EditorGUILayout.LabelField("Currently Enabled:", enabledCount.ToString());
            EditorGUILayout.LabelField("Currently Disabled:", disabledCount.ToString());
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
        }
        
        // Renderer List
        if (shadowCasters.Count > 0)
        {
            showRendererList = EditorGUILayout.BeginFoldoutHeaderGroup(showRendererList, $"Stored Renderers ({shadowCasters.Count})");
            if (showRendererList)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Search filter
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Search:", GUILayout.Width(50));
                searchFilter = EditorGUILayout.TextField(searchFilter);
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    searchFilter = "";
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // List header
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("Renderer", EditorStyles.toolbarButton, GUILayout.Width(200));
                GUILayout.Label("Type", EditorStyles.toolbarButton, GUILayout.Width(100));
                GUILayout.Label("Original", EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.Label("Current", EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.Label("Actions", EditorStyles.toolbarButton);
                EditorGUILayout.EndHorizontal();
                
                // Scrollable list
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));
                
                var filteredRenderers = GetFilteredRenderers();
                
                foreach (var kvp in filteredRenderers)
                {
                    if (kvp.Key == null) continue;
                    
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    // Object name and selection
                    if (GUILayout.Button(kvp.Key.gameObject.name, EditorStyles.label, GUILayout.Width(200)))
                    {
                        Selection.activeGameObject = kvp.Key.gameObject;
                        EditorGUIUtility.PingObject(kvp.Key.gameObject);
                    }
                    
                    // Renderer type
                    GUILayout.Label(GetRendererTypeName(kvp.Key), EditorStyles.miniLabel, GUILayout.Width(100));
                    
                    // Original mode
                    GUILayout.Label(kvp.Value.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                    
                    // Current mode with color
                    GUI.color = kvp.Key.shadowCastingMode == ShadowCastingMode.Off ? Color.red : Color.green;
                    GUILayout.Label(kvp.Key.shadowCastingMode.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                    GUI.color = Color.white;
                    
                    // Individual control buttons
                    if (kvp.Key.shadowCastingMode == ShadowCastingMode.Off)
                    {
                        if (GUILayout.Button("Enable", GUILayout.Width(60)))
                        {
                            EnableShadowCasting(kvp.Key);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Disable", GUILayout.Width(60)))
                        {
                            DisableShadowCasting(kvp.Key);
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                if (filteredRenderers.Count == 0 && shadowCasters.Count > 0)
                {
                    EditorGUILayout.HelpBox("No renderers match the current filters.", MessageType.Info);
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        else
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("No shadow casters found. Click 'Find Shadow Casters' to scan the scene.", MessageType.Info);
        }
        
        EditorGUILayout.Space(10);
        
        // Footer
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("© Crazy Rooster Games", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }
    
    private void FindAndStoreShadowCasters()
    {
        shadowCasters.Clear();
        
        Renderer[] allRenderers = includeInactive 
            ? Resources.FindObjectsOfTypeAll<Renderer>()
            : FindObjectsOfType<Renderer>();
        
        totalRenderers = 0;
        
        foreach (Renderer renderer in allRenderers)
        {
            // Skip renderers from prefabs and assets
            if (EditorUtility.IsPersistent(renderer.transform.root.gameObject))
                continue;
            
            // Skip renderers from preview scenes
            if (renderer.gameObject.scene.name == null)
                continue;
            
            // Only store renderers that have shadow casting enabled
            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                shadowCasters[renderer] = renderer.shadowCastingMode;
                totalRenderers++;
            }
        }
        
        UpdateStatistics();
        
        Debug.Log($"[Shadow Manager] Found {shadowCasters.Count} shadow casting renderers");
        Repaint();
    }
    
    private void DisableAllShadowCasting()
    {
        Undo.RecordObjects(shadowCasters.Keys.ToArray(), "Disable All Shadow Casting");
        
        int disabledCount = 0;
        
        foreach (var kvp in shadowCasters.ToList())
        {
            if (kvp.Key != null)
            {
                kvp.Key.shadowCastingMode = ShadowCastingMode.Off;
                disabledCount++;
                EditorUtility.SetDirty(kvp.Key);
            }
        }
        
        UpdateStatistics();
        
        Debug.Log($"[Shadow Manager] Disabled shadow casting on {disabledCount} renderers");
        Repaint();
    }
    
    private void EnableAllShadowCasting()
    {
        Undo.RecordObjects(shadowCasters.Keys.ToArray(), "Enable All Shadow Casting");
        
        int enabledCount = 0;
        
        foreach (var kvp in shadowCasters.ToList())
        {
            if (kvp.Key != null)
            {
                kvp.Key.shadowCastingMode = kvp.Value;
                enabledCount++;
                EditorUtility.SetDirty(kvp.Key);
            }
        }
        
        UpdateStatistics();
        
        Debug.Log($"[Shadow Manager] Restored shadow casting on {enabledCount} renderers");
        Repaint();
    }
    
    private void DisableShadowCasting(Renderer renderer)
    {
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Disable Shadow Casting");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(renderer);
            UpdateStatistics();
            Repaint();
        }
    }
    
    private void EnableShadowCasting(Renderer renderer)
    {
        if (renderer != null && shadowCasters.ContainsKey(renderer))
        {
            Undo.RecordObject(renderer, "Enable Shadow Casting");
            renderer.shadowCastingMode = shadowCasters[renderer];
            EditorUtility.SetDirty(renderer);
            UpdateStatistics();
            Repaint();
        }
    }
    
    private void ClearStoredData()
    {
        shadowCasters.Clear();
        UpdateStatistics();
        Debug.Log("[Shadow Manager] Cleared all stored shadow caster data");
        Repaint();
    }
    
    private void UpdateStatistics()
    {
        enabledCount = 0;
        disabledCount = 0;
        
        foreach (var kvp in shadowCasters)
        {
            if (kvp.Key != null)
            {
                if (kvp.Key.shadowCastingMode == ShadowCastingMode.Off)
                    disabledCount++;
                else
                    enabledCount++;
            }
        }
    }
    
    private List<KeyValuePair<Renderer, ShadowCastingMode>> GetFilteredRenderers()
    {
        var filtered = shadowCasters.Where(kvp =>
        {
            if (kvp.Key == null) return false;
            
            // Type filter
            bool typeMatch = false;
            if (kvp.Key is MeshRenderer && showMeshRenderers) typeMatch = true;
            if (kvp.Key is SkinnedMeshRenderer && showSkinnedMeshRenderers) typeMatch = true;
            if (kvp.Key is SpriteRenderer && showSpriteRenderers) typeMatch = true;
            if (!(kvp.Key is MeshRenderer) && !(kvp.Key is SkinnedMeshRenderer) && 
                !(kvp.Key is SpriteRenderer) && showOtherRenderers) typeMatch = true;
            
            if (!typeMatch) return false;
            
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                return kvp.Key.gameObject.name.ToLower().Contains(searchFilter.ToLower());
            }
            
            return true;
        }).ToList();
        
        return filtered;
    }
    
    private string GetRendererTypeName(Renderer renderer)
    {
        if (renderer is MeshRenderer) return "Mesh";
        if (renderer is SkinnedMeshRenderer) return "Skinned";
        if (renderer is SpriteRenderer) return "Sprite";
        if (renderer is ParticleSystemRenderer) return "Particle";
        if (renderer is TrailRenderer) return "Trail";
        if (renderer is LineRenderer) return "Line";
        return "Other";
    }
    
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    
    private void OnSceneGUI(SceneView sceneView)
    {
        // Optional: Add scene view visualization here if needed
    }
}
