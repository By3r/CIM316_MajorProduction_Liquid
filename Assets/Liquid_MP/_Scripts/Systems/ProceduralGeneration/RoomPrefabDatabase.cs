using System.Collections.Generic;
using System.Linq;
using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Database of all room prefabs available for procedural generation.
    /// Provides filtering by socket type, category, and compatibility.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomPrefabDatabase", menuName = "Liquid/Procedural Generation/Room Prefab Database")]
    public class RoomPrefabDatabase : ScriptableObject
    {
        #region Nested Classes

        /// <summary>
        /// Single entry in the room database representing one room prefab.
        /// </summary>
        [System.Serializable]
        public class RoomEntry
        {
            [Header("Room Identity")]
            [Tooltip("The room prefab GameObject")]
            public GameObject prefab;

            [Tooltip("Unique identifier for this room (optional)")]
            public string roomID;

            [Tooltip("Human-readable name for this room")]
            public string displayName;

            [Header("Classification")]
            [Tooltip("Architectural category of this room")]
            public RoomCategory category = RoomCategory.Corridor;

            [Tooltip("Which sector/floor range this room belongs to (1-5)")]
            [Range(1, 5)]
            public int sectorNumber = 1;

            [Header("Socket Information")]
            [Tooltip("What socket types this room has (detected automatically)")]
            public List<Door.DoorType> socketTypes = new List<Door.DoorType>();

            [Tooltip("Number of sockets on this room (auto-calculated)")]
            public int socketCount = 0;

            [Header("Generation Weights")]
            [Tooltip("Higher weight = more likely to be selected (1-10)")]
            [Range(1, 10)]
            public int spawnWeight = 5;

            [Tooltip("Is this room currently enabled for generation?")]
            public bool isEnabled = true;

            [Header("Debug Info")]
            [Tooltip("Preview image of the room (optional)")]
            public Sprite previewImage;

            /// <summary>
            /// Validates and updates socket information from the prefab.
            /// Call this when prefab changes or after importing.
            /// </summary>
            public void RefreshSocketInfo()
            {
                if (prefab == null)
                {
                    socketTypes.Clear();
                    socketCount = 0;
                    return;
                }

                // Get all sockets from prefab
                ConnectionSocket[] sockets = prefab.GetComponentsInChildren<ConnectionSocket>(true);
                socketCount = sockets.Length;

                // Collect unique socket types
                socketTypes.Clear();
                HashSet<Door.DoorType> uniqueTypes = new HashSet<Door.DoorType>();
                foreach (var socket in sockets)
                {
                    uniqueTypes.Add(socket.SocketType);
                }
                socketTypes = uniqueTypes.ToList();
            }

            /// <summary>
            /// Checks if this room has at least one socket of the specified type.
            /// </summary>
            public bool HasSocketType(Door.DoorType type)
            {
                return socketTypes.Contains(type);
            }

            /// <summary>
            /// Checks if this room is valid for generation (has prefab, is enabled, has sockets).
            /// </summary>
            public bool IsValid()
            {
                return prefab != null && isEnabled && socketCount > 0;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Room Collections")]
        [Tooltip("All room prefabs in the database")]
        [SerializeField] private List<RoomEntry> _rooms = new List<RoomEntry>();

        [Header("Special Rooms")]
        [Tooltip("Safe elevator room — the starting room for floor generation")]
        [SerializeField] private RoomEntry _safeElevatorRoom;

        [Header("Database Statistics")]
        [SerializeField] private int _totalRooms = 0;
        [SerializeField] private int _enabledRooms = 0;
        [SerializeField] private int _corridorRooms = 0;
        [SerializeField] private int _hubRooms = 0;
        [SerializeField] private int _intersectionRooms = 0;
        [SerializeField] private int _terminusRooms = 0;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets all room entries in the database.
        /// </summary>
        public List<RoomEntry> AllRooms => _rooms;

        /// <summary>
        /// Gets the safe elevator room (starting room for generation).
        /// </summary>
        public RoomEntry SafeElevatorRoom => _safeElevatorRoom;

        /// <summary>
        /// Gets the total number of rooms in the database.
        /// </summary>
        public int TotalRooms => _totalRooms;

        /// <summary>
        /// Gets the number of enabled rooms.
        /// </summary>
        public int EnabledRooms => _enabledRooms;

        #endregion

        #region Public Methods - Filtering

        /// <summary>
        /// Gets all rooms that have at least one socket of the specified type.
        /// </summary>
        /// <param name="socketType">The socket type to filter by.</param>
        /// <param name="includeDisabled">Include disabled rooms in results?</param>
        /// <returns>List of compatible room entries.</returns>
        public List<RoomEntry> GetRoomsWithSocketType(Door.DoorType socketType, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.HasSocketType(socketType) &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        /// <summary>
        /// Gets all rooms of a specific category.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <param name="includeDisabled">Include disabled rooms in results?</param>
        /// <returns>List of rooms in the specified category.</returns>
        public List<RoomEntry> GetRoomsByCategory(RoomCategory category, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.category == category &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        /// <summary>
        /// Gets all rooms for a specific sector (floor range).
        /// </summary>
        /// <param name="sectorNumber">The sector number (1-5).</param>
        /// <param name="includeDisabled">Include disabled rooms in results?</param>
        /// <returns>List of rooms in the specified sector.</returns>
        public List<RoomEntry> GetRoomsBySector(int sectorNumber, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.sectorNumber == sectorNumber &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        /// <summary>
        /// Gets rooms that match both socket type AND category.
        /// Most common filter for procedural generation.
        /// </summary>
        /// <param name="socketType">Required socket type.</param>
        /// <param name="category">Required category.</param>
        /// <param name="includeDisabled">Include disabled rooms?</param>
        /// <returns>List of matching room entries.</returns>
        public List<RoomEntry> GetRooms(Door.DoorType socketType, RoomCategory category, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.HasSocketType(socketType) &&
                room.category == category &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        /// <summary>
        /// Gets a random room that has the specified socket type.
        /// Uses weighted selection based on spawn weight.
        /// </summary>
        /// <param name="socketType">Required socket type.</param>
        /// <returns>A random compatible room entry, or null if none found.</returns>
        public RoomEntry GetRandomRoomWithSocketType(Door.DoorType socketType)
        {
            List<RoomEntry> compatibleRooms = GetRoomsWithSocketType(socketType);
            return GetWeightedRandomRoom(compatibleRooms);
        }

        /// <summary>
        /// Gets a random room of a specific category.
        /// Uses weighted selection based on spawn weight.
        /// </summary>
        /// <param name="category">Required category.</param>
        /// <returns>A random room entry, or null if none found.</returns>
        public RoomEntry GetRandomRoomByCategory(RoomCategory category)
        {
            List<RoomEntry> categoryRooms = GetRoomsByCategory(category);
            return GetWeightedRandomRoom(categoryRooms);
        }

        /// <summary>
        /// Gets a random room matching both socket type and category.
        /// Uses weighted selection based on spawn weight.
        /// </summary>
        /// <param name="socketType">Required socket type.</param>
        /// <param name="category">Required category.</param>
        /// <returns>A random matching room entry, or null if none found.</returns>
        public RoomEntry GetRandomRoom(Door.DoorType socketType, RoomCategory category)
        {
            List<RoomEntry> matchingRooms = GetRooms(socketType, category);
            return GetWeightedRandomRoom(matchingRooms);
        }

        /// <summary>
        /// Gets a room entry by its display name, roomID, or prefab name.
        /// Used for layout caching to replay exact room placements.
        /// </summary>
        /// <param name="identifier">The display name, roomID, or prefab name of the room to find.</param>
        /// <returns>The matching room entry, or null if not found.</returns>
        public RoomEntry GetRoomByDisplayName(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            // Check special rooms first (by displayName, then prefab name, then constant identifiers)
            if (_safeElevatorRoom != null)
            {
                if (_safeElevatorRoom.displayName == identifier ||
                    (_safeElevatorRoom.prefab != null && _safeElevatorRoom.prefab.name == identifier) ||
                    identifier == "SafeElevatorRoom" ||
                    identifier == "EntryElevatorRoom") // backwards compat with cached layouts
                    return _safeElevatorRoom;
            }

            // Search all rooms by displayName first
            foreach (var room in _rooms)
            {
                if (room.displayName == identifier)
                    return room;
            }

            // Then try by roomID
            foreach (var room in _rooms)
            {
                if (room.roomID == identifier)
                    return room;
            }

            // Finally try by prefab name
            foreach (var room in _rooms)
            {
                if (room.prefab != null && room.prefab.name == identifier)
                    return room;
            }

            return null;
        }

        #endregion

        #region Public Methods - Validation

        /// <summary>
        /// Validates all room entries and refreshes their socket information.
        /// Call this after adding/modifying room prefabs.
        /// </summary>
        public void RefreshAllRooms()
        {
            foreach (var room in _rooms)
            {
                room.RefreshSocketInfo();
            }

            // Refresh special rooms
            _safeElevatorRoom?.RefreshSocketInfo();

            UpdateStatistics();
            Debug.Log($"[RoomPrefabDatabase] Refreshed {_rooms.Count} rooms. Enabled: {_enabledRooms}/{_totalRooms}");
        }

        /// <summary>
        /// Updates internal statistics about the database.
        /// </summary>
        public void UpdateStatistics()
        {
            _totalRooms = _rooms.Count;
            _enabledRooms = _rooms.Count(r => r.isEnabled && r.prefab != null);
            _corridorRooms = _rooms.Count(r => r.category == RoomCategory.Corridor && r.isEnabled);
            _hubRooms = _rooms.Count(r => r.category == RoomCategory.Hub && r.isEnabled);
            _intersectionRooms = _rooms.Count(r => r.category == RoomCategory.Intersection && r.isEnabled);
            _terminusRooms = _rooms.Count(r => r.category == RoomCategory.Terminus && r.isEnabled);
        }

        /// <summary>
        /// Checks if the database has all required special rooms.
        /// </summary>
        /// <returns>True if the safe elevator room is assigned and valid.</returns>
        public bool HasAllSpecialRooms()
        {
            return _safeElevatorRoom != null && _safeElevatorRoom.prefab != null;
        }

        /// <summary>
        /// Checks if database is ready for generation (has rooms and special rooms).
        /// </summary>
        /// <returns>True if database is valid for use.</returns>
        public bool IsValid()
        {
            return _enabledRooms > 0 && HasAllSpecialRooms();
        }

        #endregion

        #region Private Methods - Weighted Selection

        /// <summary>
        /// Selects a random room from a list using weighted probability.
        /// Higher spawn weight = more likely to be selected.
        /// </summary>
        private RoomEntry GetWeightedRandomRoom(List<RoomEntry> rooms)
        {
            if (rooms == null || rooms.Count == 0)
                return null;

            // Calculate total weight (treat weight <= 0 as 1 to prevent silent exclusion)
            int totalWeight = 0;
            foreach (var room in rooms)
            {
                totalWeight += Mathf.Max(1, room.spawnWeight);
            }

            // Pick random value within total weight
            int randomValue = Random.Range(0, totalWeight);

            // Find the room corresponding to this value
            int currentWeight = 0;
            foreach (var room in rooms)
            {
                currentWeight += Mathf.Max(1, room.spawnWeight);
                if (randomValue < currentWeight)
                {
                    return room;
                }
            }

            // Fallback (shouldn't reach here)
            return rooms[rooms.Count - 1];
        }

        #endregion

        #region Unity Lifecycle

        private void OnValidate()
        {
            UpdateStatistics();
        }

        #endregion
    }
}