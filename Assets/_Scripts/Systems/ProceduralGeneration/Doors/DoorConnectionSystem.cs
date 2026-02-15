using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Doors
{
    /// <summary>
    /// Handles the connection of rooms via ConnectionSockets.
    /// Calculates target room rotation and position so sockets align face-to-face.
    /// Performs a simple narrow-phase collision check between two specific rooms.
    /// Broad-phase (OccupiedSpaceRegistry) already skips the source room, so
    /// narrow-phase only needs to verify the two rooms being connected don't
    /// excessively overlap beyond the expected door-frame contact area.
    /// </summary>
    public class DoorConnectionSystem : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugLogs = true;

        /// <summary>
        /// Connects two sockets together by aligning their rooms and instantiating a door.
        /// CRITICAL: Apply rotation BEFORE calculating position!
        /// </summary>
        /// <param name="sourceSocket">The socket to connect from (existing room).</param>
        /// <param name="targetSocket">The socket to connect to (new room to be positioned).</param>
        /// <param name="targetRoom">The root transform of the room containing the target socket.</param>
        /// <param name="doorPrefab">Optional door prefab to instantiate at the connection.</param>
        /// <returns>True if connection successful, false otherwise.</returns>
        public bool ConnectRooms(ConnectionSocket sourceSocket, ConnectionSocket targetSocket, Transform targetRoom, GameObject doorPrefab = null)
        {
            if (sourceSocket == null || targetSocket == null || targetRoom == null)
            {
                Debug.LogError("[DoorConnectionSystem] Cannot connect - null parameters provided!");
                return false;
            }

            if (sourceSocket.IsConnected)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[DoorConnectionSystem] Source socket '{sourceSocket.gameObject.name}' is already connected!");
                return false;
            }

            if (!sourceSocket.IsCompatibleWith(targetSocket.SocketType))
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[DoorConnectionSystem] Socket type mismatch! Source: {sourceSocket.SocketType}, Target: {targetSocket.SocketType}");
                return false;
            }

            Quaternion targetRotation = CalculateTargetRoomRotation(sourceSocket, targetSocket, targetRoom);
            Vector3 targetPosition = CalculateTargetRoomPosition(sourceSocket, targetSocket, targetRoom, targetRotation);

            // Narrow-phase collision between source and target is no longer needed.
            // The broad-phase (OccupiedSpaceRegistry) already checks against all placed rooms
            // except the source room, which we intentionally skip to allow door-frame overlap.

            targetRoom.rotation = targetRotation;
            targetRoom.position = targetPosition;

            GameObject door = sourceSocket.ConnectTo(targetSocket, doorPrefab);

            if (_showDebugLogs)
            {
                Debug.Log($"[DoorConnectionSystem] Connected '{sourceSocket.gameObject.name}' to '{targetSocket.gameObject.name}'");
                if (door != null)
                    Debug.Log($"[DoorConnectionSystem] Instantiated door: '{door.name}'");
            }

            return true;
        }

        /// <summary>
        /// Calculates the world position AND rotation for the target room.
        /// Returns both as a tuple for convenience.
        /// </summary>
        public (Vector3 position, Quaternion rotation) CalculateTargetRoomTransform(
            ConnectionSocket sourceSocket,
            ConnectionSocket targetSocket,
            Transform targetRoom)
        {
            Quaternion rotation = CalculateTargetRoomRotation(sourceSocket, targetSocket, targetRoom);
            Vector3 position = CalculateTargetRoomPosition(sourceSocket, targetSocket, targetRoom, rotation);
            return (position, rotation);
        }

        /// <summary>
        /// Calculates the world position for the target room so its socket aligns with the source socket.
        /// MUST be called AFTER calculating the target rotation!
        /// </summary>
        private Vector3 CalculateTargetRoomPosition(
            ConnectionSocket sourceSocket,
            ConnectionSocket targetSocket,
            Transform targetRoom,
            Quaternion targetRotation)
        {
            Vector3 originalPos = targetRoom.position;
            Quaternion originalRot = targetRoom.rotation;

            targetRoom.rotation = targetRotation;

            Vector3 socketOffsetWorld = targetSocket.Position - targetRoom.position;

            targetRoom.position = originalPos;
            targetRoom.rotation = originalRot;

            Vector3 desiredRoomPosition = sourceSocket.Position - socketOffsetWorld;

            return desiredRoomPosition;
        }

        /// <summary>
        /// Calculates the world rotation for the target room so its socket faces opposite the source socket.
        /// </summary>
        private Quaternion CalculateTargetRoomRotation(ConnectionSocket sourceSocket, ConnectionSocket targetSocket, Transform targetRoom)
        {
            Vector3 desiredTargetForward = -sourceSocket.Forward;

            Vector3 currentTargetForward = targetSocket.Forward;

            Quaternion rotationOffset = Quaternion.FromToRotation(currentTargetForward, desiredTargetForward);

            Quaternion desiredRoomRotation = rotationOffset * targetRoom.rotation;

            return desiredRoomRotation;
        }

        /// <summary>
        /// Disconnects two sockets and optionally destroys the door between them.
        /// </summary>
        public void DisconnectSockets(ConnectionSocket socket1, ConnectionSocket socket2)
        {
            if (socket1 != null)
                socket1.Disconnect();

            if (socket2 != null)
                socket2.Disconnect();

            if (_showDebugLogs)
                Debug.Log($"[DoorConnectionSystem] Disconnected sockets");
        }
    }
}
