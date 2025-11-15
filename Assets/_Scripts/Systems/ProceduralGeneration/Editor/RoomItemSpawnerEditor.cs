using _Scripts.Systems.ProceduralGeneration.Items;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(RoomItemSpawner))]
    public class RoomItemSpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _spawnOnAwake;
        private SerializedProperty _spawnDelay;
        private SerializedProperty _showDebugLogs;

        private void OnEnable()
        {
            _spawnOnAwake = serializedObject.FindProperty("_spawnOnAwake");
            _spawnDelay = serializedObject.FindProperty("_spawnDelay");
            _showDebugLogs = serializedObject.FindProperty("_showDebugLogs");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RoomItemSpawner spawner = (RoomItemSpawner)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Room Item Spawner", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // === SETTINGS ===
            DrawSettings();
            EditorGUILayout.Space(10);

            // === STATISTICS ===
            DrawStatistics(spawner);
            EditorGUILayout.Space(10);

            // === BATCH OPERATIONS ===
            DrawBatchOperations(spawner);
            EditorGUILayout.Space(10);

            // === DEBUG ===
            DrawDebug();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_spawnOnAwake, new GUIContent("Spawn On Awake"));
            
            if (_spawnOnAwake.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_spawnDelay, new GUIContent("Spawn Delay (seconds)"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawStatistics(RoomItemSpawner spawner)
        {
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            if (spawner.SpawnPoints != null)
            {
                EditorGUILayout.LabelField($"Spawn Points: {spawner.SpawnPoints.Count}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Items Spawned: {spawner.TotalItemsSpawned}");

                if (spawner.SpawnPoints.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No spawn points found in this room.\n" +
                        "Add ItemSpawnPoint components to child objects.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Spawn points not initialized. Enter Play mode to see statistics.", MessageType.None);
            }
        }

        private void DrawBatchOperations(RoomItemSpawner spawner)
        {
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Spawn all button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Spawn All Items", GUILayout.Height(35)))
            {
                if (Application.isPlaying)
                {
                    spawner.SpawnAllItems();
                }
                else
                {
                    Debug.LogWarning("[RoomItemSpawner] Item spawning only works in Play mode!");
                }
            }

            // Clear all button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear All Items", GUILayout.Height(35)))
            {
                spawner.ClearAllSpawnedItems();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Refresh spawn points button
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Refresh Spawn Points", GUILayout.Height(25)))
            {
                spawner.RefreshSpawnPoints();
                Debug.Log($"[RoomItemSpawner] Refreshed spawn points: {spawner.SpawnPoints?.Count ?? 0} found.");
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawDebug()
        {
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showDebugLogs, new GUIContent("Show Debug Logs"));
        }
    }
}