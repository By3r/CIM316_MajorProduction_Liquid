using _Scripts.Core.Managers;
using _Scripts.Systems.ProceduralGeneration;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace _Scripts.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(FloorGenerator))]
    public class FloorGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FloorGenerator generator = (FloorGenerator)target;

            // FloorStateManager Status (only in Play mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                DrawFloorStateManagerStatus(generator);
                
                // Stats (when available)
                ShowGenerationStats(generator);
            }
        }

        private void DrawFloorStateManagerStatus(FloorGenerator generator)
        {
            bool managerExists = FloorStateManager.Instance != null;
            bool isInitialized = managerExists && FloorStateManager.Instance.IsInitialized;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("FloorStateManager Status", EditorStyles.boldLabel);

            if (!managerExists)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("FloorStateManager not in scene", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!isInitialized)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("FloorStateManager not initialized", EditorStyles.miniLabel);
                GUI.color = Color.white;
                EditorGUILayout.HelpBox("Enable 'Auto Initialize On Awake' in FloorStateManager.", MessageType.Warning);
            }
            else
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Ready", EditorStyles.miniLabel);
                GUI.color = Color.white;
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"World Seed: {FloorStateManager.Instance.WorldSeed}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Current Floor: {FloorStateManager.Instance.CurrentFloorNumber}", EditorStyles.miniLabel);
                
                if (generator.CurrentSeed != 0)
                {
                    EditorGUILayout.LabelField($"Last Generated Seed: {generator.CurrentSeed}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowGenerationStats(FloorGenerator generator)
        {
            var spawnedRoomsField = typeof(FloorGenerator).GetField("_spawnedRooms", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var connectionsMadeField = typeof(FloorGenerator).GetField("_connectionsMade", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var blockadeSpawnCountField = typeof(FloorGenerator).GetField("_blockadeSpawnCount", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var creditsRemainingField = typeof(FloorGenerator).GetField("_creditsRemaining", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (spawnedRoomsField != null && connectionsMadeField != null)
            {
                var spawnedRooms = spawnedRoomsField.GetValue(generator) as System.Collections.Generic.List<GameObject>;
                int connectionsMade = (int)connectionsMadeField.GetValue(generator);
                int blockadeSpawnCount = blockadeSpawnCountField != null ? (int)blockadeSpawnCountField.GetValue(generator) : 0;
                int creditsRemaining = (int)creditsRemainingField.GetValue(generator);

                if (spawnedRooms != null && spawnedRooms.Count > 0)
                {
                    EditorGUILayout.LabelField("Last Generation", EditorStyles.boldLabel);
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Rooms: {spawnedRooms.Count}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Connections: {connectionsMade}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Blockades: {blockadeSpawnCount}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Credits Remaining: {creditsRemaining}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}
#endif