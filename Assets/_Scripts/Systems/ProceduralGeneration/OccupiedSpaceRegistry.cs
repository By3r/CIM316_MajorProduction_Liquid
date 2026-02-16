using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Singleton registry for BROAD-PHASE collision detection.
    /// Tracks all room PADDED bounds to prevent general overlap.
    /// Does NOT handle socket-level precision - that's DoorConnectionSystem's job.
    /// Used by FloorGenerator to check if a room's "personal space" is free.
    /// </summary>
    public class OccupiedSpaceRegistry : MonoBehaviour
    {
        private static OccupiedSpaceRegistry _instance;
        public static OccupiedSpaceRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<OccupiedSpaceRegistry>();
                    if (_instance == null)
                    {
                        GameObject registryObj = new GameObject("OccupiedSpaceRegistry");
                        _instance = registryObj.AddComponent<OccupiedSpaceRegistry>();
                        Debug.Log("[OccupiedSpaceRegistry] Created singleton instance.");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Data structure to hold room occupation information.
        /// Stores PADDED bounds for broad-phase collision detection.
        /// </summary>
        [System.Serializable]
        public class OccupiedSpace
        {
            public BoundsChecker boundsChecker;
            public Transform roomTransform;
            public Bounds paddedBoundsWorld;  //! IMPORTANT: WE NEED PADDED BOUNDS, Dana if you're reading this, this is very important for rooms to overlap ever so slightly so the bounds system works correctly
            public List<Bounds> subBoundsWorld;  // null/empty = no compound bounds, populated for L-shaped/cross rooms
            public Vector3 registeredPosition;
            public Quaternion registeredRotation;
            public string roomName;
            public float registrationTime;

            public OccupiedSpace(BoundsChecker checker, Transform room)
            {
                boundsChecker = checker;
                roomTransform = room;
                registeredPosition = room.position;
                registeredRotation = room.rotation;
                roomName = room.name;
                registrationTime = Time.time;
                UpdateBounds();
            }

            /// <summary>
            /// Updates the cached PADDED bounds and sub-bounds. Call this if the room moves/rotates.
            /// </summary>
            public void UpdateBounds()
            {
                if (boundsChecker != null)
                {
                    // Store encapsulating bounds for broad-phase quick-reject
                    paddedBoundsWorld = boundsChecker.GetPaddedBounds();

                    // Cache sub-bounds for compound rooms (L-shaped, cross-shaped)
                    if (boundsChecker.HasCompoundBounds)
                        subBoundsWorld = boundsChecker.GetWorldSubBounds();
                    else
                        subBoundsWorld = null;
                }
                else if (roomTransform != null)
                {
                    // Fallback: create basic padded bounds
                    paddedBoundsWorld = new Bounds(roomTransform.position, Vector3.one * 7f);
                    subBoundsWorld = null;
                }
            }

            /// <summary>
            /// Checks if this space has moved since registration
            /// </summary>
            public bool HasMoved()
            {
                if (roomTransform == null) return false;

                float positionDelta = Vector3.Distance(roomTransform.position, registeredPosition);
                float rotationDelta = Quaternion.Angle(roomTransform.rotation, registeredRotation);

                return positionDelta > 0.01f || rotationDelta > 0.1f;
            }
        }

        [Header("Registry Settings")]
        [Tooltip("Automatically update bounds if rooms move")]
        [SerializeField] private bool _autoUpdateMovedRooms = true;

        [Tooltip("Check for moved rooms every N seconds")]
        [SerializeField] private float _moveCheckInterval = 1f;

        [Tooltip("Max fraction (0-1) of the smaller room's XZ area that can overlap with its source room. " +
                 "Door-frame overlaps are typically <5%. Set higher for safety margin.")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxSourceOverlapFraction = 0.15f;

        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugLogs = true;
        [SerializeField] private bool _showGizmos = false;
        [SerializeField] private Color _occupiedSpaceColor = new Color(1f, 0f, 0f, 0.3f);

        [Header("Registry Data")]
        [SerializeField] private List<OccupiedSpace> _occupiedSpaces = new List<OccupiedSpace>();

        private float _lastMoveCheckTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[OccupiedSpaceRegistry] Multiple instances detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            if (_autoUpdateMovedRooms && Time.time - _lastMoveCheckTime > _moveCheckInterval)
            {
                CheckForMovedRooms();
                _lastMoveCheckTime = Time.time;
            }
        }

        /// <summary>
        /// Registers a room's occupied space in the registry.
        /// Should be called when a room is instantiated and positioned.
        /// Stores PADDED bounds for broad-phase collision detection.
        /// </summary>
        public void RegisterOccupiedSpace(BoundsChecker boundsChecker, Transform roomTransform)
        {
            if (boundsChecker == null || roomTransform == null)
            {
                Debug.LogError("[OccupiedSpaceRegistry] Cannot register null boundsChecker or roomTransform!");
                return;
            }

            // Check if already registered
            if (_occupiedSpaces.Any(space => space.boundsChecker == boundsChecker))
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[OccupiedSpaceRegistry] Room '{roomTransform.name}' is already registered!");
                return;
            }

            OccupiedSpace newSpace = new OccupiedSpace(boundsChecker, roomTransform);
            _occupiedSpaces.Add(newSpace);

            if (_showDebugLogs)
            {
                Debug.Log($"[OccupiedSpaceRegistry] ✓ Registered room '{roomTransform.name}' " +
                         $"at position {roomTransform.position} " +
                         $"with PADDED bounds (Total rooms: {_occupiedSpaces.Count})");
            }
        }

        /// <summary>
        /// Unregisters a room's occupied space from the registry.
        /// Should be called when a room is destroyed or removed.
        /// </summary>
        public void UnregisterOccupiedSpace(BoundsChecker boundsChecker)
        {
            if (boundsChecker == null) return;

            OccupiedSpace spaceToRemove = _occupiedSpaces.FirstOrDefault(space => space.boundsChecker == boundsChecker);

            if (spaceToRemove != null)
            {
                _occupiedSpaces.Remove(spaceToRemove);

                if (_showDebugLogs)
                {
                    Debug.Log($"[OccupiedSpaceRegistry] ✓ Unregistered room '{spaceToRemove.roomName}' " +
                             $"(Remaining rooms: {_occupiedSpaces.Count})");
                }
            }
        }

        /// <summary>
        /// BROAD-PHASE CHECK: Determines if a given PADDED bounds would collide with any registered room.
        /// This is the primary method used by FloorGenerator before attempting to place a room.
        /// Checks PADDED vs PADDED bounds - no socket-level exceptions!
        /// </summary>
        /// <param name="testPaddedBounds">The PADDED bounds of the room being tested</param>
        /// <param name="ignoreRoom">Optional room to ignore (e.g., the source room when connecting)</param>
        /// <returns>True if space is occupied (collision), false if space is free</returns>
        public bool IsSpaceOccupied(Bounds testPaddedBounds, BoundsChecker ignoreRoom = null)
        {
            foreach (OccupiedSpace space in _occupiedSpaces)
            {
                if (space.roomTransform == null || space.boundsChecker == null)
                    continue;

                if (!testPaddedBounds.Intersects(space.paddedBoundsWorld))
                    continue;

                // For the source room (connected via door), allow overlap at the door frame
                // but reject if the new room overlaps by more than the allowed fraction of the smaller room.
                if (ignoreRoom != null && space.boundsChecker == ignoreRoom)
                {
                    float overlapXZ = CalculateOverlapXZ(testPaddedBounds, space.paddedBoundsWorld);

                    // Calculate XZ area of the smaller room
                    float testAreaXZ = testPaddedBounds.size.x * testPaddedBounds.size.z;
                    float sourceAreaXZ = space.paddedBoundsWorld.size.x * space.paddedBoundsWorld.size.z;
                    float smallerAreaXZ = Mathf.Min(testAreaXZ, sourceAreaXZ);

                    float overlapFraction = smallerAreaXZ > 0.01f ? overlapXZ / smallerAreaXZ : 0f;

                    if (overlapFraction <= _maxSourceOverlapFraction)
                        continue; // Acceptable overlap at door connection

                    if (_showDebugLogs)
                    {
                        Debug.LogWarning($"[OccupiedSpaceRegistry] ✗ Source room overlap too large! " +
                                       $"Overlap XZ={overlapXZ:F1}m² = {overlapFraction:P0} of smaller room " +
                                       $"(max={_maxSourceOverlapFraction:P0}) with source room '{space.roomName}'");
                    }
                    return true; // Overlap with source room is too large
                }

                if (_showDebugLogs)
                {
                    Debug.LogWarning($"[OccupiedSpaceRegistry] ✗ BROAD-PHASE collision detected! " +
                                   $"Test bounds center: {testPaddedBounds.center}, " +
                                   $"Colliding with room '{space.roomName}' at {space.paddedBoundsWorld.center}");
                }
                return true; // Space is occupied
            }

            return false; // Space is free
        }

        /// <summary>
        /// Calculates the XZ overlap area between two bounds.
        /// </summary>
        private static float CalculateOverlapXZ(Bounds a, Bounds b)
        {
            float overlapX = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
            float overlapZ = Mathf.Max(0f, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
            return overlapX * overlapZ;
        }

        #region Compound Bounds Support

        /// <summary>
        /// BROAD-PHASE CHECK with compound bounds support.
        /// When either the test room or a registered room has sub-bounds,
        /// uses sub-bound pair checking for accuracy after encapsulating AABB quick-reject.
        /// This avoids false collision rejections in the empty corners of L-shaped/cross-shaped rooms.
        /// </summary>
        /// <param name="testEncapsulatingBounds">Encapsulating AABB of the room being tested</param>
        /// <param name="testSubBounds">Sub-bounds of the test room (null/empty = single AABB)</param>
        /// <param name="ignoreRoom">Source room to allow door-frame overlap</param>
        /// <returns>True if space is occupied (collision), false if space is free</returns>
        public bool IsSpaceOccupied(Bounds testEncapsulatingBounds, List<Bounds> testSubBounds,
                                     BoundsChecker ignoreRoom = null)
        {
            bool testHasSubs = testSubBounds != null && testSubBounds.Count > 1;

            foreach (OccupiedSpace space in _occupiedSpaces)
            {
                if (space.roomTransform == null || space.boundsChecker == null)
                    continue;

                // FAST PATH: encapsulating AABBs don't intersect -> skip
                if (!testEncapsulatingBounds.Intersects(space.paddedBoundsWorld))
                    continue;

                bool registeredHasSubs = space.subBoundsWorld != null && space.subBoundsWorld.Count > 1;

                // Source room (connected via door): fraction-based overlap check
                if (ignoreRoom != null && space.boundsChecker == ignoreRoom)
                {
                    float overlapXZ = CalculateCompoundOverlapXZ(
                        testEncapsulatingBounds, testSubBounds, testHasSubs,
                        space.paddedBoundsWorld, space.subBoundsWorld, registeredHasSubs);

                    float testAreaXZ = CalculateCompoundAreaXZ(testEncapsulatingBounds, testSubBounds, testHasSubs);
                    float sourceAreaXZ = CalculateCompoundAreaXZ(space.paddedBoundsWorld, space.subBoundsWorld, registeredHasSubs);
                    float smallerAreaXZ = Mathf.Min(testAreaXZ, sourceAreaXZ);

                    float overlapFraction = smallerAreaXZ > 0.01f ? overlapXZ / smallerAreaXZ : 0f;

                    if (overlapFraction <= _maxSourceOverlapFraction)
                        continue; // Acceptable overlap at door connection

                    if (_showDebugLogs)
                    {
                        Debug.LogWarning($"[OccupiedSpaceRegistry] Source room overlap too large! " +
                                       $"Overlap XZ={overlapXZ:F1}m2 = {overlapFraction:P0} of smaller room " +
                                       $"(max={_maxSourceOverlapFraction:P0}) with source room '{space.roomName}'");
                    }
                    return true;
                }

                // Non-source room: check for real intersection
                if (testHasSubs || registeredHasSubs)
                {
                    // Sub-bounds pair check: collision only if any test sub-bound intersects any registered sub-bound
                    if (AnySubBoundsIntersect(
                            testEncapsulatingBounds, testSubBounds, testHasSubs,
                            space.paddedBoundsWorld, space.subBoundsWorld, registeredHasSubs))
                    {
                        if (_showDebugLogs)
                        {
                            Debug.LogWarning($"[OccupiedSpaceRegistry] BROAD-PHASE collision (compound) " +
                                           $"with room '{space.roomName}'");
                        }
                        return true;
                    }
                    // Encapsulating AABBs intersect but no sub-bounds actually overlap — NOT a collision
                    continue;
                }

                // Neither has sub-bounds: original behavior (encapsulating intersection = collision)
                if (_showDebugLogs)
                {
                    Debug.LogWarning($"[OccupiedSpaceRegistry] BROAD-PHASE collision detected! " +
                                   $"Test bounds center: {testEncapsulatingBounds.center}, " +
                                   $"Colliding with room '{space.roomName}' at {space.paddedBoundsWorld.center}");
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if any sub-bound from set A intersects any sub-bound from set B.
        /// Falls back to encapsulating AABB for the side without sub-bounds.
        /// </summary>
        private static bool AnySubBoundsIntersect(
            Bounds aEncaps, List<Bounds> aSubs, bool aHasSubs,
            Bounds bEncaps, List<Bounds> bSubs, bool bHasSubs)
        {
            if (aHasSubs && bHasSubs)
            {
                foreach (Bounds a in aSubs)
                    foreach (Bounds b in bSubs)
                        if (a.Intersects(b)) return true;
                return false;
            }

            if (aHasSubs) // B has no subs, check A's subs against B's encapsulating
            {
                foreach (Bounds a in aSubs)
                    if (a.Intersects(bEncaps)) return true;
                return false;
            }

            // A has no subs, check B's subs against A's encapsulating
            foreach (Bounds b in bSubs)
                if (aEncaps.Intersects(b)) return true;
            return false;
        }

        /// <summary>
        /// Calculates total XZ overlap area between two potentially-compound bounds.
        /// For sub-bounds, sums pairwise overlaps. Sub-bounds within one room should not overlap each other.
        /// </summary>
        private static float CalculateCompoundOverlapXZ(
            Bounds aEncaps, List<Bounds> aSubs, bool aHasSubs,
            Bounds bEncaps, List<Bounds> bSubs, bool bHasSubs)
        {
            if (!aHasSubs && !bHasSubs)
                return CalculateOverlapXZ(aEncaps, bEncaps);

            float totalOverlap = 0f;

            // Build effective lists: use sub-bounds if available, otherwise single encapsulating
            IReadOnlyList<Bounds> aList = aHasSubs ? (IReadOnlyList<Bounds>)aSubs : new[] { aEncaps };
            IReadOnlyList<Bounds> bList = bHasSubs ? (IReadOnlyList<Bounds>)bSubs : new[] { bEncaps };

            foreach (Bounds a in aList)
                foreach (Bounds b in bList)
                    totalOverlap += CalculateOverlapXZ(a, b);

            return totalOverlap;
        }

        /// <summary>
        /// Calculates the XZ footprint area of a potentially-compound bounds.
        /// For sub-bounds, sums individual areas (valid when sub-bounds don't overlap each other).
        /// </summary>
        private static float CalculateCompoundAreaXZ(Bounds encaps, List<Bounds> subs, bool hasSubs)
        {
            if (!hasSubs)
                return encaps.size.x * encaps.size.z;

            float total = 0f;
            foreach (Bounds sub in subs)
                total += sub.size.x * sub.size.z;
            return total;
        }

        #endregion

        /// <summary>
        /// Overload that accepts a Transform as the ignore parameter.
        /// </summary>
        public bool IsSpaceOccupied(Bounds testPaddedBounds, Transform ignoreRoom)
        {
            BoundsChecker ignoreBounds = ignoreRoom != null ? ignoreRoom.GetComponent<BoundsChecker>() : null;
            return IsSpaceOccupied(testPaddedBounds, ignoreBounds);
        }

        /// <summary>
        /// Gets all occupied spaces currently registered.
        /// </summary>
        public List<OccupiedSpace> GetAllOccupiedSpaces()
        {
            return new List<OccupiedSpace>(_occupiedSpaces);
        }

        /// <summary>
        /// Returns a combined bounds that encapsulates all registered padded room bounds.
        /// </summary>
        public bool TryGetCombinedBounds(out Bounds combinedBounds)
        {
            combinedBounds = new Bounds();
            bool hasAny = false;

            for (int i = 0; i < _occupiedSpaces.Count; i++)
            {
                OccupiedSpace space = _occupiedSpaces[i];
                if (space == null || space.roomTransform == null)
                {
                    continue;
                }

                if (!hasAny)
                {
                    combinedBounds = space.paddedBoundsWorld;
                    hasAny = true;
                }
                else
                {
                    combinedBounds.Encapsulate(space.paddedBoundsWorld);
                }
            }

            return hasAny;
        }

        /// <summary>
        /// Gets the occupied space for a specific room transform.
        /// </summary>
        public OccupiedSpace GetOccupiedSpace(Transform roomTransform)
        {
            return _occupiedSpaces.FirstOrDefault(space => space.roomTransform == roomTransform);
        }

        /// <summary>
        /// Gets the occupied space for a specific BoundsChecker.
        /// </summary>
        public OccupiedSpace GetOccupiedSpace(BoundsChecker boundsChecker)
        {
            return _occupiedSpaces.FirstOrDefault(space => space.boundsChecker == boundsChecker);
        }

        /// <summary>
        /// Clears all registered occupied spaces.
        /// Call this when regenerating the entire floor.
        /// </summary>
        public void ClearRegistry()
        {
            _occupiedSpaces.Clear();

            if (_showDebugLogs)
                Debug.Log("[OccupiedSpaceRegistry] ✓ Cleared all occupied spaces.");
        }

        /// <summary>
        /// Checks for rooms that have moved and updates their bounds.
        /// </summary>
        private void CheckForMovedRooms()
        {
            int movedCount = 0;

            foreach (OccupiedSpace space in _occupiedSpaces)
            {
                if (space.roomTransform == null)
                    continue;

                if (space.HasMoved())
                {
                    space.registeredPosition = space.roomTransform.position;
                    space.registeredRotation = space.roomTransform.rotation;
                    space.UpdateBounds();
                    movedCount++;
                }
            }

            if (movedCount > 0 && _showDebugLogs)
            {
                Debug.Log($"[OccupiedSpaceRegistry] Updated {movedCount} moved room bounds.");
            }
        }

        /// <summary>
        /// Removes any null entries from the registry (rooms that were destroyed).
        /// </summary>
        public void CleanupNullEntries()
        {
            int removedCount = _occupiedSpaces.RemoveAll(space =>
                space.boundsChecker == null || space.roomTransform == null);

            if (removedCount > 0 && _showDebugLogs)
            {
                Debug.Log($"[OccupiedSpaceRegistry] Cleaned up {removedCount} null entries.");
            }
        }

        /// <summary>
        /// Gets statistics about the registry for debugging.
        /// </summary>
        public string GetRegistryStats()
        {
            int nullEntries = _occupiedSpaces.Count(space => space.boundsChecker == null || space.roomTransform == null);
            int validEntries = _occupiedSpaces.Count - nullEntries;

            return $"Registered Rooms: {validEntries}\n" +
                   $"Null Entries: {nullEntries}\n" +
                   $"Total Entries: {_occupiedSpaces.Count}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            foreach (OccupiedSpace space in _occupiedSpaces)
            {
                if (space.roomTransform == null) continue;

                // Use the room's local-to-world matrix so the gizmo rotates with the room
                if (space.boundsChecker != null)
                {
                    Matrix4x4 originalMatrix = Gizmos.matrix;
                    Gizmos.matrix = space.roomTransform.localToWorldMatrix;

                    if (space.boundsChecker.HasCompoundBounds)
                    {
                        // Compound room: draw encapsulating bounds dimmed
                        Color dimmedColor = _occupiedSpaceColor;
                        dimmedColor.a *= 0.3f;
                        Gizmos.color = dimmedColor;
                        Gizmos.DrawCube(space.boundsChecker.LocalBoundsCenter, space.boundsChecker.LocalBoundsSize);

                        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                        Gizmos.DrawWireCube(space.boundsChecker.LocalBoundsCenter, space.boundsChecker.LocalBoundsSize);

                        // Draw each sub-bound in the local space
                        foreach (BoundsChecker.SubBounds sub in space.boundsChecker.SubBoundsList)
                        {
                            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
                            Gizmos.DrawCube(sub.center, sub.size);
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawWireCube(sub.center, sub.size);
                        }
                    }
                    else
                    {
                        // Single bounds: original behavior
                        Vector3 localCenter = space.boundsChecker.LocalBoundsCenter;
                        Vector3 localSize = space.boundsChecker.LocalBoundsSize;

                        Gizmos.color = _occupiedSpaceColor;
                        Gizmos.DrawCube(localCenter, localSize);

                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(localCenter, localSize);
                    }

                    Gizmos.matrix = originalMatrix;
                }
                else
                {
                    // Fallback: axis-aligned bounds when no BoundsChecker reference
                    Gizmos.color = _occupiedSpaceColor;
                    Gizmos.DrawCube(space.paddedBoundsWorld.center, space.paddedBoundsWorld.size);

                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(space.paddedBoundsWorld.center, space.paddedBoundsWorld.size);
                }
            }
        }
#endif
    }
}