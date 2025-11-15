using System.Collections.Generic;
using _Scripts.ProceduralGeneration;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Manages room bounds calculation and ConnectionSocket positioning.
    /// Provides tools for automatic bounds detection, padding, and socket offset adjustment.
    /// Automatically registers with OccupiedSpaceRegistry for BROAD-PHASE collision detection.
    /// Registry stores PADDED bounds for personal space checking.
    /// </summary>
    public class BoundsChecker : MonoBehaviour
    {
        [Header("Registry Settings")]
        [Tooltip("Automatically register this room with the OccupiedSpaceRegistry")]
        [SerializeField] private bool _autoRegisterWithRegistry = true;

        [Tooltip("Register on Start (recommended for runtime generation)")]
        [SerializeField] private bool _registerOnStart = true;

        private bool _isRegistered = false;

        [Header("Bounds Settings")]
        [Tooltip("The center of the bounds in local space")]
        [SerializeField] private Vector3 _boundsCenter = Vector3.zero;

        [Tooltip("The size of the bounds")]
        [SerializeField] private Vector3 _boundsSize = Vector3.one * 10f;

        [Header("Padding Settings")]
        [Tooltip("Use uniform padding on all axes, or configure per axis")]
        [SerializeField] private bool _useUniformPadding = true;

        [Tooltip("Uniform padding applied to all axes")]
        [SerializeField] private float _uniformPadding = 0.5f;

        [Tooltip("Per-axis padding (only used if Uniform Padding is disabled)")]
        [SerializeField] private Vector3 _axisBasedPadding = Vector3.one * 0.5f;

        [Header("Socket Offset Settings")]
        [Tooltip("Distance to move sockets outward from the nearest bounds face")]
        [SerializeField] private float _socketOffsetDistance = 0.1f;

        [Header("Collision Settings")]
        [Tooltip("Use tight bounds (no padding) for collision detection at sockets")]
        [SerializeField] private bool _useTightCollisionBounds = true;

        [Tooltip("Additional padding to remove near socket areas for collision (negative value to allow overlap)")]
        [SerializeField] private float _socketCollisionTolerance = 0.1f;

        [Header("Gizmo Settings")]
        [Tooltip("Show bounds gizmos in Scene view")]
        [SerializeField] private bool _showGizmos = true;

        [Tooltip("Color for actual bounds")]
        [SerializeField] private Color _boundsColor = Color.green;

        [Tooltip("Color for padded bounds")]
        [SerializeField] private Color _paddedBoundsColor = Color.yellow;

        [Tooltip("Color for collision bounds")]
        [SerializeField] private Color _collisionBoundsColor = Color.cyan;

        [Header("Debug Info")]
        [SerializeField] private List<ConnectionSocket> _cachedSockets = new();
        [SerializeField] private List<Vector3> _socketPositionsBeforeAdjustment = new();

        private void Start()
        {
            if (_registerOnStart && _autoRegisterWithRegistry)
            {
                RegisterWithRegistry();
            }
        }

        private void OnEnable()
        {
            if (_isRegistered && _autoRegisterWithRegistry)
            {
                RegisterWithRegistry();
            }
        }

        private void OnDisable()
        {
            if (_isRegistered && _autoRegisterWithRegistry)
            {
                UnregisterFromRegistry();
            }
        }

        private void OnDestroy()
        {
            UnregisterFromRegistry();
        }

        /// <summary>
        /// Manually register this room with the OccupiedSpaceRegistry.
        /// Called automatically if _autoRegisterWithRegistry is true.
        /// Registry stores PADDED bounds for broad-phase collision detection.
        /// </summary>
        public void RegisterWithRegistry()
        {
            if (_isRegistered)
            {
                Debug.LogWarning($"[BoundsChecker] Room '{gameObject.name}' is already registered!");
                return;
            }

            OccupiedSpaceRegistry.Instance.RegisterOccupiedSpace(this, transform);
            _isRegistered = true;
        }

        /// <summary>
        /// Manually unregister this room from the OccupiedSpaceRegistry.
        /// </summary>
        public void UnregisterFromRegistry()
        {
            if (!_isRegistered) return;

            if (OccupiedSpaceRegistry.Instance != null)
            {
                OccupiedSpaceRegistry.Instance.UnregisterOccupiedSpace(this);
            }
            _isRegistered = false;
        }

        /// <summary>
        /// Checks if this room is currently registered with the registry.
        /// </summary>
        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// Gets the actual bounds (without padding) in world space.
        /// Use this for visual representation and socket positioning.
        /// </summary>
        public Bounds GetBounds()
        {
            return new Bounds(
                transform.TransformPoint(_boundsCenter),
                Vector3.Scale(_boundsSize, transform.lossyScale)
            );
        }

        /// <summary>
        /// Gets the padded bounds in world space at current transform.
        /// Use this for BROAD-PHASE collision detection in registry.
        /// </summary>
        public Bounds GetPaddedBounds()
        {
            Vector3 padding = _useUniformPadding 
                ? Vector3.one * _uniformPadding 
                : _axisBasedPadding;

            Vector3 paddedSize = _boundsSize + (padding * 2f);

            return new Bounds(
                transform.TransformPoint(_boundsCenter),
                Vector3.Scale(paddedSize, transform.lossyScale)
            );
        }

        /// <summary>
        /// Gets the padded bounds at a SPECIFIC position and rotation.
        /// CRITICAL METHOD for FloorGenerator's broad-phase check.
        /// Allows checking if a room CAN be placed before instantiating it.
        /// </summary>
        /// <param name="worldPosition">The position to calculate bounds at</param>
        /// <param name="worldRotation">The rotation to calculate bounds at</param>
        /// <returns>Padded bounds at the specified transform</returns>
        public Bounds GetPaddedBounds(Vector3 worldPosition, Quaternion worldRotation)
        {
            Vector3 padding = _useUniformPadding 
                ? Vector3.one * _uniformPadding 
                : _axisBasedPadding;

            Vector3 paddedSize = _boundsSize + (padding * 2f);

            Matrix4x4 trs = Matrix4x4.TRS(worldPosition, worldRotation, transform.lossyScale);
            Vector3 worldCenter = trs.MultiplyPoint3x4(_boundsCenter);
            Vector3 worldSize = Vector3.Scale(paddedSize, transform.lossyScale);

            return new Bounds(worldCenter, worldSize);
        }

        /// <summary>
        /// Gets bounds for collision detection during room placement.
        /// This is for NARROW-PHASE checks (TIGHT bounds with socket overlap allowed).
        /// </summary>
        /// <param name="allowSocketOverlap">If true, uses tighter bounds to allow socket areas to overlap slightly</param>
        public Bounds GetCollisionBounds(bool allowSocketOverlap = true)
        {
            if (allowSocketOverlap && _useTightCollisionBounds)
            {
                return GetBounds();
            }
            
            return GetPaddedBounds();
        }

        /// <summary>
        /// Checks if a world point is near any socket within a given tolerance.
        /// </summary>
        public bool IsPointNearSocket(Vector3 worldPoint, float tolerance = 0.5f)
        {
            if (_cachedSockets.Count == 0)
                CacheConnectionSockets();

            foreach (ConnectionSocket socket in _cachedSockets)
            {
                if (socket == null) continue;

                float distance = Vector3.Distance(worldPoint, socket.Position);
                if (distance <= tolerance)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the nearest ConnectionSocket to a given world point.
        /// </summary>
        public ConnectionSocket GetNearestSocket(Vector3 worldPoint)
        {
            if (_cachedSockets.Count == 0)
                CacheConnectionSockets();

            ConnectionSocket nearest = null;
            float minDistance = float.MaxValue;

            foreach (ConnectionSocket socket in _cachedSockets)
            {
                if (socket == null) continue;

                float distance = Vector3.Distance(worldPoint, socket.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = socket;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Calculates bounds automatically from all child Renderers, excluding ConnectionSockets.
        /// </summary>
        public void CalculateBoundsFromRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[BoundsChecker] No renderers found on '{gameObject.name}'. Cannot calculate bounds.");
                return;
            }

            List<Renderer> validRenderers = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer.GetComponentInParent<ConnectionSocket>() != null)
                    continue;

                validRenderers.Add(renderer);
            }

            if (validRenderers.Count == 0)
            {
                Debug.LogWarning($"[BoundsChecker] All renderers are part of ConnectionSockets. Cannot calculate bounds.");
                return;
            }

            Bounds localBounds = new Bounds(
                transform.InverseTransformPoint(validRenderers[0].bounds.center),
                Vector3.zero
            );

            foreach (Renderer renderer in validRenderers)
            {
                Bounds rendererBounds = renderer.bounds;
                Vector3 localMin = transform.InverseTransformPoint(rendererBounds.min);
                Vector3 localMax = transform.InverseTransformPoint(rendererBounds.max);

                localBounds.Encapsulate(localMin);
                localBounds.Encapsulate(localMax);
            }

            _boundsCenter = localBounds.center;
            _boundsSize = localBounds.size;

            Debug.Log($"[BoundsChecker] Calculated bounds for '{gameObject.name}': Center={_boundsCenter}, Size={_boundsSize}");

            if (_isRegistered && OccupiedSpaceRegistry.Instance != null)
            {
                var occupiedSpace = OccupiedSpaceRegistry.Instance.GetOccupiedSpace(this);
                if (occupiedSpace != null)
                {
                    occupiedSpace.UpdateBounds();
                    Debug.Log($"[BoundsChecker] Updated PADDED bounds in registry for '{gameObject.name}'");
                }
            }
        }

        /// <summary>
        /// Finds and caches all ConnectionSocket components in children.
        /// </summary>
        public void CacheConnectionSockets()
        {
            _cachedSockets.Clear();
            _cachedSockets.AddRange(GetComponentsInChildren<ConnectionSocket>());

            Debug.Log($"[BoundsChecker] Cached {_cachedSockets.Count} ConnectionSockets on '{gameObject.name}'");
        }

        /// <summary>
        /// Adjusts all cached socket positions to be on the nearest bounds face, then offset outward.
        /// This ensures sockets are always positioned correctly at bounds edges + offset.
        /// </summary>
        public void AdjustSocketPositions()
        {
            if (_cachedSockets.Count == 0)
            {
                Debug.LogWarning($"[BoundsChecker] No cached sockets found. Run 'Cache Sockets' first.");
                return;
            }

            _socketPositionsBeforeAdjustment.Clear();
            foreach (ConnectionSocket socket in _cachedSockets)
            {
                if (socket != null)
                    _socketPositionsBeforeAdjustment.Add(socket.transform.position);
            }

            Bounds bounds = GetBounds();
            Vector3 boundsMin = _boundsCenter - _boundsSize * 0.5f;
            Vector3 boundsMax = _boundsCenter + _boundsSize * 0.5f;

            int adjusted = 0;

            foreach (ConnectionSocket socket in _cachedSockets)
            {
                if (socket == null) continue;

                Vector3 localSocketPos = socket.transform.localPosition;

                (Vector3 facePosition, Vector3 faceNormal) = FindClosestBoundsFace(localSocketPos, boundsMin, boundsMax);

                socket.transform.localPosition = facePosition;

                socket.transform.localPosition += faceNormal * _socketOffsetDistance;

                adjusted++;
            }

            Debug.Log($"[BoundsChecker] Snapped {adjusted} sockets to bounds faces with {_socketOffsetDistance} unit offset.");
        }

        /// <summary>
        /// Finds the closest bounds face to a given local position.
        /// Returns the position on that face and the face's outward normal.
        /// </summary>
        private (Vector3 facePosition, Vector3 faceNormal) FindClosestBoundsFace(Vector3 localPoint, Vector3 boundsMin, Vector3 boundsMax)
        {
            float distToMinX = Mathf.Abs(localPoint.x - boundsMin.x);
            float distToMaxX = Mathf.Abs(localPoint.x - boundsMax.x);
            float distToMinY = Mathf.Abs(localPoint.y - boundsMin.y);
            float distToMaxY = Mathf.Abs(localPoint.y - boundsMax.y);
            float distToMinZ = Mathf.Abs(localPoint.z - boundsMin.z);
            float distToMaxZ = Mathf.Abs(localPoint.z - boundsMax.z);

            float minDist = Mathf.Min(distToMinX, distToMaxX, distToMinY, distToMaxY, distToMinZ, distToMaxZ);

            Vector3 facePosition = localPoint;
            Vector3 faceNormal = Vector3.zero;

            if (minDist == distToMinX)
            {
                facePosition.x = boundsMin.x;
                faceNormal = Vector3.left;
            }
            else if (minDist == distToMaxX)
            {
                facePosition.x = boundsMax.x;
                faceNormal = Vector3.right;
            }
            else if (minDist == distToMinY)
            {
                facePosition.y = boundsMin.y;
                faceNormal = Vector3.down;
            }
            else if (minDist == distToMaxY)
            {
                facePosition.y = boundsMax.y;
                faceNormal = Vector3.up;
            }
            else if (minDist == distToMinZ)
            {
                facePosition.z = boundsMin.z;
                faceNormal = Vector3.back;
            }
            else if (minDist == distToMaxZ)
            {
                facePosition.z = boundsMax.z;
                faceNormal = Vector3.forward;
            }

            return (facePosition, faceNormal);
        }

        /// <summary>
        /// Clears the stored "before adjustment" positions for sockets.
        /// </summary>
        public void ClearBeforePositions()
        {
            _socketPositionsBeforeAdjustment.Clear();
            Debug.Log($"[BoundsChecker] Cleared socket 'before' positions.");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            Matrix4x4 originalMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = _boundsColor;
            Gizmos.DrawWireCube(_boundsCenter, _boundsSize);

            Gizmos.color = _paddedBoundsColor;
            Vector3 padding = _useUniformPadding 
                ? Vector3.one * _uniformPadding 
                : _axisBasedPadding;
            Vector3 paddedSize = _boundsSize + (padding * 2f);
            Gizmos.DrawWireCube(_boundsCenter, paddedSize);

            if (_useTightCollisionBounds)
            {
                Gizmos.color = _collisionBoundsColor;
                Gizmos.DrawWireCube(_boundsCenter, _boundsSize);
            }

            Gizmos.matrix = originalMatrix;

            if (_cachedSockets.Count > 0)
            {
                if (_socketPositionsBeforeAdjustment.Count > 0)
                {
                    Gizmos.color = Color.red;
                    foreach (Vector3 beforePos in _socketPositionsBeforeAdjustment)
                    {
                        Gizmos.DrawSphere(beforePos, 0.1f);
                    }
                }

                Gizmos.color = Color.green;
                for (int i = 0; i < _cachedSockets.Count; i++)
                {
                    if (_cachedSockets[i] == null) continue;

                    Vector3 currentPos = _cachedSockets[i].transform.position;
                    Gizmos.DrawSphere(currentPos, 0.15f);

                    if (i < _socketPositionsBeforeAdjustment.Count)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(_socketPositionsBeforeAdjustment[i], currentPos);
                        Gizmos.color = Color.green;
                    }

                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireSphere(currentPos, _socketCollisionTolerance);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;
        }
#endif
    }
}