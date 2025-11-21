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
        private int _testSeed = 12345;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FloorGenerator generator = (FloorGenerator)target;

            EditorGUILayout.Space(10);
            
            // ===== NEW: Seed-Based Generation Section =====
            DrawSeedGenerationSection(generator);
            EditorGUILayout.Space(10);
            // ==============================================

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
                "SEED-BASED GENERATION:\n" +
                "7. Enable 'Use Seed Based Generation' for deterministic floors\n" +
                "8. Initialize FloorStateManager with a seed (see section above)\n" +
                "9. Generate multiple times - same seed = same layout!\n\n",
                MessageType.Info);

            EditorGUILayout.Space(5);
            ShowGenerationStats(generator);
        }

        // ===== NEW: Seed-Based Generation Section =====
        private void DrawSeedGenerationSection(FloorGenerator generator)
        {
            EditorGUILayout.LabelField("Seed-Based Generation Testing", EditorStyles.boldLabel);

            // Check if FloorStateManager exists
            bool managerExists = FloorStateManager.Instance != null;
            bool isInitialized = managerExists && FloorStateManager.Instance.IsInitialized;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Status display
            EditorGUILayout.LabelField("Status:", EditorStyles.miniBoldLabel);
            string statusText = !managerExists ? "❌ FloorStateManager not in scene" :
                               !isInitialized ? "⚠️ FloorStateManager not initialized" :
                               "✅ FloorStateManager ready";
            Color statusColor = !managerExists ? Color.red :
                               !isInitialized ? Color.yellow :
                               Color.green;
            
            GUI.color = statusColor;
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
            GUI.color = Color.white;

            if (isInitialized)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"World Seed: {FloorStateManager.Instance.WorldSeed}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Current Floor: {FloorStateManager.Instance.CurrentFloorNumber}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Last Generated Seed: {generator.CurrentSeed}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Initialization controls
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Initialize with Seed:", GUILayout.Width(130));
            _testSeed = EditorGUILayout.IntField(_testSeed, GUILayout.Width(100));
            
            if (GUILayout.Button("Initialize", GUILayout.Width(80)))
            {
                if (!managerExists)
                {
                    // Create FloorStateManager if it doesn't exist
                    GameObject go = new GameObject("FloorStateManager");
                    go.AddComponent<FloorStateManager>();
                    EditorUtility.DisplayDialog("FloorStateManager Created", 
                        "FloorStateManager has been added to the scene. Click 'Initialize' again.", "OK");
                }
                else
                {
                    FloorStateManager.Instance.Initialize(_testSeed);
                    Debug.Log($"[FloorGeneratorEditor] Initialized FloorStateManager with seed: {_testSeed}");
                }
            }

            if (GUILayout.Button("Random Seed", GUILayout.Width(100)))
            {
                if (!managerExists)
                {
                    GameObject go = new GameObject("FloorStateManager");
                    go.AddComponent<FloorStateManager>();
                }
                
                FloorStateManager.Instance.Initialize(0); // 0 = random seed
                Debug.Log($"[FloorGeneratorEditor] Initialized with random seed: {FloorStateManager.Instance.WorldSeed}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Determinism test buttons
            if (isInitialized)
            {
                EditorGUILayout.LabelField("Determinism Testing:", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();
                
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Test: Generate Floor 1 Twice", GUILayout.Height(30)))
                {
                    generator.CurrentFloorNumber = 1;
                    
                    Debug.Log("[TEST] === Generating Floor 1 (First Pass) ===");
                    generator.GenerateFloor();
                    int firstSeed = generator.CurrentSeed;
                    
                    EditorUtility.DisplayProgressBar("Testing Determinism", "Clearing floor...", 0.5f);
                    generator.ClearFloor();
                    
                    Debug.Log("[TEST] === Generating Floor 1 (Second Pass) ===");
                    generator.GenerateFloor();
                    int secondSeed = generator.CurrentSeed;
                    
                    EditorUtility.ClearProgressBar();
                    
                    if (firstSeed == secondSeed)
                    {
                        Debug.Log($"[TEST] ✅ SUCCESS: Both generations used seed {firstSeed}");
                        EditorUtility.DisplayDialog("Determinism Test", 
                            $"✅ SUCCESS!\n\nBoth generations used seed: {firstSeed}\n\n" +
                            "If the layouts are identical, determinism is working correctly!\n\n" +
                            "Check the Scene view to verify.", "OK");
                    }
                    else
                    {
                        Debug.LogError($"[TEST] ❌ FAILED: Seeds don't match! First: {firstSeed}, Second: {secondSeed}");
                        EditorUtility.DisplayDialog("Determinism Test", 
                            $"❌ FAILED!\n\nSeeds don't match:\nFirst: {firstSeed}\nSecond: {secondSeed}", "OK");
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(1f, 0.8f, 0.4f); // Orange
                if (GUILayout.Button("Generate Floor 2", GUILayout.Height(25)))
                {
                    generator.CurrentFloorNumber = 2;
                    generator.GenerateFloor();
                    Debug.Log($"[TEST] Generated Floor 2 with seed: {generator.CurrentSeed}");
                }

                if (GUILayout.Button("Generate Floor 3", GUILayout.Height(25)))
                {
                    generator.CurrentFloorNumber = 3;
                    generator.GenerateFloor();
                    Debug.Log($"[TEST] Generated Floor 3 with seed: {generator.CurrentSeed}");
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Test determinism by generating Floor 1 twice - layouts should be identical!\n" +
                    "Try generating different floor numbers to see unique seeds per floor.", 
                    MessageType.Info);
            }
        }
        // =============================================

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