using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Doors
{
    public class ConnectionSocket : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private Door.DoorType _socketType = Door.DoorType.Standard;
        [SerializeField] private bool _isConnected;
        [Range(0f, 360f)]
        [SerializeField] private float _forwardAngleOffset;
        [SerializeField] private Vector3 _boundsCenter = Vector3.zero;
        [SerializeField] private Vector3 _boundsSize = Vector3.zero;
        [SerializeField] private GameObject _doorPrefab;
        [SerializeField] private Transform _doorSpawnPoint;
        [SerializeField] private List<GameObject> _blockadePrefabs = new List<GameObject>();
        [SerializeField] private bool _spawnBlockadeIfUnconnected = true;
        [SerializeField] private bool _showGizmos = false;

        #endregion

        #region Private Fields

        private GameObject _instantiatedDoor;
        private GameObject _instantiatedBlockade;
        private ConnectionSocket _connectedSocket;

        #endregion

        #region Public Properties

        public Door.DoorType SocketType
        {
            get => _socketType;
            set => _socketType = value;
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => _isConnected = value;
        }

        public GameObject DoorPrefab => _doorPrefab;

        public List<GameObject> BlockadePrefabs => _blockadePrefabs;

        public bool SpawnBlockadeIfUnconnected => _spawnBlockadeIfUnconnected;

        public GameObject InstantiatedDoor => _instantiatedDoor;

        public GameObject InstantiatedBlockade => _instantiatedBlockade;

        public ConnectionSocket ConnectedSocket => _connectedSocket;

        public Vector3 Forward => Quaternion.AngleAxis(_forwardAngleOffset, Vector3.up) * transform.forward;

        public Vector3 Position
        {
            get
            {
                if (_boundsSize.sqrMagnitude < 0.001f)
                    return transform.position;

                return transform.TransformPoint(_boundsCenter);
            }
        }

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

        public Quaternion Rotation => Quaternion.AngleAxis(_forwardAngleOffset, Vector3.up) * transform.rotation;

        public Transform DoorSpawnPointTransform => _doorSpawnPoint;

        public Vector3 DoorSpawnPosition =>
            _doorSpawnPoint != null ? _doorSpawnPoint.position : Position;

        public Quaternion DoorSpawnRotation =>
            _doorSpawnPoint != null ? _doorSpawnPoint.rotation : Rotation;

        public Vector3 BoundsCenter => _boundsCenter;

        public Vector3 BoundsSize => _boundsSize;

        public bool HasBounds => _boundsSize.sqrMagnitude > 0.001f;

        #endregion

        #region Unity Lifecycle

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            DrawForwardArrow();
        }

        #endregion

        #region Public Methods

        public bool IsCompatibleWith(Door.DoorType otherType)
        {
            return _socketType == otherType;
        }

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

        private void DrawForwardArrow()
        {
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

            // If angle offset is non-zero, show raw transform.forward in grey for reference
            if (_forwardAngleOffset > 0.1f)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                Gizmos.DrawRay(center, transform.forward * 1f);
            }
        }

        #endregion
    }
}
