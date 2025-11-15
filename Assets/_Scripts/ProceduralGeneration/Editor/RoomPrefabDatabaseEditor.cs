using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;

namespace _Scripts.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(RoomPrefabDatabase))]
    public class RoomPrefabDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty _roomsProp;
        private SerializedProperty _entryElevatorProp;
        private SerializedProperty _exitElevatorProp;
        private SerializedProperty _safeRoomProp;

        private bool _showRoomList = true;
        private bool _showStatistics = true;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _roomsProp = serializedObject.FindProperty("_rooms");
            _entryElevatorProp = serializedObject.FindProperty("_entryElevatorRoom");
            _exitElevatorProp = serializedObject.FindProperty("_exitElevatorRoom");
            _safeRoomProp = serializedObject.FindProperty("_safeRoom");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var database = (RoomPrefabDatabase)target;

            DrawDatabaseHeader();
            EditorGUILayout.Space(5);

            DrawQuickActions(database);
            EditorGUILayout.Space(10);

            DrawSpecialRooms();
            EditorGUILayout.Space(10);

            DrawStatistics(database);
            EditorGUILayout.Space(10);

            DrawRoomList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDatabaseHeader()
        {
            EditorGUILayout.LabelField("Room Prefab Database", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manages all room prefabs for procedural generation.\n" +
                "Add room prefabs below, then click 'Refresh All Rooms' to update socket info.",
                MessageType.Info);
        }

        private void DrawQuickActions(RoomPrefabDatabase database)
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Refresh button
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Refresh All Rooms", GUILayout.Height(30)))
            {
                database.RefreshAllRooms();
                EditorUtility.SetDirty(database);
            }
            GUI.backgroundColor = Color.white;

            // Add room button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("+ Add Room Entry", GUILayout.Height(30)))
            {
                _roomsProp.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Validation status
            if (database.IsValid())
            {
                EditorGUILayout.HelpBox("Database is valid and ready for generation! :)", MessageType.Info);
            }
            else
            {
                string issues = "";
                if (database.EnabledRooms == 0)
                    issues += "• No enabled rooms\n";
                if (!database.HasAllSpecialRooms())
                    issues += "• Missing special rooms (Entry/Exit/Safe)\n";

                EditorGUILayout.HelpBox($"DATABASE HAS ISSUES:\n{issues}", MessageType.Warning);
            }
        }

        private void DrawSpecialRooms()
        {
            EditorGUILayout.LabelField("Special Rooms", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "These rooms are used for specific purposes and excluded from random generation.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_entryElevatorProp, new GUIContent("Entry Elevator Room",
                "Starting room for Floors 2+ (elevator entrance)"));

            EditorGUILayout.PropertyField(_exitElevatorProp, new GUIContent("Exit Elevator Room",
                "Ending room for all floors (elevator exit)"));

            EditorGUILayout.PropertyField(_safeRoomProp, new GUIContent("Safe Room",
                "Floor 1 start / Optional sanctuary on other floors"));
        }

        private void DrawStatistics(RoomPrefabDatabase database)
        {
            _showStatistics = EditorGUILayout.Foldout(_showStatistics, "Database Statistics", true, EditorStyles.foldoutHeader);

            if (!_showStatistics) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Overall", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Total Rooms: {database.TotalRooms}");
            EditorGUILayout.LabelField($"Enabled Rooms: {database.EnabledRooms}");

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("By Category", EditorStyles.miniBoldLabel);

            var categoryStats = database.AllRooms
                .Where(r => r.isEnabled && r.prefab != null)
                .GroupBy(r => r.category)
                .OrderBy(g => g.Key)
                .ToList();

            if (categoryStats.Count > 0)
            {
                foreach (var group in categoryStats)
                {
                    EditorGUILayout.LabelField($"  {group.Key}: {group.Count()}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("  (No rooms available)");
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("By Sector", EditorStyles.miniBoldLabel);

            var sectorStats = database.AllRooms
                .Where(r => r.isEnabled && r.prefab != null)
                .GroupBy(r => r.sectorNumber)
                .OrderBy(g => g.Key)
                .ToList();

            if (sectorStats.Count > 0)
            {
                foreach (var group in sectorStats)
                {
                    EditorGUILayout.LabelField($"  Sector {group.Key}: {group.Count()}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("  (No rooms available)");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawRoomList()
        {
            _showRoomList = EditorGUILayout.Foldout(_showRoomList, $"Room Entries ({_roomsProp.arraySize})", true, EditorStyles.foldoutHeader);

            if (!_showRoomList) return;

            if (_roomsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No rooms in database. Click 'Add Room Entry' to start.", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(400));

            for (int i = 0; i < _roomsProp.arraySize; i++)
            {
                DrawRoomEntry(i);
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            EditorGUI.indentLevel--;
        }

        private void DrawRoomEntry(int index)
        {
            SerializedProperty entryProp = _roomsProp.GetArrayElementAtIndex(index);

            SerializedProperty prefabProp = entryProp.FindPropertyRelative("prefab");
            SerializedProperty roomIDProp = entryProp.FindPropertyRelative("roomID");
            SerializedProperty displayNameProp = entryProp.FindPropertyRelative("displayName");
            SerializedProperty categoryProp = entryProp.FindPropertyRelative("category");
            SerializedProperty sectorProp = entryProp.FindPropertyRelative("sectorNumber");
            SerializedProperty socketTypesProp = entryProp.FindPropertyRelative("socketTypes");
            SerializedProperty socketCountProp = entryProp.FindPropertyRelative("socketCount");
            SerializedProperty spawnWeightProp = entryProp.FindPropertyRelative("spawnWeight");
            SerializedProperty isEnabledProp = entryProp.FindPropertyRelative("isEnabled");

            // Header bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            // Enable toggle
            isEnabledProp.boolValue = EditorGUILayout.Toggle(isEnabledProp.boolValue, GUILayout.Width(20));

            // Room name or index
            string label = string.IsNullOrEmpty(displayNameProp.stringValue)
                ? $"Room {index}"
                : displayNameProp.stringValue;

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Remove button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("Remove Room?",
                    $"Remove '{label}' from the database?",
                    "Yes", "Cancel"))
                {
                    _roomsProp.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    return;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Room details
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));
            EditorGUILayout.PropertyField(displayNameProp, new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(roomIDProp, new GUIContent("Room ID (Optional)"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(categoryProp, new GUIContent("Category"));
            EditorGUILayout.PropertyField(sectorProp, new GUIContent("Sector"), GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(spawnWeightProp, new GUIContent("Spawn Weight"));

            // Socket info (read-only)
            EditorGUILayout.Space(3);
            GUI.enabled = false;
            EditorGUILayout.IntField("Socket Count", socketCountProp.intValue);

            if (socketTypesProp.arraySize > 0)
            {
                string types = "";
                for (int j = 0; j < socketTypesProp.arraySize; j++)
                {
                    if (j > 0) types += ", ";
                    types += socketTypesProp.GetArrayElementAtIndex(j).enumDisplayNames[
                        socketTypesProp.GetArrayElementAtIndex(j).enumValueIndex];
                }
                EditorGUILayout.TextField("Socket Types", types);
            }
            else
            {
                EditorGUILayout.TextField("Socket Types", "(No sockets detected)");
            }
            GUI.enabled = true;

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }
    }
}
#endif