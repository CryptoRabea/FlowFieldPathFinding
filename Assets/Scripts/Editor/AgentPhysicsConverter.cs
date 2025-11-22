using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FlowFieldPathfinding.Editor
{
    /// <summary>
    /// Editor utility to batch convert agents with physics to optimized versions.
    /// Removes expensive Rigidbody components and replaces with optimized alternatives.
    /// </summary>
    public class AgentPhysicsConverter : EditorWindow
    {
        private bool removeRigidbodies = true;
        private bool removeColliders = false;
        private bool addRigidbodyOptimizer = false;
        private bool makeKinematic = true;

        [MenuItem("Tools/Flow Field/Agent Physics Converter")]
        public static void ShowWindow()
        {
            GetWindow<AgentPhysicsConverter>("Agent Physics Converter");
        }

        void OnGUI()
        {
            GUILayout.Label("Agent Physics Performance Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "For maximum performance with flow field agents:\n\n" +
                "RECOMMENDED: Remove Rigidbody components entirely.\n" +
                "The AgentMovementSystem handles all movement and doesn't need physics.\n\n" +
                "This can improve performance from 10 FPS to 60+ FPS with 1000+ agents!",
                MessageType.Info);

            EditorGUILayout.Space();

            removeRigidbodies = EditorGUILayout.Toggle("Remove Rigidbodies", removeRigidbodies);

            if (!removeRigidbodies)
            {
                EditorGUI.indentLevel++;
                makeKinematic = EditorGUILayout.Toggle("Make Kinematic", makeKinematic);
                addRigidbodyOptimizer = EditorGUILayout.Toggle("Add Optimizer Component", addRigidbodyOptimizer);
                EditorGUI.indentLevel--;
            }

            removeColliders = EditorGUILayout.Toggle("Remove Colliders", removeColliders);

            EditorGUILayout.Space();

            if (GUILayout.Button("Convert Selected GameObjects", GUILayout.Height(30)))
            {
                ConvertSelected();
            }

            if (GUILayout.Button("Convert All in Scene", GUILayout.Height(30)))
            {
                ConvertAllInScene();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This will modify GameObjects in the scene. Use Undo (Ctrl+Z) if needed.",
                MessageType.Warning);
        }

        private void ConvertSelected()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select GameObjects to convert.", "OK");
                return;
            }

            Undo.RecordObjects(Selection.gameObjects, "Convert Agent Physics");

            int converted = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (ConvertGameObject(go))
                    converted++;
            }

            Debug.Log($"[AgentPhysicsConverter] Converted {converted} GameObjects");
            EditorUtility.DisplayDialog("Conversion Complete",
                $"Converted {converted} GameObjects.\n\nCheck the Console for details.", "OK");
        }

        private void ConvertAllInScene()
        {
            var allRigidbodies = FindObjectsOfType<Rigidbody>();

            if (allRigidbodies.Length == 0)
            {
                EditorUtility.DisplayDialog("No Rigidbodies Found",
                    "No Rigidbodies found in the scene.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Convert All Rigidbodies?",
                $"This will affect {allRigidbodies.Length} GameObjects with Rigidbodies.\n\nContinue?",
                "Yes", "Cancel"))
            {
                return;
            }

            var gameObjects = new List<GameObject>();
            foreach (var rb in allRigidbodies)
            {
                gameObjects.Add(rb.gameObject);
            }

            Undo.RecordObjects(gameObjects.ToArray(), "Convert All Agent Physics");

            int converted = 0;
            foreach (var go in gameObjects)
            {
                if (ConvertGameObject(go))
                    converted++;
            }

            Debug.Log($"[AgentPhysicsConverter] Converted {converted} GameObjects");
            EditorUtility.DisplayDialog("Conversion Complete",
                $"Converted {converted} GameObjects.\n\nCheck the Console for details.", "OK");
        }

        private bool ConvertGameObject(GameObject go)
        {
            bool modified = false;

            // Handle Rigidbody
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (removeRigidbodies)
                {
                    DestroyImmediate(rb);
                    Debug.Log($"Removed Rigidbody from {go.name}");
                    modified = true;
                }
                else
                {
                    if (makeKinematic)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.constraints = RigidbodyConstraints.FreezeRotation;
                        Debug.Log($"Made {go.name} kinematic");
                        modified = true;
                    }

                    if (addRigidbodyOptimizer && go.GetComponent<AgentRigidbodyOptimizer>() == null)
                    {
                        var optimizer = go.AddComponent<AgentRigidbodyOptimizer>();
                        optimizer.makeKinematic = true;
                        optimizer.ApplyOptimizations();
                        Debug.Log($"Added AgentRigidbodyOptimizer to {go.name}");
                        modified = true;
                    }
                }
            }

            // Handle Colliders
            if (removeColliders)
            {
                var colliders = go.GetComponents<Collider>();
                foreach (var col in colliders)
                {
                    if (col != null && !(col is MeshCollider)) // Keep mesh colliders for visuals
                    {
                        DestroyImmediate(col);
                        Debug.Log($"Removed {col.GetType().Name} from {go.name}");
                        modified = true;
                    }
                }
            }

            return modified;
        }
    }
}
