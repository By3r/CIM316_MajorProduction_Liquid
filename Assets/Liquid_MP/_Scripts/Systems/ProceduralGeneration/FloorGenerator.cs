using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.ProceduralGeneration.Doors;
using _Scripts.Systems.ProceduralGeneration.Enemies;
using _Scripts.Systems.ProceduralGeneration.Items;
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

        [Header("Gizmos")]
        [SerializeField] private bool _showGizmos = false;

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
            // Subscribe to floor transition events from elevator
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Subscribe<int>("OnFloorTransitionRequested", HandleFloorTransition);
            }

            // If we generate procedurally, the nav grid will be rebuilt
            // at the end of GenerateFloor().
            if (_generateOnStart)
            {
                GenerateFloor();
            }
            else
            {
                // If the level is already built (for example placed manually
                // or restored from a save), try to build the nav grid once
                // on startup based on the current OccupiedSpaceRegistry.
                RebuildNavGridFromRegistry();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Unsubscribe<int>("OnFloorTransitionRequested", HandleFloorTransition);
            }
        }

        /// <summary>
        /// Handles floor transition requests from the elevator.
        /// Regenerates the floor with the new floor number.
        /// </summary>
        private void HandleFloorTransition(int targetFloor)
        {
            // Update the floor number
            _currentFloorNumber = targetFloor;

            // Regenerate the floor
            GenerateFloor();
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
        /// On revisited floors, replays the cached layout for Delver-style persistence.
        /// IMPORTANT: Only works in Play mode for deterministic results.
        /// </summary>
        public void GenerateFloor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FloorGenerator] Generation only works in Play mode. Enter Play mode to generate floors.");
                return;
            }

            // Notify listeners that floor generation is starting (freeze player)
            GameManager.Instance?.EventManager?.Publish("OnFloorGenerationStarted");

            _currentRegenerationAttempt = 0;

            // Check if this is a revisited floor with a cached layout
            FloorState floorState = null;
            bool isRevisit = false;

            if (_useSeedBasedGeneration && FloorStateManager.Instance != null && FloorStateManager.Instance.IsInitialized)
            {
                floorState = FloorStateManager.Instance.GetOrCreateFloorState(_currentFloorNumber);
                isRevisit = floorState.isVisited && floorState.HasCachedLayout;

                // If this is a revisit with a cached layout, replay it instead of regenerating
                if (isRevisit)
                {
                    if (_showDebugLogs)
                    {
                        Debug.Log($"[FloorGenerator] Replaying cached layout for floor {_currentFloorNumber}");
                    }

                    if (ReplayCachedLayout(floorState.cachedLayout))
                    {
                        // Re-spawn dropped items on this floor and in the safe room
                        SpawnDroppedItems(floorState);
                        SpawnSafeRoomItems();

                        // Notify listeners that floor generation is complete (unfreeze player)
                        GameManager.Instance?.EventManager?.Publish("OnFloorGenerationComplete");
                        return; // Successfully replayed, no need to generate
                    }
                    else
                    {
                        Debug.LogWarning($"[FloorGenerator] Failed to replay cached layout for floor {_currentFloorNumber}, regenerating...");
                        isRevisit = false; // Fall back to generation
                    }
                }
            }

            // Procedural generation (first visit or fallback)
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

            // Cache the layout for future revisits (Delver-style persistence)
            if (floorState != null && !floorState.HasCachedLayout)
            {
                CacheCurrentLayout(floorState);
            }

            // After the floor is built (and registry populated) rebuild the nav grid once.
            RebuildNavGridFromRegistry();

            // Re-spawn dropped items on this floor and in the safe room
            if (floorState != null)
            {
                SpawnDroppedItems(floorState);
            }
            SpawnSafeRoomItems();

            // Notify listeners that floor generation is complete (unfreeze player)
            GameManager.Instance?.EventManager?.Publish("OnFloorGenerationComplete");
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

            // Clear floor FIRST before setting seed (to avoid random state pollution)
            ClearFloor();
            OccupiedSpaceRegistry.Instance.ClearRegistry();

            // Seed-Based Generation - set seed AFTER clearing to ensure determinism
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

                // Set seed immediately before generation starts
                Random.InitState(_currentSeed);
            }
            else
            {
                _currentSeed = 0;
            }

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

            // Note: The working seed is saved by the Elevator when transitioning away from this floor.
            // This ensures the correct seed (that successfully generated the floor) is preserved.

            // Summary log (only in debug mode)
            if (_showDebugLogs)
            {
                Debug.Log($"[FloorGenerator] Floor {_currentFloorNumber} complete: {_spawnedRooms.Count} rooms, {_connectionsMade} connections, {_blockadeSpawnCount} blockades");
            }
        }

        private GameObject SpawnStartingRoom()
        {
            RoomPrefabDatabase.RoomEntry startRoomEntry = _roomDatabase.SafeElevatorRoom;

            if (startRoomEntry == null || startRoomEntry.prefab == null)
            {
                Debug.LogError("[FloorGenerator] Safe elevator room not set in database!");
                return null;
            }

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            GameObject startRoom = SpawnRoom(startRoomEntry.prefab, startPosition, startRotation, "SafeElevatorRoom", registerNow: true);
            _spawnedRooms.Add(startRoom);

            return startRoom;
        }

        /// <summary>
        /// Spawns an exit room at one of the unconnected sockets.
        /// Picks from the normal room database (any compatible room).
        /// </summary>
        private void SpawnExitRoomAtTerminus()
        {
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
                        candidateSockets.Add(socket);
                    }
                }
            }

            if (candidateSockets.Count == 0)
            {
                Debug.LogError("[FloorGenerator] No unconnected sockets found for exit room!");
                return;
            }

            // Shuffle for randomness
            for (int i = candidateSockets.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidateSockets[i], candidateSockets[j]) = (candidateSockets[j], candidateSockets[i]);
            }

            // Try each socket with compatible rooms from the database
            foreach (ConnectionSocket selectedSocket in candidateSockets)
            {
                List<RoomPrefabDatabase.RoomEntry> compatibleRooms = _roomDatabase.GetRoomsWithSocketType(selectedSocket.SocketType);
                if (compatibleRooms.Count == 0) continue;

                // Shuffle compatible rooms for variety
                for (int i = compatibleRooms.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (compatibleRooms[i], compatibleRooms[j]) = (compatibleRooms[j], compatibleRooms[i]);
                }

                foreach (RoomPrefabDatabase.RoomEntry exitRoomEntry in compatibleRooms)
                {
                    if (exitRoomEntry.prefab == null) continue;

                    ConnectionSocket exitSocketPrefab = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, selectedSocket.SocketType);
                    if (exitSocketPrefab == null) continue;

                    // Calculate placement using the same approach as TrySpawnAndConnectRoomWithTwoPhaseCheck
                    Quaternion proposedRotation = Quaternion.LookRotation(-selectedSocket.Forward)
                        * Quaternion.Inverse(exitSocketPrefab.Rotation)
                        * exitRoomEntry.prefab.transform.rotation;
                    Vector3 targetSocketWorldOffset = proposedRotation * exitSocketPrefab.LocalPosition;
                    Vector3 proposedPosition = selectedSocket.Position - targetSocketWorldOffset;

                    // Broad-phase collision check (compound-aware)
                    BoundsChecker exitBoundsChecker = exitRoomEntry.prefab.GetComponent<BoundsChecker>();
                    if (exitBoundsChecker != null)
                    {
                        Bounds paddedBounds = exitBoundsChecker.GetPaddedBounds(proposedPosition, proposedRotation);
                        List<Bounds> testSubBounds = exitBoundsChecker.GetWorldSubBounds(proposedPosition, proposedRotation);
                        BoundsChecker sourceRoomBounds = selectedSocket.GetComponentInParent<BoundsChecker>();
                        if (OccupiedSpaceRegistry.Instance.IsSpaceOccupied(paddedBounds, testSubBounds, sourceRoomBounds))
                        {
                            continue;
                        }
                    }

                    string roomName = !string.IsNullOrEmpty(exitRoomEntry.displayName)
                        ? exitRoomEntry.displayName
                        : exitRoomEntry.prefab.name;
                    GameObject exitRoom = SpawnRoom(exitRoomEntry.prefab, proposedPosition, proposedRotation,
                        $"ExitRoom_{roomName}", registerNow: true);
                    if (exitRoom == null) continue;

                    _spawnedRooms.Add(exitRoom);

                    ConnectionSocket spawnedExitSocket = FindMatchingSocketInInstance(exitRoom, exitSocketPrefab);
                    if (spawnedExitSocket == null)
                    {
                        _spawnedRooms.Remove(exitRoom);
                        BoundsChecker bc = exitRoom.GetComponent<BoundsChecker>();
                        if (bc != null) bc.UnregisterFromRegistry();
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
                        BoundsChecker bc = exitRoom.GetComponent<BoundsChecker>();
                        if (bc != null) bc.UnregisterFromRegistry();
                        Destroy(exitRoom);
                    }
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
                Vector3 targetSocketLocalPos = targetSocket.LocalPosition;
                Vector3 targetSocketWorldOffset = proposedRotation * targetSocketLocalPos;
                Vector3 proposedPosition = sourceSocket.Position - targetSocketWorldOffset;

                BoundsChecker targetBoundsChecker = selectedRoom.prefab.GetComponent<BoundsChecker>();
                if (targetBoundsChecker == null) continue;

                Bounds paddedBounds = targetBoundsChecker.GetPaddedBounds(proposedPosition, proposedRotation);
                List<Bounds> testSubBounds = targetBoundsChecker.GetWorldSubBounds(proposedPosition, proposedRotation);

                // BROAD-PHASE check — skip the source room to allow natural overlap at door frames
                // Uses compound sub-bounds when available for tighter collision detection
                if (OccupiedSpaceRegistry.Instance.IsSpaceOccupied(paddedBounds, testSubBounds, sourceBounds))
                {
                    continue;
                }

                // Use prefab name for naming if displayName is empty (ensures we can identify rooms for caching)
                string roomName = !string.IsNullOrEmpty(selectedRoom.displayName)
                    ? selectedRoom.displayName
                    : selectedRoom.prefab.name;
                GameObject newRoom = SpawnRoom(selectedRoom.prefab, proposedPosition, proposedRotation,
                    $"{roomName}_{_spawnedRooms.Count}", registerNow: false);
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
            if (this.gridPathfinder == null && GridPathfinder.Instance == null)
            {
                Debug.LogWarning("Could not rebuild nav grid: no GridPathfinder in scene.");
                return;
            }

            if (OccupiedSpaceRegistry.Instance == null)
            {
                Debug.LogWarning("Could not rebuild nav grid: no OccupiedSpaceRegistry in scene.");
                return;
            }

            if (!OccupiedSpaceRegistry.Instance.TryGetCombinedBounds(out var floorBounds))
            {
                Debug.LogWarning("Couldn't rebuild nav grid: no occupied spaces registered.");
                return;
            }

            float extraPaddingXZ = 1f;

            var gridPathfinder = this.gridPathfinder != null ? this.gridPathfinder : GridPathfinder.Instance;
            gridPathfinder.RebuildToFitBounds(floorBounds, extraPaddingXZ);
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

            // Clear all spawned pickups
            ClearAllPickups();

            // Clear all spawned enemies
            ClearAllEnemies();

            // Reset the static container references so they can be recreated
            ItemSpawnPoint.ClearContainerReference();
            EnemySpawnPoint.ClearContainerReference();
        }

        /// <summary>
        /// Destroys all pickup objects in the pickups container.
        /// Called during floor transitions to clean up items before regeneration.
        /// </summary>
        private void ClearAllPickups()
        {
            GameObject pickupsContainer = GameObject.Find("--- PICKUPS ---");
            if (pickupsContainer != null)
            {
                // Destroy all children (the actual pickup items)
                for (int i = pickupsContainer.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = pickupsContainer.transform.GetChild(i);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }

                // Pickups cleared
            }
        }

        /// <summary>
        /// Destroys all spawned enemies in the enemies container.
        /// Called during floor transitions to clean up enemies before regeneration.
        /// </summary>
        private void ClearAllEnemies()
        {
            GameObject enemiesContainer = GameObject.Find("--- ENEMIES ---");
            if (enemiesContainer != null)
            {
                for (int i = enemiesContainer.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = enemiesContainer.transform.GetChild(i);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }

        #region Dropped Item Spawning

        /// <summary>
        /// Re-spawns player-dropped items that were saved for this floor.
        /// Called after floor generation/replay to restore dropped items.
        /// </summary>
        private void SpawnDroppedItems(FloorState floorState)
        {
            if (floorState == null || floorState.droppedItems == null || floorState.droppedItems.Count == 0)
                return;

            SpawnDroppedItemList(floorState.droppedItems, "floor");
        }

        /// <summary>
        /// Re-spawns items dropped in the safe room (elevator room).
        /// These persist across all floor transitions.
        /// </summary>
        private void SpawnSafeRoomItems()
        {
            var floorManager = FloorStateManager.Instance;
            if (floorManager == null || floorManager.SafeRoomDroppedItems == null || floorManager.SafeRoomDroppedItems.Count == 0)
                return;

            SpawnDroppedItemList(floorManager.SafeRoomDroppedItems, "safe room");
        }

        /// <summary>
        /// Spawns a list of dropped items in the world.
        /// </summary>
        private void SpawnDroppedItemList(List<DroppedItemData> droppedItems, string sourceLabel)
        {
            // Ensure pickups container exists
            GameObject pickupsContainer = GameObject.Find("--- PICKUPS ---");
            if (pickupsContainer == null)
            {
                pickupsContainer = new GameObject("--- PICKUPS ---");
            }

            int spawnedCount = 0;

            foreach (DroppedItemData droppedData in droppedItems)
            {
                if (droppedData == null || string.IsNullOrEmpty(droppedData.itemId)) continue;

                InventoryItemData itemData = ItemDatabase.FindByItemId(droppedData.itemId);
                if (itemData == null)
                {
                    Debug.LogWarning($"[ItemPersistence] FAILED: Could not find item '{droppedData.itemId}' in ItemDatabase.");
                    continue;
                }

                if (itemData.worldPrefab == null)
                {
                    Debug.LogWarning($"[ItemPersistence] FAILED: Item '{droppedData.itemId}' has no worldPrefab.");
                    continue;
                }

                Vector3 position = new Vector3(droppedData.posX, droppedData.posY, droppedData.posZ);
                Quaternion rotation = Quaternion.Euler(droppedData.rotX, droppedData.rotY, droppedData.rotZ);

                GameObject spawnedItem = Instantiate(itemData.worldPrefab, position, rotation, pickupsContainer.transform);

                // Set the pickup ID so it can be tracked for re-pickup
                Pickup pickup = spawnedItem.GetComponent<Pickup>();
                if (pickup != null)
                {
                    pickup.SetPickupId(droppedData.droppedItemId);
                }

                // Disable physics on respawned items so they stay in place
                Rigidbody rb = spawnedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                spawnedCount++;
            }

            if (spawnedCount > 0 && _showDebugLogs)
            {
                Debug.Log($"[ItemPersistence] Respawned {spawnedCount}/{droppedItems.Count} items from {sourceLabel}.");
            }
        }

        #endregion

        #region Floor Layout Caching (Delver-style Persistence)

        /// <summary>
        /// Caches the current floor layout for Delver-style persistence.
        /// Stores exact room positions and rotations for replay on revisit.
        /// </summary>
        /// <param name="floorState">The floor state to cache the layout in.</param>
        private void CacheCurrentLayout(FloorState floorState)
        {
            if (floorState == null) return;

            var layout = new CachedFloorLayout { isValid = true };

            foreach (GameObject room in _spawnedRooms)
            {
                if (room == null) continue;

                // Find the matching RoomEntry by comparing prefab structure
                string displayName = FindRoomDisplayName(room);

                layout.roomPlacements.Add(new CachedRoomPlacement
                {
                    prefabDisplayName = displayName,
                    position = room.transform.position,
                    rotationEuler = room.transform.rotation.eulerAngles,
                    roomInstanceName = room.name
                });

                if (_showDebugLogs)
                {
                    Debug.Log($"[FloorGenerator] Cached room '{room.name}' with displayName '{displayName}'");
                }
            }

            floorState.cachedLayout = layout;

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorGenerator] Cached layout for floor {_currentFloorNumber}: {layout.roomPlacements.Count} rooms");
            }
        }

        /// <summary>
        /// Finds the best identifier for a spawned room by matching it against the database.
        /// Returns displayName, roomID, or prefab name - whichever is available.
        /// </summary>
        private string FindRoomDisplayName(GameObject room)
        {
            // Check special rooms first by name prefix - use constant identifiers for reliability
            if (room.name == "SafeElevatorRoom" || room.name.StartsWith("SafeElevatorRoom_") ||
                room.name == "EntryRoom" || room.name.StartsWith("EntryRoom_")) // backwards compat
            {
                return "SafeElevatorRoom";
            }

            // For regular rooms, extract the display name from the instance name
            // Rooms are named "{displayName}_{index}" during spawning
            string instanceName = room.name;
            int lastUnderscoreIndex = instanceName.LastIndexOf('_');

            if (lastUnderscoreIndex > 0)
            {
                string potentialDisplayName = instanceName.Substring(0, lastUnderscoreIndex);

                // Verify this display name exists in the database
                var roomEntry = _roomDatabase.GetRoomByDisplayName(potentialDisplayName);
                if (roomEntry != null)
                {
                    // Return the best available identifier for this room
                    if (!string.IsNullOrEmpty(roomEntry.displayName)) return roomEntry.displayName;
                    if (!string.IsNullOrEmpty(roomEntry.roomID)) return roomEntry.roomID;
                    if (roomEntry.prefab != null) return roomEntry.prefab.name;
                    return potentialDisplayName;
                }
            }

            // Fallback: Search all rooms for any match
            foreach (var entry in _roomDatabase.AllRooms)
            {
                if (entry.prefab == null) continue;

                // Check by roomID being in the room name
                if (!string.IsNullOrEmpty(entry.roomID) && room.name.Contains(entry.roomID))
                {
                    if (!string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
                    return entry.roomID;
                }

                // Check by displayName being in the room name
                if (!string.IsNullOrEmpty(entry.displayName) && room.name.StartsWith(entry.displayName))
                {
                    return entry.displayName;
                }

                // Check by prefab name being in the room name
                if (room.name.StartsWith(entry.prefab.name))
                {
                    if (!string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
                    if (!string.IsNullOrEmpty(entry.roomID)) return entry.roomID;
                    return entry.prefab.name;
                }
            }

            // Last resort: return the instance name itself as identifier
            Debug.LogWarning($"[FloorGenerator] Could not find display name for room '{room.name}', using instance name");
            return instanceName;
        }

        /// <summary>
        /// Replays a cached floor layout, spawning rooms at their exact saved positions.
        /// Used for Delver-style persistence where floors look identical when revisited.
        /// </summary>
        /// <param name="layout">The cached layout to replay.</param>
        /// <returns>True if replay was successful.</returns>
        private bool ReplayCachedLayout(CachedFloorLayout layout)
        {
            if (layout == null || !layout.isValid || layout.roomPlacements.Count == 0)
            {
                Debug.LogError("[FloorGenerator] Invalid cached layout!");
                return false;
            }

            ClearFloor();
            OccupiedSpaceRegistry.Instance.ClearRegistry();

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorGenerator] Replaying cached layout for floor {_currentFloorNumber}: {layout.roomPlacements.Count} rooms");
            }

            foreach (var placement in layout.roomPlacements)
            {
                // Find the room entry by display name
                RoomPrefabDatabase.RoomEntry roomEntry = _roomDatabase.GetRoomByDisplayName(placement.prefabDisplayName);

                if (roomEntry == null || roomEntry.prefab == null)
                {
                    Debug.LogWarning($"[FloorGenerator] Could not find room '{placement.prefabDisplayName}' in database for replay!");
                    continue;
                }

                // Spawn the room at its exact cached position and rotation
                GameObject room = Instantiate(
                    roomEntry.prefab,
                    placement.position,
                    Quaternion.Euler(placement.rotationEuler),
                    transform
                );
                room.name = placement.roomInstanceName;

                // Register with OccupiedSpaceRegistry
                BoundsChecker boundsChecker = room.GetComponent<BoundsChecker>();
                if (boundsChecker != null)
                {
                    boundsChecker.RegisterWithRegistry();
                }

                _spawnedRooms.Add(room);

                // Track if exit room was spawned
                if (placement.roomInstanceName.StartsWith("ExitRoom") || placement.prefabDisplayName == "ExitElevatorRoom")
                {
                    _exitRoomSpawned = true;
                }
            }

            // After spawning all rooms, connect sockets that are overlapping
            ConnectOverlappingSockets();

            // Spawn blockades on any remaining unconnected sockets
            SpawnBlockadesOnUnconnectedSockets();

            // Rebuild navigation grid
            RebuildNavGridFromRegistry();

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorGenerator] Replay complete: {_spawnedRooms.Count} rooms, {_connectionsMade} connections, {_blockadeSpawnCount} blockades");
            }

            return true;
        }

        /// <summary>
        /// Connects overlapping sockets between rooms after replay.
        /// Finds sockets that are at the same position and connects them.
        /// </summary>
        private void ConnectOverlappingSockets()
        {
            EnsureConnectionSystemExists();

            // Collect all sockets from all rooms
            List<ConnectionSocket> allSockets = new List<ConnectionSocket>();
            foreach (GameObject room in _spawnedRooms)
            {
                if (room == null) continue;
                allSockets.AddRange(room.GetComponentsInChildren<ConnectionSocket>());
            }

            // Find overlapping socket pairs and connect them
            float connectionThreshold = 0.5f; // Sockets within this distance are considered overlapping
            HashSet<ConnectionSocket> connectedSockets = new HashSet<ConnectionSocket>();

            for (int i = 0; i < allSockets.Count; i++)
            {
                ConnectionSocket socketA = allSockets[i];
                if (socketA == null || connectedSockets.Contains(socketA)) continue;

                for (int j = i + 1; j < allSockets.Count; j++)
                {
                    ConnectionSocket socketB = allSockets[j];
                    if (socketB == null || connectedSockets.Contains(socketB)) continue;

                    // Check if sockets are at the same position
                    float distance = Vector3.Distance(socketA.Position, socketB.Position);
                    if (distance <= connectionThreshold && socketA.IsCompatibleWith(socketB.SocketType))
                    {
                        // Connect these sockets
                        bool success = _connectionSystem.ConnectRooms(socketA, socketB, socketB.transform.root, _doorPrefab);
                        if (success)
                        {
                            connectedSockets.Add(socketA);
                            connectedSockets.Add(socketB);
                            _connectionsMade++;
                        }
                        break;
                    }
                }
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

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