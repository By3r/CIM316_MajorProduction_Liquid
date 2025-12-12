using _Scripts.Core.Managers;
using _Scripts.Systems.ProceduralGeneration.Doors;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Enhanced floor generator with TWO-PHASE collision detection, random socket selection, and blockade system.
    /// Uses seed-based generation via FloorStateManager for deterministic floor layouts.
    /// IMPORTANT: Generation only works in Play mode for consistent, deterministic results.
    /// </summary>
    public class FloorGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [Tooltip("Room prefab database to use for generation")]
        [SerializeField] private RoomPrefabDatabase _roomDatabase;

        [Tooltip("Maximum number of room connections (door credits)")]
        [SerializeField] private int _doorCreditBudget = 20;

        [Tooltip("Door prefab to instantiate at connections")]
        [SerializeField] private GameObject _doorPrefab;

        [Header("Retry Settings")]
        [Tooltip("Enable automatic regeneration if budget not fully used")]
        [SerializeField] private bool _enableRetryOnIncompleteGeneration = true;

        [Tooltip("Maximum generation attempts before giving up")]
        [SerializeField] private int _maxGenerationAttempts = 3;

        [Tooltip("Minimum credits that must be used (percentage of budget)")]
        [Range(0f, 1f)]
        [SerializeField] private float _minBudgetUsageThreshold = 0.8f;

        [Header("Generation Options")]
        [SerializeField] private bool _generateOnStart;
        [Tooltip("Show detailed debug logs (errors always shown)")]
        [SerializeField] private bool _showDebugLogs = false;

        [Header("Category Preferences")]
        [Tooltip("Prefer corridor rooms when expanding? (0 = no preference, 10 = always)")]
        [Range(0, 10)]
        [SerializeField] private int _corridorPreference = 5;

        [Tooltip("Prefer hub rooms occasionally? (0 = never, 10 = always)")]
        [Range(0, 10)]
        [SerializeField] private int _hubPreference = 2;

        [Header("Collision Settings")]
        [Tooltip("Maximum attempts to place a room at each socket")]
        [SerializeField] private int _maxAttemptsPerSocket = 5;

        [Header("Floor Persistence")]
        [Tooltip("Current floor number being generated (1-based)")]
        [SerializeField] private int _currentFloorNumber = 1;

        [Tooltip("Current seed used for generation (read from FloorStateManager)")]
        [SerializeField] private int _currentSeed;

        [Tooltip("Use FloorStateManager for seed-based generation? If false, uses pure random generation.")]
        [SerializeField] private bool _useSeedBasedGeneration = true;

        [Header("Runtime Info (Read-Only)")]
        [SerializeField] private List<GameObject> _spawnedRooms = new();
        [SerializeField] private List<GameObject> _spawnedBlockades = new();
        [SerializeField] private List<ConnectionSocket> _availableSockets = new();
        [SerializeField] private int _connectionsMade;
        [SerializeField] private int _creditsRemaining;
        [SerializeField] private int _blockadeSpawnCount;
        [SerializeField] private int _currentRegenerationAttempt;
        [SerializeField] private bool _exitRoomSpawned;

        private DoorConnectionSystem _connectionSystem;
        [SerializeField] private GridPathfinder gridPathfinder;
        [SerializeField] private OccupiedSpaceRegistry occupiedSpaceRegistry;
        private GridPathfinder GridRef => gridPathfinder != null ? gridPathfinder : GridPathfinder.Instance;
        private OccupiedSpaceRegistry RegistryRef => occupiedSpaceRegistry != null ? occupiedSpaceRegistry : OccupiedSpaceRegistry.Instance;

        /// <summary>
        /// Gets or sets the current floor number being generated.
        /// </summary>
        public int CurrentFloorNumber
        {
            get => _currentFloorNumber;
            set => _currentFloorNumber = Mathf.Max(1, value);
        }

        /// <summary>
        /// Gets the seed used for the current floor generation.
        /// </summary>
        public int CurrentSeed => _currentSeed;

        private void Awake()
        {
            EnsureConnectionSystemExists();
        }

        private void Start()
        {
            if (_generateOnStart)
            {
                GenerateFloor();
            }
        }

        private void EnsureConnectionSystemExists()
        {
            if (_connectionSystem == null)
            {
                _connectionSystem = GetComponent<DoorConnectionSystem>();
                if (_connectionSystem == null)
                {
                    _connectionSystem = gameObject.AddComponent<DoorConnectionSystem>();
                }
            }
        }

        /// <summary>
        /// Generates a procedural floor using the room database with two-phase collision detection.
        /// IMPORTANT: Only works in Play mode for deterministic results.
        /// </summary>
        public void GenerateFloor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FloorGenerator] Generation only works in Play mode. Enter Play mode to generate floors.");
                return;
            }

            _currentRegenerationAttempt = 0;

            while (true)
            {
                GenerateFloorInternal();

                float budgetUsagePercentage = 1f - ((float)_creditsRemaining / _doorCreditBudget);
                bool budgetSufficient = budgetUsagePercentage >= _minBudgetUsageThreshold;

                // Exit room must ALWAYS spawn
                if (!_exitRoomSpawned)
                {
                    _currentRegenerationAttempt++;
                    if (_currentRegenerationAttempt >= _maxGenerationAttempts)
                    {
                        Debug.LogError($"[FloorGenerator] GENERATION FAILED: Exit room could not spawn after {_maxGenerationAttempts} attempts!");
                        break;
                    }
                    continue;
                }

                // Success
                if (budgetSufficient || _creditsRemaining == 0)
                {
                    break;
                }

                // Retry threshold not met
                if (!_enableRetryOnIncompleteGeneration || _currentRegenerationAttempt >= _maxGenerationAttempts - 1)
                {
                    if (_showDebugLogs)
                    {
                        Debug.LogWarning($"[FloorGenerator] Generation incomplete: used {budgetUsagePercentage:P0} of budget");
                    }
                    break;
                }

                _currentRegenerationAttempt++;
            }

            RebuildNavGridFromRegistry();

            if (GridPathfinder.Instance != null && OccupiedSpaceRegistry.Instance != null)
            {
                if (OccupiedSpaceRegistry.Instance.TryGetCombinedBounds(out var floorBounds))
                {
                    float extraPadding = 1f;
                    GridPathfinder.Instance.RebuildToFitBounds(floorBounds, extraPadding);
                }
            }
        }

        /// <summary>
        /// Internal generation method that performs a single generation pass.
        /// </summary>
        private void GenerateFloorInternal()
        {
            EnsureConnectionSystemExists();

            if (_roomDatabase == null)
            {
                Debug.LogError("[FloorGenerator] Room database not assigned!");
                return;
            }

            if (!_roomDatabase.IsValid())
            {
                Debug.LogError("[FloorGenerator] Room database is invalid!");
                return;
            }

            // Seed-Based Generation
            if (_useSeedBasedGeneration)
            {
                if (!FloorStateManager.Instance.IsInitialized)
                {
                    Debug.LogError("[FloorGenerator] FloorStateManager not initialized! Enable 'Auto Initialize On Awake' or initialize manually.");
                    return;
                }

                FloorState floorState = FloorStateManager.Instance.GetOrCreateFloorState(_currentFloorNumber);
                int baseSeed = floorState.generationSeed;
                _currentSeed = baseSeed + _currentRegenerationAttempt;
                Random.InitState(_currentSeed);
            }
            else
            {
                _currentSeed = 0;
            }

            ClearFloor();
            OccupiedSpaceRegistry.Instance.ClearRegistry();

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorGenerator] Starting Floor {_currentFloorNumber} generation (Seed: {_currentSeed}, Budget: {_doorCreditBudget})");
            }

            _creditsRemaining = _doorCreditBudget;
            _availableSockets.Clear();
            _blockadeSpawnCount = 0;
            _exitRoomSpawned = false;

            GameObject startRoom = SpawnStartingRoom();
            if (startRoom == null)
            {
                Debug.LogError("[FloorGenerator] Failed to spawn starting room!");
                return;
            }

            AddRoomSocketsToList(startRoom);

            // Main generation loop - reserve 1 credit for exit room
            while (_creditsRemaining > 1 && _availableSockets.Count > 0)
            {
                int randomIndex = Random.Range(0, _availableSockets.Count);
                ConnectionSocket sourceSocket = _availableSockets[randomIndex];
                _availableSockets.RemoveAt(randomIndex);

                if (sourceSocket == null || sourceSocket.IsConnected)
                    continue;

                bool success = TrySpawnAndConnectRoomWithTwoPhaseCheck(sourceSocket);

                if (success)
                {
                    _creditsRemaining--;
                    _connectionsMade++;
                }
            }

            SpawnExitRoomAtTerminus();
            SpawnBlockadesOnUnconnectedSockets();

            // Save working seed for floor revisits
            if (_useSeedBasedGeneration && _exitRoomSpawned)
            {
                FloorState floorState = FloorStateManager.Instance.GetCurrentFloorState();
                if (floorState.generationSeed != _currentSeed)
                {
                    floorState.generationSeed = _currentSeed;
                }
                FloorStateManager.Instance.MarkCurrentFloorAsVisited();
            }

            // Summary log
            Debug.Log($"[FloorGenerator] Floor {_currentFloorNumber} complete: {_spawnedRooms.Count} rooms, {_connectionsMade} connections, {_blockadeSpawnCount} blockades");
        }

        private GameObject SpawnStartingRoom()
        {
            RoomPrefabDatabase.RoomEntry startRoomEntry = _roomDatabase.EntryElevatorRoom;

            if (startRoomEntry == null || startRoomEntry.prefab == null)
            {
                Debug.LogError("[FloorGenerator] Entry elevator room not set in database!");
                return null;
            }

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            GameObject startRoom = SpawnRoom(startRoomEntry.prefab, startPosition, startRotation, "EntryRoom", registerNow: true);
            _spawnedRooms.Add(startRoom);

            return startRoom;
        }

        /// <summary>
        /// Spawns the exit elevator room at one of the unconnected sockets.
        /// </summary>
        private void SpawnExitRoomAtTerminus()
        {
            RoomPrefabDatabase.RoomEntry exitRoomEntry = _roomDatabase.ExitElevatorRoom;

            if (exitRoomEntry == null || exitRoomEntry.prefab == null)
            {
                Debug.LogError("[FloorGenerator] Exit elevator room not set in database!");
                return;
            }

            // Collect all unconnected sockets
            List<ConnectionSocket> candidateSockets = new List<ConnectionSocket>();
            foreach (GameObject room in _spawnedRooms)
            {
                if (room == null) continue;

                ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
                foreach (ConnectionSocket socket in sockets)
                {
                    if (socket != null && !socket.IsConnected && socket.ConnectedSocket == null)
                    {
                        ConnectionSocket exitSocket = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, socket.SocketType);
                        if (exitSocket != null)
                        {
                            candidateSockets.Add(socket);
                        }
                    }
                }
            }

            if (candidateSockets.Count == 0)
            {
                Debug.LogError("[FloorGenerator] No compatible sockets found for exit room!");
                return;
            }

            // Shuffle for randomness
            for (int i = candidateSockets.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidateSockets[i], candidateSockets[j]) = (candidateSockets[j], candidateSockets[i]);
            }

            // Try each socket
            foreach (ConnectionSocket selectedSocket in candidateSockets)
            {
                ConnectionSocket exitSocketPrefab = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, selectedSocket.SocketType);
                if (exitSocketPrefab == null) continue;

                Vector3 prefabOriginalPos = exitRoomEntry.prefab.transform.position;
                Quaternion prefabOriginalRot = exitRoomEntry.prefab.transform.rotation;

                exitRoomEntry.prefab.transform.position = Vector3.zero;
                exitRoomEntry.prefab.transform.rotation = Quaternion.identity;

                var (roomPosition, roomRotation) = _connectionSystem.CalculateTargetRoomTransform(
                    selectedSocket, exitSocketPrefab, exitRoomEntry.prefab.transform);

                exitRoomEntry.prefab.transform.position = prefabOriginalPos;
                exitRoomEntry.prefab.transform.rotation = prefabOriginalRot;

                // Broad-phase collision check
                BoundsChecker exitBoundsChecker = exitRoomEntry.prefab.GetComponent<BoundsChecker>();
                if (exitBoundsChecker != null)
                {
                    exitRoomEntry.prefab.transform.position = roomPosition;
                    exitRoomEntry.prefab.transform.rotation = roomRotation;
                    Bounds worldBounds = exitBoundsChecker.GetPaddedBounds();
                    exitRoomEntry.prefab.transform.position = prefabOriginalPos;
                    exitRoomEntry.prefab.transform.rotation = prefabOriginalRot;

                    if (OccupiedSpaceRegistry.Instance.IsSpaceOccupied(worldBounds, (BoundsChecker)null))
                    {
                        continue;
                    }
                }

                GameObject exitRoom = SpawnRoom(exitRoomEntry.prefab, roomPosition, roomRotation, "ExitRoom", registerNow: true);
                if (exitRoom == null) continue;

                _spawnedRooms.Add(exitRoom);

                ConnectionSocket spawnedExitSocket = FindMatchingSocketInInstance(exitRoom, exitSocketPrefab);
                if (spawnedExitSocket == null)
                {
                    _spawnedRooms.Remove(exitRoom);
                    Destroy(exitRoom);
                    continue;
                }

                bool connectionSuccess = _connectionSystem.ConnectRooms(selectedSocket, spawnedExitSocket, exitRoom.transform, _doorPrefab);

                if (connectionSuccess)
                {
                    _creditsRemaining--;
                    _connectionsMade++;
                    _exitRoomSpawned = true;
                    return;
                }
                else
                {
                    _spawnedRooms.Remove(exitRoom);
                    BoundsChecker boundsChecker = exitRoom.GetComponent<BoundsChecker>();
                    if (boundsChecker != null) boundsChecker.UnregisterFromRegistry();
                    Destroy(exitRoom);
                }
            }

            Debug.LogError("[FloorGenerator] Failed to place exit room at any compatible socket!");
        }

        /// <summary>
        /// Spawns blockades on all unconnected sockets.
        /// </summary>
        private void SpawnBlockadesOnUnconnectedSockets()
        {
            _blockadeSpawnCount = 0;

            foreach (GameObject room in _spawnedRooms)
            {
                if (room == null) continue;

                ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
                foreach (ConnectionSocket socket in sockets)
                {
                    if (socket == null || socket.IsConnected || socket.ConnectedSocket != null)
                        continue;

                    GameObject blockade = socket.SpawnBlockade(transform);
                    if (blockade != null)
                    {
                        _blockadeSpawnCount++;
                        _spawnedBlockades.Add(blockade);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to spawn and connect a room using TWO-PHASE collision detection.
        /// </summary>
        private bool TrySpawnAndConnectRoomWithTwoPhaseCheck(ConnectionSocket sourceSocket)
        {
            BoundsChecker sourceBounds = sourceSocket.GetComponentInParent<BoundsChecker>();
            if (sourceBounds == null) return false;

            List<RoomPrefabDatabase.RoomEntry> compatibleRooms = _roomDatabase.GetRoomsWithSocketType(sourceSocket.SocketType);
            if (compatibleRooms.Count == 0) return false;

            for (int attempt = 0; attempt < _maxAttemptsPerSocket; attempt++)
            {
                RoomPrefabDatabase.RoomEntry selectedRoom = SelectRoomWithPreferences(compatibleRooms, sourceSocket.SocketType);
                if (selectedRoom == null || selectedRoom.prefab == null) continue;

                ConnectionSocket targetSocket = FindCompatibleSocketInPrefab(selectedRoom.prefab, sourceSocket.SocketType);
                if (targetSocket == null) continue;

                Quaternion proposedRotation = Quaternion.LookRotation(-sourceSocket.Forward) * Quaternion.Inverse(targetSocket.Rotation) * selectedRoom.prefab.transform.rotation;
                Vector3 targetSocketLocalPos = targetSocket.transform.localPosition;
                Vector3 targetSocketWorldOffset = proposedRotation * targetSocketLocalPos;
                Vector3 proposedPosition = sourceSocket.Position - targetSocketWorldOffset;

                BoundsChecker targetBoundsChecker = selectedRoom.prefab.GetComponent<BoundsChecker>();
                if (targetBoundsChecker == null) continue;

                Bounds paddedBounds = targetBoundsChecker.GetPaddedBounds(proposedPosition, proposedRotation);

                // BROAD-PHASE check
                if (OccupiedSpaceRegistry.Instance.IsSpaceOccupied(paddedBounds))
                {
                    continue;
                }

                GameObject newRoom = SpawnRoom(selectedRoom.prefab, proposedPosition, proposedRotation,
                    $"{selectedRoom.displayName}_{_spawnedRooms.Count}", registerNow: false);
                _spawnedRooms.Add(newRoom);

                ConnectionSocket newRoomSocket = FindMatchingSocketInInstance(newRoom, targetSocket);
                if (newRoomSocket == null)
                {
                    Destroy(newRoom);
                    _spawnedRooms.Remove(newRoom);
                    continue;
                }

                // NARROW-PHASE check via connection
                bool narrowPhasePass = _connectionSystem.ConnectRooms(sourceSocket, newRoomSocket, newRoom.transform, _doorPrefab);

                if (narrowPhasePass)
                {
                    BoundsChecker boundsChecker = newRoom.GetComponent<BoundsChecker>();
                    if (boundsChecker != null)
                    {
                        boundsChecker.RegisterWithRegistry();
                    }
                    AddRoomSocketsToList(newRoom, newRoomSocket);
                    return true;
                }
                else
                {
                    Destroy(newRoom);
                    _spawnedRooms.Remove(newRoom);
                }
            }

            return false;
        }

        private ConnectionSocket FindCompatibleSocketInPrefab(GameObject prefab, Door.DoorType socketType)
        {
            ConnectionSocket[] sockets = prefab.GetComponentsInChildren<ConnectionSocket>(true);
            foreach (ConnectionSocket socket in sockets)
            {
                if (!socket.IsConnected && socket.IsCompatibleWith(socketType))
                {
                    return socket;
                }
            }
            return null;
        }

        private ConnectionSocket FindMatchingSocketInInstance(GameObject instantiatedRoom, ConnectionSocket prefabSocket)
        {
            ConnectionSocket[] sockets = instantiatedRoom.GetComponentsInChildren<ConnectionSocket>();

            foreach (ConnectionSocket socket in sockets)
            {
                if (socket.name == prefabSocket.name && socket.SocketType == prefabSocket.SocketType)
                {
                    return socket;
                }
            }

            foreach (ConnectionSocket socket in sockets)
            {
                if (socket.SocketType == prefabSocket.SocketType && !socket.IsConnected)
                {
                    return socket;
                }
            }

            return null;
        }

        private RoomPrefabDatabase.RoomEntry SelectRoomWithPreferences(
            List<RoomPrefabDatabase.RoomEntry> compatibleRooms,
            Door.DoorType socketType)
        {
            if (compatibleRooms.Count == 1)
                return compatibleRooms[0];

            return _roomDatabase.GetRandomRoomWithSocketType(socketType);
        }

        private void AddRoomSocketsToList(GameObject room, ConnectionSocket excludeSocket = null)
        {
            ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
            foreach (ConnectionSocket socket in sockets)
            {
                if (!socket.IsConnected && socket != excludeSocket)
                {
                    _availableSockets.Add(socket);
                }
            }
        }

        private GameObject SpawnRoom(GameObject prefab, Vector3 position, Quaternion rotation, string roomName, bool registerNow = true)
        {
            GameObject room = Instantiate(prefab, position, rotation, transform);
            room.name = roomName;

            if (registerNow)
            {
                BoundsChecker boundsChecker = room.GetComponent<BoundsChecker>();
                if (boundsChecker != null)
                {
                    boundsChecker.RegisterWithRegistry();
                }
            }

            return room;
        }

        private void RebuildNavGridFromRegistry()
        {
            if (GridPathfinder.Instance == null || OccupiedSpaceRegistry.Instance == null)
                return;

            if (!OccupiedSpaceRegistry.Instance.TryGetCombinedBounds(out var floorBounds))
                return;

            float extraPaddingXZ = 1f;
            float gridHeight = 4f;
            GridPathfinder.Instance.RebuildToFitBounds(floorBounds, extraPaddingXZ, gridHeight);
        }

        /// <summary>
        /// Clears all spawned rooms and resets generation state.
        /// </summary>
        public void ClearFloor()
        {
            foreach (GameObject room in _spawnedRooms)
            {
                if (room != null)
                {
                    ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
                    foreach (ConnectionSocket socket in sockets)
                    {
                        if (socket != null) socket.Disconnect();
                    }
                }
            }

            foreach (GameObject room in _spawnedRooms)
            {
                if (room != null)
                {
                    BoundsChecker boundsChecker = room.GetComponent<BoundsChecker>();
                    if (boundsChecker != null) boundsChecker.UnregisterFromRegistry();

                    if (Application.isPlaying)
                        Destroy(room);
                    else
                        DestroyImmediate(room);
                }
            }

            foreach (GameObject blockade in _spawnedBlockades)
            {
                if (blockade != null)
                {
                    if (Application.isPlaying)
                        Destroy(blockade);
                    else
                        DestroyImmediate(blockade);
                }
            }

            _spawnedRooms.Clear();
            _spawnedBlockades.Clear();
            _availableSockets.Clear();
            _connectionsMade = 0;
            _creditsRemaining = 0;
            _blockadeSpawnCount = 0;

            if (OccupiedSpaceRegistry.Instance != null)
            {
                OccupiedSpaceRegistry.Instance.ClearRegistry();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnedRooms.Count > 0)
            {
                Gizmos.color = Color.cyan;
                foreach (GameObject room in _spawnedRooms)
                {
                    if (room != null)
                    {
                        Gizmos.DrawWireCube(room.transform.position, Vector3.one * 0.5f);
                    }
                }
            }
        }
#endif
    }
}