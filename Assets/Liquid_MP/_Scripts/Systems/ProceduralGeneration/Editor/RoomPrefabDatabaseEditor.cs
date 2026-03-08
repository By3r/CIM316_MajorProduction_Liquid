using _Scripts.Systems.ProceduralGeneration;
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
        private SerializedProperty _safeElevatorProp;

        private bool _showRoomList = true;
        private bool _showStatistics = true;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _roomsProp = serializedObject.FindProperty("_rooms");
            _safeElevatorProp = serializedObject.FindProperty("_safeElevatorRoom");
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
        }

        private void DrawQuickActions(RoomPrefabDatabase database)
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Refresh All Rooms", GUILayout.Height(30)))
            {
                database.RefreshAllRooms();
                EditorUtility.SetDirty(database);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("+ Add Room Entry", GUILayout.Height(30)))
            {
                _roomsProp.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            if (database.IsValid())
            {
                EditorGUILayout.HelpBox("Database valid.", MessageType.Info);
            }
            else
            {
                string issues = "";
                if (database.EnabledRooms == 0)
                    issues += "• No enabled rooms\n";
                if (!database.HasAllSpecialRooms())
                    issues += "• Missing Safe Elevator Room\n";

                EditorGUILayout.HelpBox(issues.TrimEnd(), MessageType.Warning);
            }
        }

        private void DrawSpecialRooms()
        {
            EditorGUILayout.LabelField("Special Rooms", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_safeElevatorProp, new GUIContent("Safe Elevator Room"));
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
                EditorGUILayout.HelpBox("No rooms in database.", MessageType.Info);
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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            isEnabledProp.boolValue = EditorGUILayout.Toggle(isEnabledProp.boolValue, GUILayout.Width(20));

            string label = string.IsNullOrEmpty(displayNameProp.stringValue)
                ? $"Room {index}"
                : displayNameProp.stringValue;

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

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

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));
            EditorGUILayout.PropertyField(displayNameProp, new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(roomIDProp, new GUIContent("Room ID"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(categoryProp, new GUIContent("Category"));
            EditorGUILayout.PropertyField(sectorProp, new GUIContent("Sector"), GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(spawnWeightProp, new GUIContent("Spawn Weight"));

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