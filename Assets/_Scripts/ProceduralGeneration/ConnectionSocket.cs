using _Scripts.ProceduralGeneration.Doors;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.ProceduralGeneration
{
    /// <summary>
    /// ConnectionSocket represents a potential doorway connection point in a room prefab.
    /// Used by the procedural generation system to connect rooms together.
    /// Must match door types for compatibility.
    /// Can spawn blockade prefabs if socket remains unconnected after generation.
    /// </summary>
    public class ConnectionSocket : MonoBehaviour
    {
        #region Serialized Fields

        [Header("-- Socket Configuration --")]
        [Tooltip("The type of door that can connect to this socket. Must match Door.DoorType.")]
        [SerializeField] private Door.DoorType _socketType = Door.DoorType.Standard;

        [Tooltip("Is this socket currently connected to another room?")]
        [SerializeField] private bool _isConnected = false;

        [Header("-- Door Prefab (Optional) --")]
        [Tooltip("Optional: Door prefab to instantiate when this socket is connected. Leave empty to use from database.")]
        [SerializeField] private GameObject _doorPrefab;

        [Tooltip("Local position offset for spawning the door. Use this to adjust door pivot alignment.")]
        [SerializeField] private Vector3 _doorSpawnOffset = Vector3.zero;

        [Header("Blockade Prefabs")]
        [Tooltip("Prefabs to spawn if this socket remains unconnected (walls, barriers, etc.)")]
        [SerializeField] private List<GameObject> _blockadePrefabs = new List<GameObject>();

        [Tooltip("Should this socket spawn a blockade if unconnected?")]
        [SerializeField] private bool _spawnBlockadeIfUnconnected = true;

        [Header("-- Visual Debugging --")]
        [Tooltip("Show debug gizmos in scene view?")]
        [SerializeField] private bool _showGizmos = true;

        [Tooltip("Size of the socket opening for gizmo visualization.")]
        [SerializeField] private Vector2 _socketSize = new Vector2(2f, 3f);

        [Tooltip("Show door spawn offset visualization in scene view?")]
        [SerializeField] private bool _showDoorOffsetGizmo = true;

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
        /// Gets or sets the door spawn offset.
        /// </summary>
        public Vector3 DoorSpawnOffset
        {
            get => _doorSpawnOffset;
            set => _doorSpawnOffset = value;
        }

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
        /// Gets the forward direction of this socket (outward from the room).
        /// </summary>
        public Vector3 Forward => transform.forward;

        /// <summary>
        /// Gets the position of this socket.
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Gets the rotation of this socket.
        /// </summary>
        public Quaternion Rotation => transform.rotation;

        /// <summary>
        /// Gets the world position where a door should be spawned (socket position + offset).
        /// </summary>
        public Vector3 DoorSpawnPosition => transform.position + transform.TransformDirection(_doorSpawnOffset);

        /// <summary>
        /// Gets the rotation where a door should be spawned (same as socket rotation).
        /// </summary>
        public Quaternion DoorSpawnRotation => transform.rotation;

        #endregion

        #region Unity Lifecycle

        private void OnValidate()
        {
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            DrawSocketGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;

            DrawSocketGizmo(true);
            DrawDirectionIndicator();
            
            if (_showDoorOffsetGizmo && _doorSpawnOffset != Vector3.zero)
            {
                DrawDoorOffsetIndicator();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if this socket is compatible with a given door type.
        /// Currently exact match required, can be extended for tier compatibility.
        /// </summary>
        public bool IsCompatibleWith(Door.DoorType otherType)
        {
            return _socketType == otherType;
        }

        /// <summary>
        /// Connects this socket to another socket and spawns a door.
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

            _isConnected = true;
            _connectedSocket = otherSocket;
            
            otherSocket._connectedSocket = this;
            otherSocket._isConnected = true;

            GameObject doorToSpawn = doorPrefabOverride ?? _doorPrefab;
            if (doorToSpawn != null)
            {
                _instantiatedDoor = Instantiate(doorToSpawn, DoorSpawnPosition, DoorSpawnRotation);
                _instantiatedDoor.name = $"Door_{gameObject.name}_to_{otherSocket.gameObject.name}";
                _instantiatedDoor.transform.SetParent(transform);

                return _instantiatedDoor;
            }

            return null;
        }

        /// <summary>
        /// Spawns a blockade at this socket if it's unconnected.
        /// Called by FloorGenerator at the end of generation.
        /// </summary>
        /// <param name="parentTransform">Optional parent transform for the blockade</param>
        /// <returns>The instantiated blockade GameObject, or null if none spawned</returns>
        public GameObject SpawnBlockade(Transform parentTransform = null)
        {
            if (_isConnected)
            {
                return null;
            }

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

            if (parentTransform != null)
            {
                _instantiatedBlockade.transform.SetParent(parentTransform);
            }
            else
            {
                _instantiatedBlockade.transform.SetParent(transform);
            }

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

            Vector3 center = transform.position;
            Vector3 right = transform.right * (_socketSize.x / 2f);
            Vector3 up = transform.up * (_socketSize.y / 2f);

            Vector3 topLeft = center - right + up;
            Vector3 topRight = center + right + up;
            Vector3 bottomLeft = center - right - up;
            Vector3 bottomRight = center + right - up;

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);

            if (selected)
            {
                DrawSocketTypeLabel();
            }
        }

        private void DrawDirectionIndicator()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
            
            Vector3 arrowTip = transform.position + transform.forward * 1.5f;
            Vector3 arrowRight = arrowTip - transform.forward * 0.3f + transform.right * 0.2f;
            Vector3 arrowLeft = arrowTip - transform.forward * 0.3f - transform.right * 0.2f;
            
            Gizmos.DrawLine(arrowTip, arrowRight);
            Gizmos.DrawLine(arrowTip, arrowLeft);
        }

        private void DrawDoorOffsetIndicator()
        {
            Gizmos.color = Color.magenta;
            Vector3 offsetPosition = DoorSpawnPosition;
            Gizmos.DrawWireSphere(offsetPosition, 0.2f);
            Gizmos.DrawLine(transform.position, offsetPosition);
        }

        private void DrawSocketTypeLabel()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_socketSize.y / 2f + 0.3f),
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

        #endregion
    }
}