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
            /// Updates the cached PADDED bounds. Call this if the room moves/rotates.
            /// </summary>
            public void UpdateBounds()
            {
                if (boundsChecker != null)
                {
                    // Store PADDED bounds for personal space checking
                    paddedBoundsWorld = boundsChecker.GetPaddedBounds();
                }
                else if (roomTransform != null)
                {
                    // Fallback: create basic padded bounds
                    paddedBoundsWorld = new Bounds(roomTransform.position, Vector3.one * 7f);
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

        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugLogs = true;
        [SerializeField] private bool _showGizmos = true;
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
                // Skip null or ignored rooms
                if (space.roomTransform == null || space.boundsChecker == null)
                    continue;

                if (ignoreRoom != null && space.boundsChecker == ignoreRoom)
                    continue;

                // Check for intersection between PADDED bounds
                if (testPaddedBounds.Intersects(space.paddedBoundsWorld))
                {
                    if (_showDebugLogs)
                    {
                        Debug.LogWarning($"[OccupiedSpaceRegistry] ✗ BROAD-PHASE collision detected! " +
                                       $"Test bounds center: {testPaddedBounds.center}, " +
                                       $"Colliding with room '{space.roomName}' at {space.paddedBoundsWorld.center}");
                    }
                    return true; // Space is occupied
                }
            }

            return false; // Space is free
        }

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

            Gizmos.color = _occupiedSpaceColor;

            foreach (OccupiedSpace space in _occupiedSpaces)
            {
                if (space.roomTransform == null) continue;

                // Draw PADDED occupied bounds (broad-phase)
                Gizmos.DrawCube(space.paddedBoundsWorld.center, space.paddedBoundsWorld.size);
                
                // Draw wireframe for clarity
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(space.paddedBoundsWorld.center, space.paddedBoundsWorld.size);
                Gizmos.color = _occupiedSpaceColor;
            }
        }
#endif
    }
}