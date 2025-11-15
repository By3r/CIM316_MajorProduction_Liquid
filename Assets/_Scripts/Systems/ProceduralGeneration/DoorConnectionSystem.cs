using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Handles the connection of rooms via ConnectionSockets.
    /// Performs NARROW-PHASE collision detection between two specific rooms.
    /// Uses TIGHT bounds with socket-level overlap exception.
    /// Does NOT use the registry - that's for BROAD-PHASE checks in FloorGenerator.
    /// </summary>
    public class DoorConnectionSystem : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugLogs = true;

        [Header("Narrow-Phase Collision Settings")]
        [Tooltip("Allow minimal overlap at socket connection points")]
        [SerializeField] private bool _allowSocketOverlap = true;

        [Tooltip("Maximum overlap volume allowed at socket connections (in cubic units)")]
        [SerializeField] private float _socketOverlapThreshold = 0.5f;

        [Tooltip("Maximum distance from socket center to consider overlap as 'at socket'")]
        [SerializeField] private float _socketProximityThreshold = 0.3f;

        /// <summary>
        /// Connects two sockets together by aligning their rooms and instantiating a door.
        /// Performs NARROW-PHASE collision check between the two specific rooms.
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

            BoundsChecker sourceBounds = sourceSocket.GetComponentInParent<BoundsChecker>();
            BoundsChecker targetBounds = targetRoom.GetComponent<BoundsChecker>();

            if (WouldConnectionCauseCollision(sourceBounds, targetBounds, targetPosition, targetRotation, sourceSocket, targetSocket))
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[DoorConnectionSystem] Narrow-phase collision detected between '{sourceSocket.transform.root.name}' and '{targetRoom.name}'. Connection aborted.");
                return false;
            }

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
        /// NARROW-PHASE COLLISION CHECK between two specific rooms.
        /// Uses TIGHT bounds with socket-level overlap exception.
        /// Now supports intentional overlap from negative padding!
        /// </summary>
        private bool WouldConnectionCauseCollision(
            BoundsChecker sourceBounds,
            BoundsChecker targetBounds,
            Vector3 targetPosition,
            Quaternion targetRotation,
            ConnectionSocket sourceSocket,
            ConnectionSocket targetSocket)
        {
            if (sourceBounds == null || targetBounds == null)
            {
                Debug.LogWarning("[DoorConnectionSystem] Missing BoundsChecker on one or both rooms. Skipping narrow-phase check.");
                return false;
            }

            Bounds sourceBoundsWorld = sourceBounds.GetCollisionBounds(allowSocketOverlap: true);

            Vector3 originalPos = targetBounds.transform.position;
            Quaternion originalRot = targetBounds.transform.rotation;

            targetBounds.transform.position = targetPosition;
            targetBounds.transform.rotation = targetRotation;

            Bounds targetBoundsWorld = targetBounds.GetCollisionBounds(allowSocketOverlap: true);

            targetBounds.transform.position = originalPos;
            targetBounds.transform.rotation = originalRot;

            if (!sourceBoundsWorld.Intersects(targetBoundsWorld))
            {
                return false;
            }
            
            if (_allowSocketOverlap && IsAcceptableOverlap(
                sourceBoundsWorld, targetBoundsWorld, 
                sourceBounds, targetBounds,
                sourceSocket, targetSocket))
            {
                if (_showDebugLogs)
                    Debug.Log($"[DoorConnectionSystem] Acceptable overlap detected during narrow-phase check.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if overlap is acceptable (socket-level or intentional from negative padding).
        /// </summary>
        private bool IsAcceptableOverlap(
            Bounds boundsA, Bounds boundsB,
            BoundsChecker checkerA, BoundsChecker checkerB,
            ConnectionSocket sourceSocket, ConnectionSocket targetSocket)
        {
            Bounds intersection = GetBoundsIntersection(boundsA, boundsB);
            
            if (intersection.size == Vector3.zero)
                return false;

            float intersectionVolume = intersection.size.x * intersection.size.y * intersection.size.z;
            
            if (intersectionVolume < _socketOverlapThreshold)
            {
                Vector3 socketConnectionPoint = (sourceSocket.Position + targetSocket.Position) * 0.5f;
                float distanceToSocket = Vector3.Distance(intersection.center, socketConnectionPoint);
                
                if (distanceToSocket < _socketProximityThreshold)
                {
                    return true;
                }
            }

            Bounds sourcePadded = checkerA.GetPaddedBounds();
            Bounds targetPadded = checkerB.GetPaddedBounds();

            if (!sourcePadded.Intersects(targetPadded))
            {
                if (_showDebugLogs)
                    Debug.Log($"[DoorConnectionSystem] Intentional overlap from negative padding detected (PADDED don't intersect, TIGHT do).");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates the intersection of two bounds.
        /// Returns a bounds with zero size if no intersection exists.
        /// </summary>
        private Bounds GetBoundsIntersection(Bounds a, Bounds b)
        {
            Vector3 min = Vector3.Max(a.min, b.min);
            Vector3 max = Vector3.Min(a.max, b.max);
            
            if (min.x > max.x || min.y > max.y || min.z > max.z)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }
            
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            
            return new Bounds(center, size);
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