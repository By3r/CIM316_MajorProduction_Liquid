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
        #region Nested Types

        /// <summary>
        /// A single sub-bound box defined in local space.
        /// Multiple sub-bounds allow compound rooms (L-shaped, cross-shaped)
        /// to have tighter collision detection than a single encapsulating AABB.
        /// </summary>
        [System.Serializable]
        public struct SubBounds
        {
            [Tooltip("Center of this sub-bound in local space")]
            public Vector3 center;

            [Tooltip("Size of this sub-bound in local space")]
            public Vector3 size;

            [Tooltip("Label for editor identification (e.g. 'Horizontal Bar', 'Vertical Wing')")]
            public string label;
        }

        #endregion

        [Header("Registry Settings")]
        [Tooltip("Automatically register this room with the OccupiedSpaceRegistry")]
        [SerializeField] private bool _autoRegisterWithRegistry = true;

        [Tooltip("Register on Start (recommended for runtime generation)")]
        [SerializeField] private bool _registerOnStart = true;

        private bool _isRegistered = false;

        [Header("Bounds Settings")]
        [Tooltip("The center of the bounds in local space. " +
                 "When sub-bounds are defined, this becomes the auto-calculated encapsulating AABB center.")]
        [SerializeField] private Vector3 _boundsCenter = Vector3.zero;

        [Tooltip("The size of the bounds. " +
                 "When sub-bounds are defined, this becomes the auto-calculated encapsulating AABB size.")]
        [SerializeField] private Vector3 _boundsSize = Vector3.one * 10f;

        [Header("Sub-Bounds (Compound Rooms)")]
        [Tooltip("Optional: Define multiple sub-bounds for L-shaped or cross-shaped rooms. " +
                 "When populated (2+), the main Bounds above becomes the auto-calculated encapsulating AABB. " +
                 "Leave empty for simple rectangular rooms.")]
        [SerializeField] private List<SubBounds> _subBounds = new();

        [Header("Gizmo Settings")]
        [Tooltip("Show bounds gizmos in Scene view")]
        [SerializeField] private bool _showGizmos = false;

        [Tooltip("Color for bounds wireframe")]
        [SerializeField] private Color _boundsColor = Color.green;

        /// <summary>Local-space bounds center (for rotation-aware gizmos).</summary>
        public Vector3 LocalBoundsCenter => _boundsCenter;

        /// <summary>Local-space bounds size (for rotation-aware gizmos).</summary>
        public Vector3 LocalBoundsSize => _boundsSize;

        /// <summary>True if this room uses compound sub-bounds (more than 1 entry).</summary>
        public bool HasCompoundBounds => _subBounds != null && _subBounds.Count > 1;

        /// <summary>Read-only access to sub-bounds list for external tools.</summary>
        public IReadOnlyList<SubBounds> SubBoundsList => _subBounds;

        /// <summary>Number of sub-bounds. Returns 0 if no compound bounds defined.</summary>
        public int SubBoundsCount => _subBounds?.Count ?? 0;

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
        /// Accounts for rotation by computing the AABB that fully contains the rotated local box.
        /// </summary>
        public Bounds GetBounds()
        {
            return CalculateRotatedAABB(_boundsCenter, _boundsSize, transform.position, transform.rotation, transform.lossyScale);
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
        /// Accounts for rotation by computing the AABB that fully contains the rotated local box.
        /// </summary>
        public Bounds GetPaddedBounds(Vector3 worldPosition, Quaternion worldRotation)
        {
            return CalculateRotatedAABB(_boundsCenter, _boundsSize, worldPosition, worldRotation, transform.lossyScale);
        }

        /// <summary>
        /// Gets bounds for collision detection during room placement.
        /// (Simplified — no padding/tight distinction. Returns actual bounds.)
        /// </summary>
        public Bounds GetCollisionBounds(bool allowSocketOverlap = true)
        {
            return GetBounds();
        }

        #region Sub-Bounds Methods

        /// <summary>
        /// Gets all sub-bounds transformed to world space at the current transform.
        /// Returns empty list if no compound bounds defined (&lt;2 entries).
        /// </summary>
        public List<Bounds> GetWorldSubBounds()
        {
            if (_subBounds == null || _subBounds.Count < 2)
                return new List<Bounds>();

            var result = new List<Bounds>(_subBounds.Count);
            foreach (SubBounds sub in _subBounds)
            {
                result.Add(CalculateRotatedAABB(sub.center, sub.size,
                    transform.position, transform.rotation, transform.lossyScale));
            }
            return result;
        }

        /// <summary>
        /// Gets all sub-bounds transformed to world space at a PROPOSED position/rotation.
        /// CRITICAL: Used by FloorGenerator BEFORE instantiation to check placement.
        /// Returns empty list if no compound bounds defined (&lt;2 entries).
        /// </summary>
        public List<Bounds> GetWorldSubBounds(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_subBounds == null || _subBounds.Count < 2)
                return new List<Bounds>();

            var result = new List<Bounds>(_subBounds.Count);
            foreach (SubBounds sub in _subBounds)
            {
                result.Add(CalculateRotatedAABB(sub.center, sub.size,
                    worldPosition, worldRotation, transform.lossyScale));
            }
            return result;
        }

        /// <summary>
        /// Recalculates the encapsulating _boundsCenter/_boundsSize from sub-bounds.
        /// Call after modifying the sub-bounds list in the editor.
        /// </summary>
        public void RecalculateEncapsulatingBounds()
        {
            if (_subBounds == null || _subBounds.Count == 0) return;

            Bounds encapsulating = new Bounds(_subBounds[0].center, _subBounds[0].size);
            for (int i = 1; i < _subBounds.Count; i++)
            {
                Bounds sub = new Bounds(_subBounds[i].center, _subBounds[i].size);
                encapsulating.Encapsulate(sub);
            }

            _boundsCenter = encapsulating.center;
            _boundsSize = encapsulating.size;

            Debug.Log($"[BoundsChecker] Recalculated encapsulating bounds from {_subBounds.Count} sub-bounds: " +
                      $"Center={_boundsCenter}, Size={_boundsSize}");
        }

        /// <summary>
        /// Automatically detects sub-bounds from child renderers using XZ grid occupancy.
        /// Divides the room's footprint into a grid, marks which cells contain geometry,
        /// then extracts maximal rectangles as sub-bounds. Ideal for cross/L/T-shaped rooms.
        /// If the room is already rectangular (all cells occupied), no sub-bounds are created.
        /// </summary>
        /// <param name="gridResolution">Size of each grid cell in meters. Smaller = more precise but more sub-bounds.</param>
        public void AutoDetectSubBounds(float gridResolution = 2f)
        {
            // Step 1: Get valid renderers (same as CalculateBoundsFromRenderers)
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            List<Renderer> validRenderers = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer.GetComponentInParent<Doors.ConnectionSocket>() != null)
                    continue;
                validRenderers.Add(renderer);
            }

            if (validRenderers.Count == 0)
            {
                Debug.LogWarning($"[BoundsChecker] No valid renderers found on '{gameObject.name}'. Cannot auto-detect sub-bounds.");
                return;
            }

            // Step 2: Calculate encapsulating local bounds from renderers
            Bounds localBounds = new Bounds(
                transform.InverseTransformPoint(validRenderers[0].bounds.center),
                Vector3.zero);

            // Also collect individual renderer local bounds for occupancy testing
            List<Bounds> rendererLocalBounds = new List<Bounds>();
            foreach (Renderer renderer in validRenderers)
            {
                Vector3 localMin = transform.InverseTransformPoint(renderer.bounds.min);
                Vector3 localMax = transform.InverseTransformPoint(renderer.bounds.max);

                localBounds.Encapsulate(localMin);
                localBounds.Encapsulate(localMax);

                // Store individual renderer bounds in local space
                Bounds rLocalBounds = new Bounds();
                rLocalBounds.SetMinMax(
                    Vector3.Min(localMin, localMax),
                    Vector3.Max(localMin, localMax));
                rendererLocalBounds.Add(rLocalBounds);
            }

            // Step 3: Create XZ occupancy grid
            float boundsMinX = localBounds.min.x;
            float boundsMinZ = localBounds.min.z;
            float boundsMaxX = localBounds.max.x;
            float boundsMaxZ = localBounds.max.z;

            int gridX = Mathf.Max(1, Mathf.CeilToInt((boundsMaxX - boundsMinX) / gridResolution));
            int gridZ = Mathf.Max(1, Mathf.CeilToInt((boundsMaxZ - boundsMinZ) / gridResolution));

            // Recalculate actual cell size to fit evenly
            float cellSizeX = (boundsMaxX - boundsMinX) / gridX;
            float cellSizeZ = (boundsMaxZ - boundsMinZ) / gridZ;

            bool[,] occupied = new bool[gridX, gridZ];
            int occupiedCount = 0;

            // Step 4: Mark cells that contain renderer geometry
            for (int gx = 0; gx < gridX; gx++)
            {
                for (int gz = 0; gz < gridZ; gz++)
                {
                    float cellMinX = boundsMinX + gx * cellSizeX;
                    float cellMinZ = boundsMinZ + gz * cellSizeZ;
                    float cellMaxX = cellMinX + cellSizeX;
                    float cellMaxZ = cellMinZ + cellSizeZ;

                    // Check if any renderer overlaps this cell (XZ only)
                    foreach (Bounds rBounds in rendererLocalBounds)
                    {
                        if (rBounds.max.x > cellMinX && rBounds.min.x < cellMaxX &&
                            rBounds.max.z > cellMinZ && rBounds.min.z < cellMaxZ)
                        {
                            occupied[gx, gz] = true;
                            occupiedCount++;
                            break;
                        }
                    }
                }
            }

            int totalCells = gridX * gridZ;

            // If all cells are occupied (or nearly all), it's a simple rectangular room — no sub-bounds needed
            if (occupiedCount >= totalCells * 0.9f)
            {
                _subBounds.Clear();
                _boundsCenter = localBounds.center;
                _boundsSize = localBounds.size;
                Debug.Log($"[BoundsChecker] Room '{gameObject.name}' is rectangular ({occupiedCount}/{totalCells} cells). " +
                          "No sub-bounds needed. Updated encapsulating bounds.");
                return;
            }

            // Step 5: Extract maximal rectangles using greedy algorithm
            // Mark cells as consumed as they are assigned to rectangles
            bool[,] consumed = new bool[gridX, gridZ];
            List<SubBounds> detectedSubBounds = new List<SubBounds>();
            int subIndex = 0;

            for (int gx = 0; gx < gridX; gx++)
            {
                for (int gz = 0; gz < gridZ; gz++)
                {
                    if (!occupied[gx, gz] || consumed[gx, gz]) continue;

                    // Greedy expand: first extend in Z as far as possible, then extend in X
                    int maxZ = gz;
                    while (maxZ + 1 < gridZ && occupied[gx, maxZ + 1] && !consumed[gx, maxZ + 1])
                        maxZ++;

                    // Now extend in X while the full Z column is occupied and not consumed
                    int maxX = gx;
                    bool canExtendX = true;
                    while (canExtendX && maxX + 1 < gridX)
                    {
                        for (int z = gz; z <= maxZ; z++)
                        {
                            if (!occupied[maxX + 1, z] || consumed[maxX + 1, z])
                            {
                                canExtendX = false;
                                break;
                            }
                        }
                        if (canExtendX) maxX++;
                    }

                    // Mark all cells in this rectangle as consumed
                    for (int x = gx; x <= maxX; x++)
                        for (int z = gz; z <= maxZ; z++)
                            consumed[x, z] = true;

                    // Convert grid rectangle to local-space sub-bound
                    float subMinX = boundsMinX + gx * cellSizeX;
                    float subMinZ = boundsMinZ + gz * cellSizeZ;
                    float subMaxX = boundsMinX + (maxX + 1) * cellSizeX;
                    float subMaxZ = boundsMinZ + (maxZ + 1) * cellSizeZ;

                    Vector3 subCenter = new Vector3(
                        (subMinX + subMaxX) * 0.5f,
                        localBounds.center.y,
                        (subMinZ + subMaxZ) * 0.5f);

                    Vector3 subSize = new Vector3(
                        subMaxX - subMinX,
                        localBounds.size.y,
                        subMaxZ - subMinZ);

                    detectedSubBounds.Add(new SubBounds
                    {
                        center = subCenter,
                        size = subSize,
                        label = $"Auto {subIndex++}"
                    });
                }
            }

            // Step 6: If only 1 sub-bound was detected, it's effectively rectangular — clear sub-bounds
            if (detectedSubBounds.Count <= 1)
            {
                _subBounds.Clear();
                _boundsCenter = localBounds.center;
                _boundsSize = localBounds.size;
                Debug.Log($"[BoundsChecker] Room '{gameObject.name}' produced only 1 sub-bound — using simple rectangular bounds.");
                return;
            }

            // Apply detected sub-bounds
            _subBounds.Clear();
            _subBounds.AddRange(detectedSubBounds);

            // Recalculate encapsulating bounds from sub-bounds
            RecalculateEncapsulatingBounds();

            Debug.Log($"[BoundsChecker] Auto-detected {_subBounds.Count} sub-bounds for '{gameObject.name}' " +
                      $"(grid: {gridX}x{gridZ}, resolution: {gridResolution}m, " +
                      $"occupied: {occupiedCount}/{totalCells} cells)");
        }

        /// <summary>
        /// Checks if a world-space point is inside any sub-bound (or the single encapsulating bound).
        /// More accurate than encapsulating AABB for compound rooms.
        /// </summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            if (HasCompoundBounds)
            {
                foreach (Bounds sub in GetWorldSubBounds())
                {
                    if (sub.Contains(worldPoint)) return true;
                }
                return false;
            }
            return GetBounds().Contains(worldPoint);
        }

        #endregion

        /// <summary>
        /// Computes an axis-aligned bounding box that fully contains the rotated local bounds.
        /// Transforms all 8 corners of the local box through rotation and scale,
        /// then creates a Bounds that encapsulates all corners.
        /// </summary>
        private static Bounds CalculateRotatedAABB(Vector3 localCenter, Vector3 localSize, Vector3 worldPosition, Quaternion worldRotation, Vector3 lossyScale)
        {
            Vector3 scaledSize = Vector3.Scale(localSize, lossyScale);
            Vector3 scaledCenter = Vector3.Scale(localCenter, lossyScale);
            Vector3 halfSize = scaledSize * 0.5f;

            // Compute AABB size from the absolute values of the rotated axes.
            // This is equivalent to transforming all 8 corners but much cheaper.
            Matrix4x4 rotMatrix = Matrix4x4.Rotate(worldRotation);
            Vector3 axisX = rotMatrix.GetColumn(0); // rotated X axis
            Vector3 axisY = rotMatrix.GetColumn(1); // rotated Y axis
            Vector3 axisZ = rotMatrix.GetColumn(2); // rotated Z axis

            // The AABB half-extent along each world axis is the sum of
            // the absolute projections of each local half-extent onto that axis.
            Vector3 aabbHalfSize = new Vector3(
                Mathf.Abs(axisX.x) * halfSize.x + Mathf.Abs(axisY.x) * halfSize.y + Mathf.Abs(axisZ.x) * halfSize.z,
                Mathf.Abs(axisX.y) * halfSize.x + Mathf.Abs(axisY.y) * halfSize.y + Mathf.Abs(axisZ.y) * halfSize.z,
                Mathf.Abs(axisX.z) * halfSize.x + Mathf.Abs(axisY.z) * halfSize.y + Mathf.Abs(axisZ.z) * halfSize.z
            );

            Vector3 worldCenter = worldPosition + worldRotation * scaledCenter;

            return new Bounds(worldCenter, aabbHalfSize * 2f);
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

            if (_subBounds != null && _subBounds.Count > 1)
            {
                // Compound bounds: draw encapsulating AABB dimmed
                Color dimmedColor = _boundsColor;
                dimmedColor.a *= 0.3f;
                Gizmos.color = dimmedColor;
                Gizmos.DrawWireCube(_boundsCenter, _boundsSize);

                // Draw each sub-bound in distinct colors
                Color[] subColors =
                {
                    Color.cyan, Color.magenta, Color.yellow,
                    new Color(1f, 0.5f, 0f), new Color(0.5f, 1f, 0f)
                };
                for (int i = 0; i < _subBounds.Count; i++)
                {
                    Gizmos.color = subColors[i % subColors.Length];
                    Gizmos.DrawWireCube(_subBounds[i].center, _subBounds[i].size);
                }
            }
            else
            {
                // Single bounds — original behavior
                Gizmos.color = _boundsColor;
                Gizmos.DrawWireCube(_boundsCenter, _boundsSize);
            }

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