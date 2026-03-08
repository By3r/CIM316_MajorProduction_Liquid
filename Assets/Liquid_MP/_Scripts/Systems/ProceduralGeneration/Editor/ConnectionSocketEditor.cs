using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(ConnectionSocket))]
    public class ConnectionSocketEditor : UnityEditor.Editor
    {
        private SerializedProperty _socketTypeProp;
        private SerializedProperty _isConnectedProp;
        private SerializedProperty _forwardAngleOffsetProp;
        private SerializedProperty _boundsCenterProp;
        private SerializedProperty _boundsSizeProp;
        private SerializedProperty _doorPrefabProp;
        private SerializedProperty _doorSpawnPointProp;
        private SerializedProperty _blockadePrefabsProp;
        private SerializedProperty _spawnBlockadeIfUnconnectedProp;
        private SerializedProperty _showGizmosProp;

        private void OnEnable()
        {
            _socketTypeProp = serializedObject.FindProperty("_socketType");
            _isConnectedProp = serializedObject.FindProperty("_isConnected");
            _forwardAngleOffsetProp = serializedObject.FindProperty("_forwardAngleOffset");
            _boundsCenterProp = serializedObject.FindProperty("_boundsCenter");
            _boundsSizeProp = serializedObject.FindProperty("_boundsSize");
            _doorPrefabProp = serializedObject.FindProperty("_doorPrefab");
            _doorSpawnPointProp = serializedObject.FindProperty("_doorSpawnPoint");
            _blockadePrefabsProp = serializedObject.FindProperty("_blockadePrefabs");
            _spawnBlockadeIfUnconnectedProp = serializedObject.FindProperty("_spawnBlockadeIfUnconnected");
            _showGizmosProp = serializedObject.FindProperty("_showGizmos");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSocketConfiguration();
            EditorGUILayout.Space(10);

            DrawForwardDirection();
            EditorGUILayout.Space(10);

            DrawSocketBounds();
            EditorGUILayout.Space(10);

            DrawDoorSpawnSettings();
            EditorGUILayout.Space(10);

            DrawBlockadeSettings();
            EditorGUILayout.Space(10);

            DrawVisualizationSettings();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                DrawRuntimeInfo((ConnectionSocket)target);
            }

            EditorGUILayout.Space(10);
            DrawUsageInstructions();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSocketConfiguration()
        {
            EditorGUILayout.LabelField("Socket Configuration", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_socketTypeProp, new GUIContent("Socket Type",
                "The door tier this socket accepts. Must match Door.DoorType."));

            EditorGUILayout.Space(3);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_isConnectedProp, new GUIContent("Is Connected",
                "Is this socket currently connected? (Set at runtime by generation system)"));
            GUI.enabled = true;
        }

        private void DrawForwardDirection()
        {
            EditorGUILayout.LabelField("Forward Direction", EditorStyles.boldLabel);

            EditorGUILayout.Slider(_forwardAngleOffsetProp, 0f, 360f, new GUIContent("Forward Angle Offset",
                "Rotates the socket's outward direction around Y axis. Use if the door frame model doesn't face outward by default."));

            float angle = _forwardAngleOffsetProp.floatValue;
            if (angle > 0.1f)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Reset to 0"))
                {
                    _forwardAngleOffsetProp.floatValue = 0f;
                }
                EditorGUI.indentLevel--;
            }

            // Quick rotation buttons
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("0"))
                _forwardAngleOffsetProp.floatValue = 0f;
            if (GUILayout.Button("90"))
                _forwardAngleOffsetProp.floatValue = 90f;
            if (GUILayout.Button("180"))
                _forwardAngleOffsetProp.floatValue = 180f;
            if (GUILayout.Button("270"))
                _forwardAngleOffsetProp.floatValue = 270f;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSocketBounds()
        {
            EditorGUILayout.LabelField("Socket Bounds (Connection Point)", EditorStyles.boldLabel);

            var socket = (ConnectionSocket)target;

            if (!socket.HasBounds)
            {
                EditorGUILayout.HelpBox("Socket bounds not calculated!", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.PropertyField(_boundsCenterProp, new GUIContent("Bounds Center (Local)"));
                EditorGUILayout.PropertyField(_boundsSizeProp, new GUIContent("Bounds Size"));
            }

            EditorGUILayout.Space(3);

            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("Calculate Socket Bounds", GUILayout.Height(24)))
            {
                Undo.RecordObject(socket, "Calculate Socket Bounds");
                socket.CalculateBoundsFromRenderers();
                EditorUtility.SetDirty(socket);
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawDoorSpawnSettings()
        {
            EditorGUILayout.LabelField("Door Spawn Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_doorPrefabProp, new GUIContent("Door Prefab",
                "Prefab to instantiate when connected. Leave empty to use from DoorPrefabDatabase."));

            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_doorSpawnPointProp, new GUIContent("Door Spawn Point",
                "Child Transform that marks where the door spawns. If not assigned, uses socket position."));

            var socket = (ConnectionSocket)target;
            if (socket.DoorSpawnPointTransform == null)
            {
                if (GUILayout.Button("Create DoorSpawnPoint Child"))
                {
                    CreateDoorSpawnPoint(socket);
                }
            }

            EditorGUILayout.Space(3);

            // Door prefab validation
            if (_doorPrefabProp.objectReferenceValue != null)
            {
                var doorPrefab = _doorPrefabProp.objectReferenceValue as GameObject;
                if (doorPrefab != null)
                {
                    var doorComponent = doorPrefab.GetComponent<Door>();
                    if (doorComponent == null)
                    {
                        EditorGUILayout.HelpBox("Warning: Assigned prefab does not have a Door component!", MessageType.Warning);
                    }
                    else
                    {
                        var socketType = (Door.DoorType)_socketTypeProp.enumValueIndex;
                        if (doorComponent.Type != socketType)
                        {
                            EditorGUILayout.HelpBox($"Door type mismatch! Socket: {socketType}, Door: {doorComponent.Type}",
                                MessageType.Warning);
                        }
                    }
                }
            }
        }

        private void DrawBlockadeSettings()
        {
            EditorGUILayout.LabelField("Blockade Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_spawnBlockadeIfUnconnectedProp, new GUIContent("Spawn Blockade If Unconnected",
                "Should this socket spawn a blockade if it remains unconnected after generation?"));

            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_blockadePrefabsProp, new GUIContent("Blockade Prefabs",
                "List of prefabs to randomly spawn if socket is unconnected. Add multiple for variety!"), true);

            int blockadeCount = _blockadePrefabsProp.arraySize;
            bool spawnEnabled = _spawnBlockadeIfUnconnectedProp.boolValue;

            if (spawnEnabled && blockadeCount == 0)
            {
                EditorGUILayout.HelpBox("Blockade spawning enabled but no prefabs assigned!", MessageType.Warning);
            }
        }

        private void DrawVisualizationSettings()
        {
            EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_showGizmosProp, new GUIContent("Show Gizmos",
                "Show socket visualization in scene view?"));
        }

        private void DrawRuntimeInfo(ConnectionSocket socket)
        {
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

            GUI.enabled = false;

            EditorGUILayout.TextField("Socket Type", socket.SocketType.ToString());
            EditorGUILayout.Toggle("Is Connected", socket.IsConnected);

            if (socket.ConnectedSocket != null)
            {
                EditorGUILayout.ObjectField("Connected To", socket.ConnectedSocket, typeof(ConnectionSocket), true);
            }
            else
            {
                EditorGUILayout.TextField("Connected To", "None");
            }

            if (socket.InstantiatedDoor != null)
            {
                EditorGUILayout.ObjectField("Instantiated Door", socket.InstantiatedDoor, typeof(GameObject), true);
            }
            else
            {
                EditorGUILayout.TextField("Instantiated Door", "None");
            }

            if (socket.InstantiatedBlockade != null)
            {
                EditorGUILayout.ObjectField("Instantiated Blockade", socket.InstantiatedBlockade, typeof(GameObject), true);
            }
            else
            {
                EditorGUILayout.TextField("Instantiated Blockade", "None");
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Socket Transform", EditorStyles.miniBoldLabel);
            EditorGUILayout.Vector3Field("Position", socket.Position);
            EditorGUILayout.Vector3Field("Forward Direction", socket.Forward);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Door Spawn Transform", EditorStyles.miniBoldLabel);
            EditorGUILayout.Vector3Field("Door Spawn Position", socket.DoorSpawnPosition);
            if (socket.DoorSpawnPointTransform != null)
            {
                EditorGUILayout.ObjectField("Door Spawn Point", socket.DoorSpawnPointTransform, typeof(Transform), true);
            }

            GUI.enabled = true;

            EditorGUILayout.Space(5);

            if (Application.isPlaying)
            {
                if (!socket.IsConnected)
                {
                    if (GUILayout.Button("Test Spawn Blockade (Runtime)"))
                    {
                        socket.SpawnBlockade();
                    }
                }

                if (GUILayout.Button("Disconnect & Clear (Runtime)"))
                {
                    socket.Disconnect();
                }
            }
        }

        private void DrawUsageInstructions()
        {
            EditorGUILayout.LabelField("Usage Instructions", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "SOCKET SETUP:\n" +
                "1. Add ConnectionSocket to a door frame wall piece (the mesh with the hole)\n" +
                "2. The FORWARD (blue arrow) should point OUT of the room through the doorway\n" +
                "3. If the model faces the wrong way, adjust 'Forward Angle Offset'\n" +
                "4. Set the appropriate Socket Type for this opening\n\n" +
                "DOOR SPAWN POINT:\n" +
                "5. Create a child empty GameObject named 'DoorSpawnPoint'\n" +
                "6. Position it exactly where the door should appear\n" +
                "7. Assign it in the 'Door Spawn Point' field\n\n" +
                "BLOCKADE SETUP:\n" +
                "8. Add blockade prefabs for unconnected sockets\n\n" +
                "COLORS (when selected):\n" +
                "Yellow = Unconnected, Green = Connected, Blue = Forward, Magenta = Door spawn point",
                MessageType.Info);
        }

        private void CreateDoorSpawnPoint(ConnectionSocket socket)
        {
            GameObject spawnPoint = new GameObject("DoorSpawnPoint");
            Undo.RegisterCreatedObjectUndo(spawnPoint, "Create DoorSpawnPoint");

            spawnPoint.transform.SetParent(socket.transform);
            spawnPoint.transform.localPosition = Vector3.zero;
            spawnPoint.transform.localRotation = Quaternion.identity;

            _doorSpawnPointProp.objectReferenceValue = spawnPoint.transform;
            serializedObject.ApplyModifiedProperties();

            Selection.activeGameObject = spawnPoint;

        }

        // Scene view handles
        private void OnSceneGUI()
        {
            var socket = (ConnectionSocket)target;

            // Draw position handle
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(socket.Position, socket.Rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(socket.transform, "Move Connection Socket");
                socket.transform.position = newPosition;
            }

            // Draw rotation handle
            EditorGUI.BeginChangeCheck();
            Quaternion newRotation = Handles.RotationHandle(socket.Rotation, socket.Position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(socket.transform, "Rotate Connection Socket");
                socket.transform.rotation = newRotation;
            }

            // Draw socket label
            string statusLabel = socket.IsConnected ? "Connected" : "Unconnected";
            if (!socket.IsConnected && socket.SpawnBlockadeIfUnconnected && socket.BlockadePrefabs.Count > 0)
            {
                statusLabel += $" (Has {socket.BlockadePrefabs.Count} blockade{(socket.BlockadePrefabs.Count > 1 ? "s" : "")})";
            }

            Handles.Label(socket.Position + Vector3.up * 0.5f,
                $"Socket: {socket.SocketType}\n{statusLabel}",
                EditorStyles.whiteBoldLabel);

            // Draw DoorSpawnPoint label if assigned
            if (socket.DoorSpawnPointTransform != null)
            {
                Handles.Label(socket.DoorSpawnPosition + Vector3.up * 0.3f,
                    "Door Spawn",
                    EditorStyles.whiteLabel);
            }
        }
    }
}
