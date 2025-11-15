using UnityEditor;
using UnityEngine;
using System.Linq;
using _Scripts.Systems.ProceduralGeneration;

namespace _Scripts.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(OccupiedSpaceRegistry))]
    public class OccupiedSpaceRegistryEditor : UnityEditor.Editor
    {
        private OccupiedSpaceRegistry _registry;
        
        // Serialized properties
        private SerializedProperty _autoUpdateMovedRooms;
        private SerializedProperty _moveCheckInterval;
        private SerializedProperty _showDebugLogs;
        private SerializedProperty _showGizmos;
        private SerializedProperty _occupiedSpaceColor;
        private SerializedProperty _occupiedSpaces;

        // Foldout states
        private bool _showStatsFoldout = true;
        private bool _showOccupiedSpacesFoldout = true;

        private void OnEnable()
        {
            _registry = (OccupiedSpaceRegistry)target;

            // Cache serialized properties
            _autoUpdateMovedRooms = serializedObject.FindProperty("_autoUpdateMovedRooms");
            _moveCheckInterval = serializedObject.FindProperty("_moveCheckInterval");
            _showDebugLogs = serializedObject.FindProperty("_showDebugLogs");
            _showGizmos = serializedObject.FindProperty("_showGizmos");
            _occupiedSpaceColor = serializedObject.FindProperty("_occupiedSpaceColor");
            _occupiedSpaces = serializedObject.FindProperty("_occupiedSpaces");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Occupied Space Registry", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Central registry that tracks all occupied spaces in the procedurally generated level.\n" +
                "• Automatic room tracking\n" +
                "• Fast collision detection\n" +
                "• Prevents room overlaps during generation\n" +
                "• Integrates with BoundsChecker and DoorConnectionSystem",
                MessageType.Info);

            EditorGUILayout.Space();

            // === REGISTRY SETTINGS ===
            DrawRegistrySettings();

            EditorGUILayout.Space();

            // === DEBUG SETTINGS ===
            DrawDebugSettings();

            EditorGUILayout.Space();

            // === STATISTICS ===
            DrawStatistics();

            EditorGUILayout.Space();

            // === OCCUPIED SPACES LIST ===
            DrawOccupiedSpacesList();

            EditorGUILayout.Space();

            // === ACTIONS ===
            DrawActions();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRegistrySettings()
        {
            EditorGUILayout.LabelField("Registry Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_autoUpdateMovedRooms, new GUIContent("Auto Update Moved Rooms"));
            
            if (_autoUpdateMovedRooms.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_moveCheckInterval, new GUIContent("Move Check Interval (s)"));
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox(
                    "Automatically detects when rooms move and updates their bounds in the registry. " +
                    "Useful during level editing or dynamic room adjustments.",
                    MessageType.None);
            }
        }

        private void DrawDebugSettings()
        {
            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_showDebugLogs, new GUIContent("Show Debug Logs"));
            EditorGUILayout.PropertyField(_showGizmos, new GUIContent("Show Gizmos"));

            if (_showGizmos.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_occupiedSpaceColor, new GUIContent("Occupied Space Color"));
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox(
                    "Gizmos show all registered room bounds in the Scene view. " +
                    "Red wireframe cubes indicate occupied spaces.",
                    MessageType.None);
            }
        }

        private void DrawStatistics()
        {
            _showStatsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_showStatsFoldout, "Registry Statistics");
            
            if (_showStatsFoldout)
            {
                EditorGUI.indentLevel++;
                
                var allSpaces = _registry.GetAllOccupiedSpaces();
                int validRooms = allSpaces.Count(s => s.boundsChecker != null && s.roomTransform != null);
                int nullEntries = allSpaces.Count - validRooms;
                
                EditorGUILayout.LabelField("Total Registered Rooms:", validRooms.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Null/Invalid Entries:", nullEntries.ToString());
                EditorGUILayout.LabelField("Total Entries:", allSpaces.Count.ToString());
                
                if (allSpaces.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    
                    // Calculate total occupied volume
                    float totalVolume = 0f;
                    foreach (var space in allSpaces)
                    {
                        if (space.roomTransform != null)
                        {
                            Bounds bounds = space.paddedBoundsWorld;
                            totalVolume += bounds.size.x * bounds.size.y * bounds.size.z;
                        }
                    }
                    
                    EditorGUILayout.LabelField("Total Occupied Volume:", $"{totalVolume:F2} cubic units");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOccupiedSpacesList()
        {
            _showOccupiedSpacesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_showOccupiedSpacesFoldout, "Occupied Spaces");
            
            if (_showOccupiedSpacesFoldout)
            {
                if (_occupiedSpaces.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No occupied spaces registered. Rooms will auto-register when BoundsChecker is enabled.", MessageType.Info);
                }
                else
                {
                    EditorGUI.indentLevel++;
                    
                    var allSpaces = _registry.GetAllOccupiedSpaces();
                    
                    for (int i = 0; i < allSpaces.Count; i++)
                    {
                        var space = allSpaces[i];
                        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        // Room name and status
                        bool isValid = space.boundsChecker != null && space.roomTransform != null;
                        string statusIcon = isValid ? "✓" : "✗";
                        Color statusColor = isValid ? Color.green : Color.red;
                        
                        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
                        labelStyle.normal.textColor = statusColor;
                        
                        EditorGUILayout.LabelField($"{statusIcon} {space.roomName}", labelStyle);
                        
                        if (isValid)
                        {
                            EditorGUI.indentLevel++;
                            
                            // Room transform (clickable)
                            EditorGUILayout.ObjectField("Room Transform", space.roomTransform, typeof(Transform), true);
                            
                            // Position and rotation
                            EditorGUILayout.LabelField("Position:", space.registeredPosition.ToString("F2"));
                            EditorGUILayout.LabelField("Rotation:", space.registeredRotation.eulerAngles.ToString("F1"));
                            
                            // Bounds info
                            Bounds bounds = space.paddedBoundsWorld;
                            EditorGUILayout.LabelField("Bounds Center:", bounds.center.ToString("F2"));
                            EditorGUILayout.LabelField("Bounds Size:", bounds.size.ToString("F2"));
                            
                            // Registration time
                            float timeSinceRegistration = Time.time - space.registrationTime;
                            EditorGUILayout.LabelField("Registered:", $"{timeSinceRegistration:F1}s ago");
                            
                            // Movement check
                            if (space.HasMoved())
                            {
                                EditorGUILayout.HelpBox("⚠ Room has moved since registration!", MessageType.Warning);
                                
                                if (GUILayout.Button("Update Bounds", GUILayout.Height(20)))
                                {
                                    space.UpdateBounds();
                                    EditorUtility.SetDirty(_registry);
                                }
                            }
                            
                            EditorGUI.indentLevel--;
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Invalid entry (null reference). Clean up to remove.", MessageType.Error);
                        }
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }
                    
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Cleanup button
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Clean Up Null Entries", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clean Up Registry", 
                    "Remove all null or invalid entries from the registry?", 
                    "Clean Up", 
                    "Cancel"))
                {
                    Undo.RecordObject(_registry, "Clean Up Null Entries");
                    _registry.CleanupNullEntries();
                    EditorUtility.SetDirty(_registry);
                }
            }
            
            // Clear button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Light red
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Registry", 
                    "Remove all entries from the registry? This cannot be undone.", 
                    "Clear", 
                    "Cancel"))
                {
                    Undo.RecordObject(_registry, "Clear Registry");
                    _registry.ClearRegistry();
                    EditorUtility.SetDirty(_registry);
                }
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Refresh button
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Refresh All Bounds", GUILayout.Height(25)))
            {
                Undo.RecordObject(_registry, "Refresh All Bounds");
                
                var allSpaces = _registry.GetAllOccupiedSpaces();
                int updatedCount = 0;
                
                foreach (var space in allSpaces)
                {
                    if (space.boundsChecker != null && space.roomTransform != null)
                    {
                        space.UpdateBounds();
                        updatedCount++;
                    }
                }
                
                EditorUtility.SetDirty(_registry);
                Debug.Log($"[OccupiedSpaceRegistry] Refreshed {updatedCount} room bounds.");
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.HelpBox(
                "• Clean Up: Removes destroyed or invalid room entries\n" +
                "• Clear All: Resets the entire registry (use when regenerating floors)\n" +
                "• Refresh All Bounds: Updates all room bounds (useful after moving rooms)",
                MessageType.None);
        }
    }
}