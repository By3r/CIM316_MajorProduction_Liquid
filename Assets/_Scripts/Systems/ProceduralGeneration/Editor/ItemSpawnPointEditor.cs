using _Scripts.Systems.ProceduralGeneration.Items;
using UnityEngine;
using UnityEditor;

namespace _Scripts.ProceduralGeneration.ItemSpawning.Editor
{
    [CustomEditor(typeof(ItemSpawnPoint))]
    public class ItemSpawnPointEditor : UnityEditor.Editor
    {
        private SerializedProperty _maxGroundCheckDistance;
        private SerializedProperty _groundLayerMask;

        private void OnEnable()
        {
            _maxGroundCheckDistance = serializedObject.FindProperty("_maxGroundCheckDistance");
            _groundLayerMask = serializedObject.FindProperty("_groundLayerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ItemSpawnPoint spawnPoint = (ItemSpawnPoint)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            DrawTestingTools(spawnPoint);
            EditorGUILayout.Space(10);

            DrawGroundSnapTool(spawnPoint);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTestingTools(ItemSpawnPoint spawnPoint)
        {
            EditorGUILayout.LabelField("Testing Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

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
            EditorGUILayout.LabelField("Manual Ground Snap Tool", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Select GameObjects in hierarchy, then click button to snap using bottom collider for positioning.",
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

            Bounds bounds = itemCollider.bounds;
            Vector3 bottomPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            float maxDistance = _maxGroundCheckDistance.floatValue;
            LayerMask groundMask = _groundLayerMask.intValue;

            RaycastHit hit;
            if (Physics.Raycast(bottomPoint, Vector3.down, out hit, maxDistance, groundMask))
            {
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