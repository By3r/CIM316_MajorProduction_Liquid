using UnityEngine;
using UnityEditor;

namespace _Scripts.ProceduralGeneration.ItemSpawning.Editor
{
    [CustomEditor(typeof(ItemSpawnPoint))]
    public class ItemSpawnPointEditor : UnityEditor.Editor
    {
        private SerializedProperty _spawnableItems;
        private SerializedProperty _guaranteedSpawn;
        private SerializedProperty _useFallbackItem;
        private SerializedProperty _fallbackItemPrefab;
        private SerializedProperty _snapToGround;
        private SerializedProperty _maxGroundCheckDistance;
        private SerializedProperty _groundLayerMask;
        private SerializedProperty _spawnOffset;
        private SerializedProperty _showDebugLogs;

        private void OnEnable()
        {
            _spawnableItems = serializedObject.FindProperty("_spawnableItems");
            _guaranteedSpawn = serializedObject.FindProperty("_guaranteedSpawn");
            _useFallbackItem = serializedObject.FindProperty("_useFallbackItem");
            _fallbackItemPrefab = serializedObject.FindProperty("_fallbackItemPrefab");
            _snapToGround = serializedObject.FindProperty("_snapToGround");
            _maxGroundCheckDistance = serializedObject.FindProperty("_maxGroundCheckDistance");
            _groundLayerMask = serializedObject.FindProperty("_groundLayerMask");
            _spawnOffset = serializedObject.FindProperty("_spawnOffset");
            _showDebugLogs = serializedObject.FindProperty("_showDebugLogs");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ItemSpawnPoint spawnPoint = (ItemSpawnPoint)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Spawn Point", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);

            // === SPAWN SETTINGS ===
            DrawSpawnSettings();
            EditorGUILayout.Space(10);

            // === SPAWNABLE ITEMS LIST ===
            DrawSpawnableItemsList();
            EditorGUILayout.Space(10);

            // === POSITIONING SETTINGS ===
            DrawPositioningSettings();
            EditorGUILayout.Space(10);

            // === SPAWN OFFSET ===
            EditorGUILayout.LabelField("Spawn Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spawnOffset, new GUIContent("Offset"));
            EditorGUILayout.Space(10);

            // === TESTING TOOLS ===
            DrawTestingTools(spawnPoint);
            EditorGUILayout.Space(10);

            // === GROUND SNAP TOOL ===
            DrawGroundSnapTool(spawnPoint);
            EditorGUILayout.Space(10);

            // === DEBUG ===
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showDebugLogs, new GUIContent("Show Debug Logs"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSpawnSettings()
        {
            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_guaranteedSpawn, new GUIContent("Guaranteed Spawn"));
            
            if (_guaranteedSpawn.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_useFallbackItem, new GUIContent("Use Fallback Item"));
                
                if (_useFallbackItem.boolValue)
                {
                    EditorGUILayout.PropertyField(_fallbackItemPrefab, new GUIContent("Fallback Prefab"));
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSpawnableItemsList()
        {
            EditorGUILayout.LabelField("Spawnable Items", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_spawnableItems, new GUIContent("Items"), true);

            if (_spawnableItems.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No spawnable items configured.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"Items: {_spawnableItems.arraySize}", EditorStyles.miniLabel);
            }
        }

        private void DrawPositioningSettings()
        {
            EditorGUILayout.LabelField("Positioning Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_snapToGround, new GUIContent("Snap to Ground (Once at Spawn)"));

            if (_snapToGround.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_maxGroundCheckDistance, new GUIContent("Max Ground Distance"));
                EditorGUILayout.PropertyField(_groundLayerMask, new GUIContent("Ground Layer Mask"));
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox(
                    "Raycast happens ONCE when item spawns - no continuous updates.",
                    MessageType.None);
            }
        }

        private void DrawTestingTools(ItemSpawnPoint spawnPoint)
        {
            EditorGUILayout.LabelField("Testing Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Test spawn button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Test Spawn Item", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    spawnPoint.SpawnItem();
                }
                else
                {
                    Debug.LogWarning("[ItemSpawnPoint] Test spawning only works in Play mode!");
                }
            }

            // Clear spawned item button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Spawned", GUILayout.Height(30)))
            {
                spawnPoint.ClearSpawnedItem();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroundSnapTool(ItemSpawnPoint spawnPoint)
        {
            EditorGUILayout.LabelField("Manual Ground Snap Tool (Edit Mode)", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Use to manually snap items in Edit Mode:\n" +
                "• Select GameObjects in hierarchy\n" +
                "• Click button below\n" +
                "• Uses bottom collider for positioning",
                MessageType.Info);

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Snap Selected Items to Ground", GUILayout.Height(35)))
            {
                SnapSelectedItemsToGround(spawnPoint);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Snap Spawned Item", GUILayout.Height(25)))
            {
                if (spawnPoint.HasSpawnedItem)
                {
                    SnapItemToGround(spawnPoint.SpawnedItem, spawnPoint);
                }
                else
                {
                    Debug.LogWarning("[ItemSpawnPoint] No item spawned to snap!");
                }
            }

            if (GUILayout.Button("Snap All Children", GUILayout.Height(25)))
            {
                SnapAllChildrenToGround(spawnPoint);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void SnapSelectedItemsToGround(ItemSpawnPoint spawnPoint)
        {
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("[ItemSpawnPoint] No items selected in hierarchy!");
                return;
            }

            int snappedCount = 0;

            foreach (GameObject obj in selectedObjects)
            {
                if (SnapItemToGround(obj, spawnPoint))
                {
                    snappedCount++;
                }
            }

            Debug.Log($"[ItemSpawnPoint] Snapped {snappedCount}/{selectedObjects.Length} items to ground.");
        }

        private void SnapAllChildrenToGround(ItemSpawnPoint spawnPoint)
        {
            Transform[] children = spawnPoint.GetComponentsInChildren<Transform>();
            int snappedCount = 0;

            foreach (Transform child in children)
            {
                if (child == spawnPoint.transform)
                    continue;

                if (SnapItemToGround(child.gameObject, spawnPoint))
                {
                    snappedCount++;
                }
            }

            Debug.Log($"[ItemSpawnPoint] Snapped {snappedCount} child items to ground.");
        }

        private bool SnapItemToGround(GameObject item, ItemSpawnPoint spawnPoint)
        {
            Collider itemCollider = item.GetComponent<Collider>();

            if (itemCollider == null)
            {
                Debug.LogWarning($"[ItemSpawnPoint] '{item.name}' has no collider - cannot snap!");
                return false;
            }

            // Get the bottom point of the collider
            Bounds bounds = itemCollider.bounds;
            Vector3 bottomPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // Get settings from spawn point
            float maxDistance = _maxGroundCheckDistance.floatValue;
            LayerMask groundMask = _groundLayerMask.intValue;

            // Raycast down from the bottom point
            RaycastHit hit;
            if (Physics.Raycast(bottomPoint, Vector3.down, out hit, maxDistance, groundMask))
            {
                // Calculate offset needed to place bottom on ground
                float distanceToGround = bottomPoint.y - hit.point.y;
                
                Undo.RecordObject(item.transform, "Snap Item to Ground");
                item.transform.position += Vector3.down * distanceToGround;
                
                EditorUtility.SetDirty(item);

                Debug.Log($"[ItemSpawnPoint] Snapped '{item.name}' to ground at {hit.point}");
                return true;
            }
            else
            {
                Debug.LogWarning($"[ItemSpawnPoint] No ground found below '{item.name}' within {maxDistance}m");
                return false;
            }
        }
    }
}