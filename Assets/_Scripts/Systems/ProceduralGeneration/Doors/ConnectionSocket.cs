using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Doors
{
    /// <summary>
    /// ConnectionSocket represents a doorway connection point in a room prefab.
    /// Attach this directly to a door frame wall piece (the mesh with the hole).
    /// The connection point is the geometric center of this object's renderers,
    /// so rooms align at doorway centers regardless of mesh pivot placement.
    /// A child DoorSpawnPoint Transform marks where the door prefab instantiates.
    /// </summary>
    public class ConnectionSocket : MonoBehaviour
    {
        #region Serialized Fields

        [Header("-- Socket Configuration --")]
        [Tooltip("The type of door that can connect to this socket. Must match Door.DoorType.")]
        [SerializeField] private Door.DoorType _socketType = Door.DoorType.Standard;

        [Tooltip("Is this socket currently connected to another room?")]
        [SerializeField] private bool _isConnected;

        [Header("-- Forward Direction --")]
        [Tooltip("Angle offset (degrees) to correct the forward direction if the door frame model doesn't face outward. " +
                 "Rotates around the Y axis. 0 = use transform.forward as-is.")]
        [Range(0f, 360f)]
        [SerializeField] private float _forwardAngleOffset;

        [Header("-- Socket Bounds --")]
        [Tooltip("Local-space center of this door frame's geometry. Calculated from renderers. " +
                 "This is the actual connection point where rooms meet.")]
        [SerializeField] private Vector3 _boundsCenter = Vector3.zero;

        [Tooltip("Local-space size of this door frame's geometry.")]
        [SerializeField] private Vector3 _boundsSize = Vector3.zero;

        [Header("-- Door Spawn --")]
        [Tooltip("Optional: Door prefab to instantiate when connected. Leave empty to use from database.")]
        [SerializeField] private GameObject _doorPrefab;

        [Tooltip("Child Transform marking where the door spawns. If not assigned, uses the socket's center.")]
        [SerializeField] private Transform _doorSpawnPoint;

        [Header("-- Blockade Prefabs --")]
        [Tooltip("Prefabs to spawn if this socket remains unconnected (walls, barriers, etc.)")]
        [SerializeField] private List<GameObject> _blockadePrefabs = new List<GameObject>();

        [Tooltip("Should this socket spawn a blockade if unconnected?")]
        [SerializeField] private bool _spawnBlockadeIfUnconnected = true;

        [Header("-- Visual Debugging --")]
        [Tooltip("Show debug gizmos in scene view?")]
        [SerializeField] private bool _showGizmos = true;

        #endregion

        #region Private Fields

        private GameObject _instantiatedDoor;
        private GameObject _instantiatedBlockade;
        private ConnectionSocket _connectedSocket;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the socket type (door tier).
        /// </summary>
        public Door.DoorType SocketType
        {
            get => _socketType;
            set => _socketType = value;
        }

        /// <summary>
        /// Gets or sets whether this socket is connected.
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => _isConnected = value;
        }

        /// <summary>
        /// Gets the door prefab assigned to this socket.
        /// </summary>
        public GameObject DoorPrefab => _doorPrefab;

        /// <summary>
        /// Gets the list of blockade prefabs assigned to this socket.
        /// </summary>
        public List<GameObject> BlockadePrefabs => _blockadePrefabs;

        /// <summary>
        /// Gets whether this socket should spawn a blockade if unconnected.
        /// </summary>
        public bool SpawnBlockadeIfUnconnected => _spawnBlockadeIfUnconnected;

        /// <summary>
        /// Gets the instantiated door GameObject (if any).
        /// </summary>
        public GameObject InstantiatedDoor => _instantiatedDoor;

        /// <summary>
        /// Gets the instantiated blockade GameObject (if any).
        /// </summary>
        public GameObject InstantiatedBlockade => _instantiatedBlockade;

        /// <summary>
        /// Gets the socket this socket is connected to (if any).
        /// </summary>
        public ConnectionSocket ConnectedSocket => _connectedSocket;

        /// <summary>
        /// Gets the forward direction of this socket, with angle offset applied.
        /// This is the direction pointing OUTWARD from the room through the doorway.
        /// </summary>
        public Vector3 Forward => Quaternion.AngleAxis(_forwardAngleOffset, Vector3.up) * transform.forward;

        /// <summary>
        /// Gets the world-space connection point of this socket.
        /// Uses the geometric center of the door frame (from cached bounds).
        /// Falls back to transform.position if bounds haven't been calculated.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                if (_boundsSize.sqrMagnitude < 0.001f)
                    return transform.position;

                return transform.TransformPoint(_boundsCenter);
            }
        }

        /// <summary>
        /// Gets the local-space connection point relative to the room root.
        /// Used by FloorGenerator for placement offset calculations.
        /// </summary>
        public Vector3 LocalPosition
        {
            get
            {
                if (_boundsSize.sqrMagnitude < 0.001f)
                    return transform.localPosition;

                // Transform bounds center from socket-local to room-root-local
                Vector3 worldCenter = transform.TransformPoint(_boundsCenter);
                Transform roomRoot = transform.root;
                return roomRoot.InverseTransformPoint(worldCenter);
            }
        }

        /// <summary>
        /// Gets the rotation of this socket, with forward angle offset applied.
        /// </summary>
        public Quaternion Rotation => Quaternion.AngleAxis(_forwardAngleOffset, Vector3.up) * transform.rotation;

        /// <summary>
        /// Gets the DoorSpawnPoint child Transform (if assigned).
        /// </summary>
        public Transform DoorSpawnPointTransform => _doorSpawnPoint;

        /// <summary>
        /// Gets the world position where a door should be spawned.
        /// Uses the DoorSpawnPoint child if assigned, otherwise falls back to the socket center.
        /// </summary>
        public Vector3 DoorSpawnPosition =>
            _doorSpawnPoint != null ? _doorSpawnPoint.position : Position;

        /// <summary>
        /// Gets the rotation for door spawning.
        /// Uses the DoorSpawnPoint child if assigned, otherwise uses socket rotation with offset.
        /// </summary>
        public Quaternion DoorSpawnRotation =>
            _doorSpawnPoint != null ? _doorSpawnPoint.rotation : Rotation;

        /// <summary>
        /// Gets the local-space bounds center of this socket's door frame geometry.
        /// </summary>
        public Vector3 BoundsCenter => _boundsCenter;

        /// <summary>
        /// Gets the local-space bounds size of this socket's door frame geometry.
        /// </summary>
        public Vector3 BoundsSize => _boundsSize;

        /// <summary>
        /// Returns true if socket bounds have been calculated (size > 0).
        /// </summary>
        public bool HasBounds => _boundsSize.sqrMagnitude > 0.001f;

        #endregion

        #region Unity Lifecycle

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            DrawSocketGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;

            DrawSocketGizmo(true);
            DrawBoundsGizmo();
            DrawDirectionIndicator();
            DrawDoorSpawnPointIndicator();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if this socket is compatible with a given door type.
        /// Currently exact match required.
        /// </summary>
        public bool IsCompatibleWith(Door.DoorType otherType)
        {
            return _socketType == otherType;
        }

        /// <summary>
        /// Calculates the local-space bounds center and size from child renderers.
        /// This determines the geometric center of the door frame piece,
        /// which becomes the connection alignment point.
        /// </summary>
        public void CalculateBoundsFromRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[ConnectionSocket] No renderers found on '{gameObject.name}'. Cannot calculate bounds.");
                _boundsCenter = Vector3.zero;
                _boundsSize = Vector3.zero;
                return;
            }

            // Calculate bounds in local space (relative to this socket's transform)
            Bounds localBounds = new Bounds(
                transform.InverseTransformPoint(renderers[0].bounds.center),
                Vector3.zero);

            foreach (Renderer renderer in renderers)
            {
                Vector3 localMin = transform.InverseTransformPoint(renderer.bounds.min);
                Vector3 localMax = transform.InverseTransformPoint(renderer.bounds.max);
                localBounds.Encapsulate(localMin);
                localBounds.Encapsulate(localMax);
            }

            _boundsCenter = localBounds.center;
            _boundsSize = localBounds.size;

            Debug.Log($"[ConnectionSocket] Calculated bounds for '{gameObject.name}': " +
                      $"Center={_boundsCenter}, Size={_boundsSize}");
        }

        /// <summary>
        /// Connects this socket to another socket and spawns a door.
        /// Only the source socket spawns the door (one door per pair).
        /// Returns the instantiated door GameObject.
        /// </summary>
        public GameObject ConnectTo(ConnectionSocket otherSocket, GameObject doorPrefabOverride = null)
        {
            if (otherSocket == null)
            {
                Debug.LogError($"[ConnectionSocket] Cannot connect socket '{gameObject.name}' - other socket is null!");
                return null;
            }

            if (_isConnected)
            {
                Debug.LogWarning($"[ConnectionSocket] Socket '{gameObject.name}' is already connected!");
                return _instantiatedDoor;
            }

            if (!IsCompatibleWith(otherSocket.SocketType))
            {
                Debug.LogWarning($"[ConnectionSocket] Socket type mismatch! {SocketType} != {otherSocket.SocketType}");
                return null;
            }

            // Mark both sockets as connected
            _isConnected = true;
            _connectedSocket = otherSocket;

            otherSocket._connectedSocket = this;
            otherSocket._isConnected = true;

            // Spawn door at the DoorSpawnPoint (only source socket spawns the door)
            GameObject doorToSpawn = doorPrefabOverride ?? _doorPrefab;
            if (doorToSpawn != null)
            {
                _instantiatedDoor = Instantiate(doorToSpawn, DoorSpawnPosition, DoorSpawnRotation);
                _instantiatedDoor.name = $"Door_{gameObject.name}_to_{otherSocket.gameObject.name}";

                // Preserve the prefab's original scale — SetParent can distort it
                // if the parent hierarchy has non-uniform or non-1 scale.
                Vector3 prefabScale = doorToSpawn.transform.localScale;
                _instantiatedDoor.transform.SetParent(transform);
                _instantiatedDoor.transform.localScale = prefabScale;

                return _instantiatedDoor;
            }

            return null;
        }

        /// <summary>
        /// Spawns a blockade at this socket if it's unconnected.
        /// Called by FloorGenerator at the end of generation.
        /// </summary>
        public GameObject SpawnBlockade(Transform parentTransform = null)
        {
            if (_isConnected)
            {
                return null;
            }

            // Check if the connected socket already has a door
            if (_connectedSocket != null)
            {
                foreach (Transform child in _connectedSocket.transform)
                {
                    if (child.name.Contains("Door_"))
                    {
                        _isConnected = true;
                        _instantiatedDoor = child.gameObject;
                        return null;
                    }
                }

                _isConnected = true;
                return null;
            }

            if (_instantiatedDoor != null)
            {
                _isConnected = true;
                return null;
            }

            // Check for existing door children
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Door_"))
                {
                    _isConnected = true;
                    _instantiatedDoor = child.gameObject;
                    return null;
                }
            }

            if (!_spawnBlockadeIfUnconnected || _blockadePrefabs == null || _blockadePrefabs.Count == 0)
            {
                return null;
            }

            if (_instantiatedBlockade != null)
            {
                return _instantiatedBlockade;
            }

            GameObject blockadePrefab = _blockadePrefabs[Random.Range(0, _blockadePrefabs.Count)];

            if (blockadePrefab == null)
            {
                Debug.LogWarning($"[ConnectionSocket] Blockade prefab is null for socket '{gameObject.name}'!");
                return null;
            }

            Quaternion finalRotation = transform.rotation * blockadePrefab.transform.localRotation;

            Vector3 rotatedOffset = transform.rotation * blockadePrefab.transform.localPosition;
            Vector3 finalPosition = transform.position + rotatedOffset;

            _instantiatedBlockade = Instantiate(blockadePrefab, finalPosition, finalRotation);
            _instantiatedBlockade.name = $"Blockade_{gameObject.name}";

            Vector3 blockadeScale = blockadePrefab.transform.localScale;
            if (parentTransform != null)
            {
                _instantiatedBlockade.transform.SetParent(parentTransform);
            }
            else
            {
                _instantiatedBlockade.transform.SetParent(transform);
            }
            _instantiatedBlockade.transform.localScale = blockadeScale;

            return _instantiatedBlockade;
        }

        /// <summary>
        /// Disconnects this socket and destroys any spawned door or blockade.
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            _connectedSocket = null;

            if (_instantiatedDoor != null)
            {
                if (Application.isPlaying)
                    Destroy(_instantiatedDoor);
                else
                    DestroyImmediate(_instantiatedDoor);

                _instantiatedDoor = null;
            }

            if (_instantiatedBlockade != null)
            {
                if (Application.isPlaying)
                    Destroy(_instantiatedBlockade);
                else
                    DestroyImmediate(_instantiatedBlockade);

                _instantiatedBlockade = null;
            }
        }

        #endregion

        #region Gizmo Drawing

        private void DrawSocketGizmo(bool selected = false)
        {
            Color socketColor = _isConnected ? Color.green : Color.yellow;
            if (selected) socketColor = Color.cyan;
            socketColor.a = selected ? 0.8f : 0.5f;

            Gizmos.color = socketColor;

            // Draw sphere at the connection point (bounds center, not pivot)
            Gizmos.DrawWireSphere(Position, 0.15f);
        }

        private void DrawBoundsGizmo()
        {
            if (!HasBounds) return;

            // Draw the socket's local bounds in orange
            Matrix4x4 original = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.DrawWireCube(_boundsCenter, _boundsSize);

            // Draw a solid sphere at the center for clarity
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.DrawSphere(_boundsCenter, 0.08f);

            Gizmos.matrix = original;

            // Draw line from pivot to center if they differ
            if (Vector3.Distance(transform.position, Position) > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
                Gizmos.DrawLine(transform.position, Position);
            }
        }

        private void DrawDirectionIndicator()
        {
            // Draw the adjusted forward direction from the connection point
            Vector3 center = Position;
            Vector3 adjustedForward = Forward;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(center, adjustedForward * 1.5f);

            Vector3 arrowTip = center + adjustedForward * 1.5f;
            Vector3 right = Vector3.Cross(Vector3.up, adjustedForward).normalized;
            Vector3 arrowRight = arrowTip - adjustedForward * 0.3f + right * 0.2f;
            Vector3 arrowLeft = arrowTip - adjustedForward * 0.3f - right * 0.2f;

            Gizmos.DrawLine(arrowTip, arrowRight);
            Gizmos.DrawLine(arrowTip, arrowLeft);

            // If angle offset is non-zero, also draw the raw transform.forward in grey for reference
            if (_forwardAngleOffset > 0.1f)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                Gizmos.DrawRay(center, transform.forward * 1f);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                center + Vector3.up * 0.5f,
                $"{_socketType}\n{(_isConnected ? "Connected" : "Available")}",
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }
            );
#endif
        }

        private void DrawDoorSpawnPointIndicator()
        {
            if (_doorSpawnPoint == null) return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_doorSpawnPoint.position, 0.12f);
            Gizmos.DrawLine(Position, _doorSpawnPoint.position);

            // Show door spawn forward direction
            Gizmos.color = new Color(1f, 0f, 1f, 0.6f);
            Gizmos.DrawRay(_doorSpawnPoint.position, _doorSpawnPoint.forward * 0.8f);
        }

        #endregion
    }
}
