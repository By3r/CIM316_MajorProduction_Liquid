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

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generation Controls", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate Floor with Database", GUILayout.Height(40)))
            {
                generator.GenerateFloor();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("DELETE/CLEAR FLOORS", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear Floor?", 
                        "This will destroy all generated rooms. Continue?", 
                        "Yes", "Cancel"))
                {
                    generator.ClearFloor();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Guide", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "SETUP:\n" +
                "1. Add room prefabs to the Room Database (Assets/Data)\n" +
                "2. Click 'Refresh All Rooms' in the database\n" +
                "3. Assign the database to this generator\n" +
                "4. (Optional) Add blockade prefabs to seal failed connections\n\n" +
                "GENERATION:\n" +
                "5. Click 'Generate Floor with Database' to create a floor\n" +
                "6. Each generation randomly selects compatible rooms for branching layouts!\n\n" +
                "FEATURES:\n" +
                "Random socket selection creates maze-like dungeons\n" +
                "Blockade system seals failed connection sockets\n" +
                "Two-phase collision detection prevents overlaps",
                MessageType.Info);

            EditorGUILayout.Space(5);
            ShowGenerationStats(generator);
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
                    EditorGUILayout.LabelField("Last Generation Stats", EditorStyles.boldLabel);
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Rooms Spawned: {spawnedRooms.Count}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Connections Made: {connectionsMade}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Blockades Spawned: {blockadeSpawnCount}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Credits Remaining: {creditsRemaining}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}
#endif