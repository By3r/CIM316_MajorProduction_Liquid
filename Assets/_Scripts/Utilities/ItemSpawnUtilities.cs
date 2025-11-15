using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace _Scripts.ProceduralGeneration.ItemSpawning.Editor
{
    /// <summary>
    /// Utility window for batch operations on ItemSpawnPoints.
    /// Access via Tools > LIQUID > Item Spawn Utilities
    /// </summary>
    public class ItemSpawnUtilities : EditorWindow
    {
        private bool _enableGizmos = false;
        private GameObject _targetRoom;

        [MenuItem("Tools/LIQUID/Item Spawn Utilities")]
        public static void ShowWindow()
        {
            var window = GetWindow<ItemSpawnUtilities>("Spawn Utilities");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Spawn Point Utilities", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Batch operations for ItemSpawnPoints.\n" +
                "Useful for rooms with many spawn points.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // === TARGET SELECTION ===
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            _targetRoom = (GameObject)EditorGUILayout.ObjectField(
                "Room Prefab/Object", 
                _targetRoom, 
                typeof(GameObject), 
                true);

            if (_targetRoom == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a room GameObject to perform batch operations on its spawn points.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.Space(10);

            // === GIZMO OPERATIONS ===
            EditorGUILayout.LabelField("Gizmo Operations", EditorStyles.boldLabel);
            
            ItemSpawnPoint[] spawnPoints = _targetRoom.GetComponentsInChildren<ItemSpawnPoint>(true);
            EditorGUILayout.LabelField($"Spawn Points Found: {spawnPoints.Length}", EditorStyles.miniLabel);

            if (spawnPoints.Length == 0)
            {
                EditorGUILayout.HelpBox("No ItemSpawnPoints found in this object.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Enable All Gizmos", GUILayout.Height(30)))
            {
                SetGizmosOnAll(spawnPoints, true);
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Disable All Gizmos", GUILayout.Height(30)))
            {
                SetGizmosOnAll(spawnPoints, false);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "💡 Disable gizmos if you have 20+ spawn points for better editor performance.",
                MessageType.None);

            EditorGUILayout.Space(10);

            // === GROUND SNAP OPERATIONS ===
            EditorGUILayout.LabelField("Ground Snap Operations", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Snap All Spawn Points' Children to Ground", GUILayout.Height(35)))
            {
                SnapAllSpawnPointsChildren(spawnPoints);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Snaps all child objects of all spawn points to ground.\n" +
                "Useful for batch positioning items in Edit mode.",
                MessageType.None);

            EditorGUILayout.Space(10);

            // === STATISTICS ===
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            
            int gizmosEnabled = 0;
            int totalChildren = 0;
            
            foreach (var sp in spawnPoints)
            {
                SerializedObject so = new SerializedObject(sp);
                SerializedProperty showGizmosProp = so.FindProperty("_showGizmos");
                
                if (showGizmosProp != null && showGizmosProp.boolValue)
                {
                    gizmosEnabled++;
                }

                totalChildren += sp.transform.childCount;
            }

            EditorGUILayout.LabelField($"Spawn Points: {spawnPoints.Length}");
            EditorGUILayout.LabelField($"Gizmos Enabled: {gizmosEnabled}/{spawnPoints.Length}");
            EditorGUILayout.LabelField($"Total Child Objects: {totalChildren}");
        }

        private void SetGizmosOnAll(ItemSpawnPoint[] spawnPoints, bool enabled)
        {
            int updatedCount = 0;

            try
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                        enabled ? "Enabling Gizmos" : "Disabling Gizmos",
                        $"Processing spawn point {i + 1}/{spawnPoints.Length}",
                        (float)i / spawnPoints.Length))
                    {
                        break;
                    }

                    SerializedObject so = new SerializedObject(spawnPoints[i]);
                    SerializedProperty showGizmosProp = so.FindProperty("_showGizmos");
                    
                    if (showGizmosProp != null)
                    {
                        showGizmosProp.boolValue = enabled;
                        so.ApplyModifiedProperties();
                        updatedCount++;
                    }

                    EditorUtility.SetDirty(spawnPoints[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[ItemSpawnUtilities] {(enabled ? "Enabled" : "Disabled")} gizmos on {updatedCount}/{spawnPoints.Length} spawn points.");
        }

        private void SnapAllSpawnPointsChildren(ItemSpawnPoint[] spawnPoints)
        {
            int totalSnapped = 0;
            int totalProcessed = 0;

            try
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    ItemSpawnPoint sp = spawnPoints[i];

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Snapping Items to Ground",
                        $"Processing spawn point {i + 1}/{spawnPoints.Length}: {sp.name}",
                        (float)i / spawnPoints.Length))
                    {
                        break;
                    }

                    foreach (Transform child in sp.transform)
                    {
                        totalProcessed++;
                        
                        if (SnapItemToGround(child.gameObject, sp))
                        {
                            totalSnapped++;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[ItemSpawnUtilities] Snapped {totalSnapped}/{totalProcessed} items to ground across {spawnPoints.Length} spawn points.");
        }

        private bool SnapItemToGround(GameObject item, ItemSpawnPoint spawnPoint)
        {
            Collider itemCollider = item.GetComponent<Collider>();

            if (itemCollider == null)
            {
                return false;
            }

            // Get settings from spawn point
            SerializedObject so = new SerializedObject(spawnPoint);
            float maxDistance = so.FindProperty("_maxGroundCheckDistance").floatValue;
            LayerMask groundMask = so.FindProperty("_groundLayerMask").intValue;

            // Get the bottom point of the collider
            Bounds bounds = itemCollider.bounds;
            Vector3 bottomPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // Raycast down
            RaycastHit hit;
            if (Physics.Raycast(bottomPoint, Vector3.down, out hit, maxDistance, groundMask))
            {
                float distanceToGround = bottomPoint.y - hit.point.y;
                
                Undo.RecordObject(item.transform, "Snap Item to Ground");
                item.transform.position += Vector3.down * distanceToGround;
                
                EditorUtility.SetDirty(item);
                return true;
            }

            return false;
        }
    }
}