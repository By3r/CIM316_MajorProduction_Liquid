using System.Collections.Generic;
using System.Linq;
using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    [CreateAssetMenu(fileName = "RoomPrefabDatabase", menuName = "Liquid/Procedural Generation/Room Prefab Database")]
    public class RoomPrefabDatabase : ScriptableObject
    {
        #region Nested Classes

        [System.Serializable]
        public class RoomEntry
        {
            [Header("Room Identity")]
            public GameObject prefab;
            public string roomID;
            public string displayName;

            [Header("Classification")]
            public RoomCategory category = RoomCategory.Corridor;

            [Range(1, 5)]
            public int sectorNumber = 1;

            [Header("Socket Information")]
            public List<Door.DoorType> socketTypes = new List<Door.DoorType>();
            public int socketCount = 0;

            [Header("Generation Weights")]
            [Range(1, 10)]
            public int spawnWeight = 5;
            public bool isEnabled = true;

            [Header("Debug Info")]
            public Sprite previewImage;

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

            public bool HasSocketType(Door.DoorType type)
            {
                return socketTypes.Contains(type);
            }

            public bool IsValid()
            {
                return prefab != null && isEnabled && socketCount > 0;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Room Collections")]
        [SerializeField] private List<RoomEntry> _rooms = new List<RoomEntry>();

        [Header("Special Rooms")]
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

        public List<RoomEntry> AllRooms => _rooms;

        public RoomEntry SafeElevatorRoom => _safeElevatorRoom;

        public int TotalRooms => _totalRooms;

        public int EnabledRooms => _enabledRooms;

        #endregion

        #region Public Methods - Filtering

        public List<RoomEntry> GetRoomsWithSocketType(Door.DoorType socketType, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.HasSocketType(socketType) &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        public List<RoomEntry> GetRoomsByCategory(RoomCategory category, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.category == category &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        public List<RoomEntry> GetRoomsBySector(int sectorNumber, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.sectorNumber == sectorNumber &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        public List<RoomEntry> GetRooms(Door.DoorType socketType, RoomCategory category, bool includeDisabled = false)
        {
            return _rooms.Where(room =>
                room.HasSocketType(socketType) &&
                room.category == category &&
                (includeDisabled || room.isEnabled) &&
                room.prefab != null
            ).ToList();
        }

        public RoomEntry GetRandomRoomWithSocketType(Door.DoorType socketType)
        {
            List<RoomEntry> compatibleRooms = GetRoomsWithSocketType(socketType);
            return GetWeightedRandomRoom(compatibleRooms);
        }

        public RoomEntry GetRandomRoomByCategory(RoomCategory category)
        {
            List<RoomEntry> categoryRooms = GetRoomsByCategory(category);
            return GetWeightedRandomRoom(categoryRooms);
        }

        public RoomEntry GetRandomRoom(Door.DoorType socketType, RoomCategory category)
        {
            List<RoomEntry> matchingRooms = GetRooms(socketType, category);
            return GetWeightedRandomRoom(matchingRooms);
        }

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

        public void UpdateStatistics()
        {
            _totalRooms = _rooms.Count;
            _enabledRooms = _rooms.Count(r => r.isEnabled && r.prefab != null);
            _corridorRooms = _rooms.Count(r => r.category == RoomCategory.Corridor && r.isEnabled);
            _hubRooms = _rooms.Count(r => r.category == RoomCategory.Hub && r.isEnabled);
            _intersectionRooms = _rooms.Count(r => r.category == RoomCategory.Intersection && r.isEnabled);
            _terminusRooms = _rooms.Count(r => r.category == RoomCategory.Terminus && r.isEnabled);
        }

        public bool HasAllSpecialRooms()
        {
            return _safeElevatorRoom != null && _safeElevatorRoom.prefab != null;
        }

        public bool IsValid()
        {
            return _enabledRooms > 0 && HasAllSpecialRooms();
        }

        #endregion

        #region Private Methods - Weighted Selection

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