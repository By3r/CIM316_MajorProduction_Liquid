using System.Collections.Generic;
using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Manages room bounds calculation for procedural generation.
    /// Provides tools for automatic bounds detection from renderers.
    /// Automatically registers with OccupiedSpaceRegistry for BROAD-PHASE collision detection.
    /// Broad-phase skips the previous room to allow natural overlap at door frames.
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

        [Header("Gizmo Settings")]
        [Tooltip("Show bounds gizmos in Scene view")]
        [SerializeField] private bool _showGizmos = true;

        [Tooltip("Color for bounds wireframe")]
        [SerializeField] private Color _boundsColor = Color.green;

        [Header("Debug Info")]
        [SerializeField] private List<ConnectionSocket> _cachedSockets = new();

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
        /// Registry stores bounds for broad-phase collision detection.
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
        /// Gets bounds in world space at current transform.
        /// Used for BROAD-PHASE collision detection in registry.
        /// (Padding has been removed — returns same as GetBounds.)
        /// </summary>
        public Bounds GetPaddedBounds()
        {
            return GetBounds();
        }

        /// <summary>
        /// Gets bounds at a SPECIFIC position and rotation (without instantiating).
        /// CRITICAL METHOD for FloorGenerator's broad-phase check.
        /// (Padding has been removed — returns actual bounds at the specified transform.)
        /// </summary>
        public Bounds GetPaddedBounds(Vector3 worldPosition, Quaternion worldRotation)
        {
            Matrix4x4 trs = Matrix4x4.TRS(worldPosition, worldRotation, transform.lossyScale);
            Vector3 worldCenter = trs.MultiplyPoint3x4(_boundsCenter);
            Vector3 worldSize = Vector3.Scale(_boundsSize, transform.lossyScale);

            return new Bounds(worldCenter, worldSize);
        }

        /// <summary>
        /// Gets bounds for collision detection during room placement.
        /// (Simplified — no padding/tight distinction. Returns actual bounds.)
        /// </summary>
        public Bounds GetCollisionBounds(bool allowSocketOverlap = true)
        {
            return GetBounds();
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

        // AdjustSocketPositions, FindClosestBoundsFace, and ClearBeforePositions
        // have been removed. Sockets now live directly on door frame pieces
        // and don't need to be snapped to bounds faces.

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw room bounds wireframe
            Gizmos.color = _boundsColor;
            Gizmos.DrawWireCube(_boundsCenter, _boundsSize);

            Gizmos.matrix = originalMatrix;

            // Draw cached socket positions
            if (_cachedSockets.Count > 0)
            {
                Gizmos.color = Color.green;
                foreach (ConnectionSocket socket in _cachedSockets)
                {
                    if (socket == null) continue;
                    Gizmos.DrawSphere(socket.transform.position, 0.15f);
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