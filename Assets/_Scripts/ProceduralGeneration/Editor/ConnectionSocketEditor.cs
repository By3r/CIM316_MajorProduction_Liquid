using _Scripts.ProceduralGeneration.Doors;
using UnityEditor;
using UnityEngine;

namespace _Scripts.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(ConnectionSocket))]
    public class ConnectionSocketEditor : UnityEditor.Editor
    {
        private SerializedProperty _socketTypeProp;
        private SerializedProperty _isConnectedProp;
        private SerializedProperty _doorPrefabProp;
        private SerializedProperty _doorSpawnOffsetProp;
        private SerializedProperty _blockadePrefabsProp;
        private SerializedProperty _spawnBlockadeIfUnconnectedProp;
        private SerializedProperty _showGizmosProp;
        private SerializedProperty _socketSizeProp;
        private SerializedProperty _showDoorOffsetGizmoProp;

        private void OnEnable()
        {
            _socketTypeProp = serializedObject.FindProperty("_socketType");
            _isConnectedProp = serializedObject.FindProperty("_isConnected");
            _doorPrefabProp = serializedObject.FindProperty("_doorPrefab");
            _doorSpawnOffsetProp = serializedObject.FindProperty("_doorSpawnOffset");
            _blockadePrefabsProp = serializedObject.FindProperty("_blockadePrefabs");
            _spawnBlockadeIfUnconnectedProp = serializedObject.FindProperty("_spawnBlockadeIfUnconnected");
            _showGizmosProp = serializedObject.FindProperty("_showGizmos");
            _socketSizeProp = serializedObject.FindProperty("_socketSize");
            _showDoorOffsetGizmoProp = serializedObject.FindProperty("_showDoorOffsetGizmo");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSocketConfiguration();
            EditorGUILayout.Space(10);
            
            DrawDoorPrefabSettings();
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
            
            var socketType = (Door.DoorType)_socketTypeProp.enumValueIndex;
            
            EditorGUI.indentLevel++;
            switch (socketType)
            {
                case Door.DoorType.Standard:
                    EditorGUILayout.HelpBox("Standard sockets connect basic corridors and small rooms.", MessageType.Info);
                    break;
                case Door.DoorType.Large:
                    EditorGUILayout.HelpBox("Large sockets are for wide openings and hub rooms.", MessageType.Info);
                    break;
                case Door.DoorType.Airlock:
                    EditorGUILayout.HelpBox("Airlock sockets are for sector transitions.", MessageType.Info);
                    break;
                case Door.DoorType.Emergency:
                    EditorGUILayout.HelpBox("Emergency sockets are for emergency exits.", MessageType.Info);
                    break;
                case Door.DoorType.Maintenance:
                    EditorGUILayout.HelpBox("Maintenance sockets are for service tunnels.", MessageType.Info);
                    break;
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(3);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_isConnectedProp, new GUIContent("Is Connected", 
                "Is this socket currently connected? (Set at runtime by generation system)"));
            GUI.enabled = true;
        }

        private void DrawDoorPrefabSettings()
        {
            EditorGUILayout.LabelField("Door Prefab Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_doorPrefabProp, new GUIContent("Door Prefab (Optional)", 
                "Prefab to instantiate when connected. Leave empty to use from DoorPrefabDatabase."));
            
            // Door Spawn Offset
            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_doorSpawnOffsetProp, new GUIContent("Door Spawn Offset", 
                "Local position offset for spawning the door. Adjust this to align door pivot points."));
            
            // Show helpful info if offset is non-zero
            Vector3 offset = _doorSpawnOffsetProp.vector3Value;
            if (offset != Vector3.zero)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    $"Door will spawn {offset.magnitude:F2} units from socket position.\n" +
                    $"X: {offset.x:F2} (Left/Right)\n" +
                    $"Y: {offset.y:F2} (Up/Down)\n" +
                    $"Z: {offset.z:F2} (Forward/Back)", 
                    MessageType.Info);
                
                if (GUILayout.Button("Reset Offset to Zero"))
                {
                    _doorSpawnOffsetProp.vector3Value = Vector3.zero;
                }
                EditorGUI.indentLevel--;
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
                            EditorGUILayout.HelpBox($"Warning: Door type mismatch!\nSocket: {socketType}, Door: {doorComponent.Type}", 
                                MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"âœ“ Door type matches socket: {socketType}", MessageType.Info);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No prefab assigned - will use DoorPrefabDatabase at runtime.", MessageType.Info);
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
            
            // Validation and help
            int blockadeCount = _blockadePrefabsProp.arraySize;
            bool spawnEnabled = _spawnBlockadeIfUnconnectedProp.boolValue;
            
            EditorGUILayout.Space(3);
            
            if (spawnEnabled && blockadeCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Blockade spawning is enabled but no prefabs assigned!\n" +
                    "Click '+' below to add blockade prefabs.", 
                    MessageType.Warning);
            }
            else if (spawnEnabled && blockadeCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"âœ“ {blockadeCount} blockade prefab{(blockadeCount > 1 ? "s" : "")} configured.\n" +
                    $"If this socket is unconnected after generation, one will be randomly selected and spawned.", 
                    MessageType.Info);
                
                // Show blockade prefab details
                EditorGUI.indentLevel++;
                for (int i = 0; i < blockadeCount; i++)
                {
                    var blockadeProp = _blockadePrefabsProp.GetArrayElementAtIndex(i);
                    if (blockadeProp.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox($"Slot {i + 1}: Empty (drag a prefab here)", MessageType.Warning);
                    }
                    else
                    {
                        var prefab = blockadeProp.objectReferenceValue as GameObject;
                        EditorGUILayout.LabelField($"Slot {i + 1}: {prefab.name}", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }
            else if (!spawnEnabled && blockadeCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "Blockade spawning is disabled.\n" +
                    "Enable 'Spawn Blockade If Unconnected' to use the assigned prefabs.", 
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Blockade spawning is disabled and no prefabs assigned.\n" +
                    "This socket will remain empty if unconnected.", 
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(3);
            
            // Quick add buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Blockade Slot"))
            {
                _blockadePrefabsProp.arraySize++;
            }
            if (blockadeCount > 0 && GUILayout.Button("Remove Last Slot"))
            {
                _blockadePrefabsProp.arraySize--;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVisualizationSettings()
        {
            EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_showGizmosProp, new GUIContent("Show Gizmos", 
                "Show socket visualization in scene view?"));
            
            EditorGUILayout.PropertyField(_socketSizeProp, new GUIContent("Socket Size", 
                "Visual size of the doorway opening (width, height in units)"));
            
            if (_socketSizeProp.vector2Value.x <= 0 || _socketSizeProp.vector2Value.y <= 0)
            {
                EditorGUILayout.HelpBox("Socket size should be greater than 0!", MessageType.Warning);
            }
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.PropertyField(_showDoorOffsetGizmoProp, new GUIContent("Show Door Offset Gizmo", 
                "Show magenta line and cube indicating where doors will spawn?"));
            
            if (_showDoorOffsetGizmoProp.boolValue && _doorSpawnOffsetProp.vector3Value == Vector3.zero)
            {
                EditorGUILayout.HelpBox("Door offset is zero - offset gizmo won't be visible.", MessageType.Info);
            }
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
            EditorGUILayout.Vector3Field("Door Spawn Offset", socket.DoorSpawnOffset);
            
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
                "1. Place ConnectionSocket at doorway centers in room prefabs\n" +
                "2. Ensure the FORWARD (blue Z-axis) points OUT of the room\n" +
                "3. Set appropriate Socket Type for each opening\n" +
                "4. Adjust Door Spawn Offset if door pivot is not centered\n\n" +
                "BLOCKADE SETUP:\n" +
                "5. Add blockade prefabs to spawn if socket remains unconnected\n" +
                "6. Add multiple prefabs for random variety\n" +
                "7. Enable/disable per socket as needed\n\n" +
                "GENERATION:\n" +
                "8. FloorGenerator will connect matching sockets automatically\n" +
                "9. Unconnected sockets spawn blockades at end of generation\n\n" +
                "COLORS:\n" +
                "Yellow = Unconnected, Green = Connected, Magenta = Door spawn point",
                MessageType.Info);
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Align to Room Center"))
            {
                AlignSocketToRoomCenter();
            }
            
            if (GUILayout.Button("Snap to Ground"))
            {
                SnapSocketToGround();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Tip: Use 'Align to Room Center' to position at room center.\n" +
                "Use 'Snap to Ground' to move socket down until it hits ground (perfect for floor placement).", 
                MessageType.Info);
        }

        private void AlignSocketToRoomCenter()
        {
            var socket = (ConnectionSocket)target;
            
            // Find the room's transform (parent or root)
            Transform roomTransform = socket.transform.parent;
            if (roomTransform == null)
            {
                roomTransform = socket.transform.root;
            }
            
            // Get room bounds
            Renderer[] renderers = roomTransform.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[ConnectionSocket] No renderers found in room to calculate bounds!");
                return;
            }
            
            Bounds roomBounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                roomBounds.Encapsulate(renderer.bounds);
            }
            
            // Position socket at center of a wall
            // This is a simplified version - you may want to customize this based on your needs
            Vector3 targetPosition = roomBounds.center;
            
            Undo.RecordObject(socket.transform, "Align Socket to Room Center");
            socket.transform.position = targetPosition;
            
            Debug.Log($"[ConnectionSocket] Aligned '{socket.gameObject.name}' to room center. Adjust position as needed!");
        }

        private void SnapSocketToGround()
        {
            var socket = (ConnectionSocket)target;
            Vector3 startPosition = socket.transform.position;
            
            // Get the socket size to calculate offset
            SerializedProperty socketSizeProp = serializedObject.FindProperty("_socketSize");
            Vector2 socketSize = socketSizeProp.vector2Value;
            
            // For a plane/disc socket, we need to offset by half the HEIGHT
            // The socket height is socketSize.y (the vertical dimension)
            // We want the BOTTOM of the socket to touch the ground, not the center
            float socketHalfHeight = socketSize.y * 0.5f;
            
            // Find the room's root transform
            Transform roomTransform = socket.transform.parent;
            if (roomTransform == null)
            {
                roomTransform = socket.transform.root;
            }
            
            // Method 1: Try to find floor renderers/meshes below the socket
            MeshRenderer[] renderers = roomTransform.GetComponentsInChildren<MeshRenderer>();
            MeshCollider[] colliders = roomTransform.GetComponentsInChildren<MeshCollider>();
            
            float lowestPointY = float.MaxValue;
            bool foundGround = false;
            string hitObjectName = "";
            
            // Check all renderers for the lowest point below the socket
            foreach (var renderer in renderers)
            {
                // Skip if this is above the socket
                if (renderer.bounds.min.y > startPosition.y)
                    continue;
                
                // Check if socket's XZ position is within this renderer's XZ bounds
                Vector3 socketXZ = new Vector3(startPosition.x, 0, startPosition.z);
                Vector3 boundsCenter = new Vector3(renderer.bounds.center.x, 0, renderer.bounds.center.z);
                Vector3 boundsExtents = new Vector3(renderer.bounds.extents.x, 0, renderer.bounds.extents.z);
                
                // Simple XZ containment check
                if (Mathf.Abs(socketXZ.x - boundsCenter.x) <= boundsExtents.x &&
                    Mathf.Abs(socketXZ.z - boundsCenter.z) <= boundsExtents.z)
                {
                    // This renderer is below and contains the socket's XZ position
                    float topY = renderer.bounds.max.y;
                    if (topY < lowestPointY && topY < startPosition.y)
                    {
                        lowestPointY = topY;
                        foundGround = true;
                        hitObjectName = renderer.gameObject.name;
                    }
                }
            }
            
            // Method 2: If no renderer found, try colliders
            if (!foundGround)
            {
                foreach (var collider in colliders)
                {
                    if (collider.bounds.min.y > startPosition.y)
                        continue;
                    
                    Vector3 socketXZ = new Vector3(startPosition.x, 0, startPosition.z);
                    Vector3 boundsCenter = new Vector3(collider.bounds.center.x, 0, collider.bounds.center.z);
                    Vector3 boundsExtents = new Vector3(collider.bounds.extents.x, 0, collider.bounds.extents.z);
                    
                    if (Mathf.Abs(socketXZ.x - boundsCenter.x) <= boundsExtents.x &&
                        Mathf.Abs(socketXZ.z - boundsCenter.z) <= boundsExtents.z)
                    {
                        float topY = collider.bounds.max.y;
                        if (topY < lowestPointY && topY < startPosition.y)
                        {
                            lowestPointY = topY;
                            foundGround = true;
                            hitObjectName = collider.gameObject.name;
                        }
                    }
                }
            }
            
            // Method 3: If still nothing, just find the lowest point in the entire room
            if (!foundGround)
            {
                foreach (var renderer in renderers)
                {
                    float bottomY = renderer.bounds.min.y;
                    if (bottomY < lowestPointY)
                    {
                        lowestPointY = bottomY;
                        foundGround = true;
                        hitObjectName = renderer.gameObject.name;
                    }
                }
            }
            
            if (foundGround && lowestPointY != float.MaxValue)
            {
                // CRITICAL: Add socketHalfHeight offset so the BOTTOM of the socket touches ground
                // Socket center should be ABOVE the ground by half its height
                float targetY = lowestPointY + socketHalfHeight;
                Vector3 newPosition = new Vector3(startPosition.x, targetY, startPosition.z);
                
                Undo.RecordObject(socket.transform, "Snap Socket to Ground");
                socket.transform.position = newPosition;
                
                float distanceMoved = Mathf.Abs(startPosition.y - targetY);
                Debug.Log($"[ConnectionSocket] ✓ Snapped '{socket.gameObject.name}' to ground.\n" +
                         $"• Ground Y: {lowestPointY:F3}\n" +
                         $"• Socket Center Y: {targetY:F3} (offset by {socketHalfHeight:F3} for socket height)\n" +
                         $"• Moved: {distanceMoved:F3} units\n" +
                         $"• Hit: {hitObjectName}");
                
                EditorUtility.SetDirty(socket);
            }
            else
            {
                Debug.LogWarning($"[ConnectionSocket] ✗ No ground found below '{socket.gameObject.name}'!\n" +
                                "Make sure the room has floor mesh/collider geometry.");
            }
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
            
            // Draw door spawn position label if offset is non-zero
            if (socket.DoorSpawnOffset != Vector3.zero)
            {
                Handles.Label(socket.DoorSpawnPosition + Vector3.up * 0.3f, 
                    $"Door Spawn\nOffset: {socket.DoorSpawnOffset.magnitude:F2}u", 
                    EditorStyles.whiteLabel);
            }
        }
    }
}