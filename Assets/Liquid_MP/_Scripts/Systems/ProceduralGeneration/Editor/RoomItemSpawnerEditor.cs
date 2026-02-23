using UnityEngine;
using UnityEditor;
using _Scripts.ProceduralGeneration.ItemSpawning;
using System.Collections.Generic;

namespace _Scripts.Systems.ProceduralGeneration.Items.Editor
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

            EditorGUILayout.PropertyField(_spawnOnAwake, new GUIContent("Spawn On Awake"));
            EditorGUILayout.PropertyField(_spawnDelay, new GUIContent("Spawn Delay (seconds)"));

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            if (Application.isPlaying && spawner.SpawnPoints != null)
            {
                EditorGUILayout.LabelField($"Spawn Points: {spawner.SpawnPoints.Count}");
                EditorGUILayout.LabelField($"Items Spawned: {spawner.TotalItemsSpawned}");
            }
            else
            {
                int spawnPointCount = spawner.GetComponentsInChildren<ItemSpawnPoint>(true).Length;
                EditorGUILayout.LabelField($"Spawn Points: {spawnPointCount}");
                
                if (!Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Items Spawned: N/A (Runtime only)");
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Batch operations only available in Play Mode.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("Spawn All Items", GUILayout.Height(30)))
                {
                    spawner.SpawnAllItems();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUILayout.Button("Clear All Items", GUILayout.Height(30)))
                {
                    spawner.ClearAllSpawnedItems();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(0.3f, 0.6f, 0.8f);
                if (GUILayout.Button("Refresh Spawn Points", GUILayout.Height(30)))
                {
                    spawner.RefreshSpawnPoints();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.PropertyField(_showDebugLogs, new GUIContent("Show Debug Logs"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}