using System.Collections.Generic;
using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Enhanced     floor generator with TWO-PHASE collision detection, random socket selection, and blockade system.
    /// PHASE 1 (BROAD): Registry checks PADDED bounds before instantiation
    /// PHASE 2 (NARROW): DoorConnectionSystem checks TIGHT bounds during connection
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
        [SerializeField] private bool _showDebugLogs = true;

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

        [Header("Runtime Info")]
        [SerializeField] private List<GameObject> _spawnedRooms = new();
        [SerializeField] private List<GameObject> _spawnedBlockades = new();
        [SerializeField] private List<ConnectionSocket> _availableSockets = new();
        [SerializeField] private int _connectionsMade;
        [SerializeField] private int _creditsRemaining;
        [SerializeField] private int _blockadeSpawnCount;
        [SerializeField] private int _currentRegenerationAttempt;
        [SerializeField] private bool _exitRoomSpawned;

        private DoorConnectionSystem _connectionSystem;

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
        /// Automatically regenerates if the budget wasn't fully utilized.
        /// </summary>
        public void GenerateFloor()
        {
            _currentRegenerationAttempt = 0;

            // Loop-based retry instead of recursion to avoid stack overflow
            while (true)
            {
                GenerateFloorInternal();

                float budgetUsagePercentage = 1f - ((float)_creditsRemaining / _doorCreditBudget);
                bool budgetSufficient = budgetUsagePercentage >= _minBudgetUsageThreshold;

                // CRITICAL: Exit room must ALWAYS spawn
                if (!_exitRoomSpawned)
                {
                    if (_showDebugLogs)
                        Debug.LogWarning($"[FloorGenerator] Exit room failed to spawn! Retrying generation...");
                    
                    _currentRegenerationAttempt++;
                    if (_currentRegenerationAttempt >= _maxGenerationAttempts)
                    {
                        Debug.LogError($"[FloorGenerator] === GENERATION FAILED === Exit room could not spawn after {_maxGenerationAttempts} attempts!");
                        break;
                    }
                    continue;
                }

                // Success: budget threshold met AND exit room spawned
                if (budgetSufficient || _creditsRemaining == 0)
                {
                    if (_showDebugLogs && _currentRegenerationAttempt > 0)
                    {
                        Debug.Log($"[FloorGenerator] === GENERATION SUCCESSFUL on attempt {_currentRegenerationAttempt + 1} === " +
                                  $"Budget usage: {budgetUsagePercentage:P1}");
                    }
                    break;
                }

                // Failure: max attempts reached
                if (!_enableRetryOnIncompleteGeneration || _currentRegenerationAttempt >= _maxGenerationAttempts - 1)
                {
                    if (_showDebugLogs)
                    {
                        Debug.LogWarning($"[FloorGenerator] === GENERATION INCOMPLETE === " +
                                       $"Final attempt ({_currentRegenerationAttempt + 1}/{_maxGenerationAttempts}) " +
                                       $"used {budgetUsagePercentage:P1} of budget " +
                                       $"({_doorCreditBudget - _creditsRemaining}/{_doorCreditBudget} credits)");
                    }
                    break;
                }

                // Retry
                _currentRegenerationAttempt++;
                if (_showDebugLogs)
                {
                    Debug.LogWarning($"[FloorGenerator] Budget usage {budgetUsagePercentage:P1} below threshold {_minBudgetUsageThreshold:P0}. " +
                                   $"Retrying... (attempt {_currentRegenerationAttempt + 1}/{_maxGenerationAttempts})");
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

            ClearFloor();

            OccupiedSpaceRegistry.Instance.ClearRegistry();

            if (_showDebugLogs)
            {
                string attemptInfo = _currentRegenerationAttempt > 0 
                    ? $" (Regeneration Attempt {_currentRegenerationAttempt}/{_maxGenerationAttempts})" 
                    : "";
                Debug.Log($"[FloorGenerator] === STARTING FLOOR GENERATION{attemptInfo} === Budget: {_doorCreditBudget} credits");
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

            // Reserve 1 credit for exit room - stop at 1 remaining
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

            if (_showDebugLogs)
            {
                string attemptInfo = _currentRegenerationAttempt > 0 
                    ? $" (Attempt {_currentRegenerationAttempt + 1}/{_maxGenerationAttempts})" 
                    : "";
                Debug.Log($"[FloorGenerator] === GENERATION PASS COMPLETE{attemptInfo} === " +
                          $"Spawned {_spawnedRooms.Count} rooms, {_connectionsMade} connections, " +
                          $"{_blockadeSpawnCount} blockades, {_creditsRemaining} credits remaining");
                Debug.Log($"[FloorGenerator] {OccupiedSpaceRegistry.Instance.GetRegistryStats()}");
            }
        }

        private GameObject SpawnStartingRoom()
        {
            RoomPrefabDatabase.RoomEntry startRoomEntry = _roomDatabase.EntryElevatorRoom;

            if (startRoomEntry == null || startRoomEntry.prefab == null)
            {
                Debug.LogError("[FloorGenerator] Entry elevator room not set in database!");
                return null;
            }

            GameObject startRoom = SpawnRoom(startRoomEntry.prefab, Vector3.zero, Quaternion.identity, "EntryRoom", registerNow: true);
            _spawnedRooms.Add(startRoom);

            if (_showDebugLogs)
                Debug.Log($"[FloorGenerator] Spawned starting room: {startRoomEntry.displayName}");

            return startRoom;
        }

        /// <summary>
        /// Spawns the exit elevator room at one of the unconnected sockets.
        /// GUARANTEES at least one exit room is always present in the floor.
        /// Uses the reserved credit and proper socket alignment via DoorConnectionSystem.
        /// </summary>
        private void SpawnExitRoomAtTerminus()
        {
            RoomPrefabDatabase.RoomEntry exitRoomEntry = _roomDatabase.ExitElevatorRoom;

            if (exitRoomEntry == null || exitRoomEntry.prefab == null)
            {
                Debug.LogError("[FloorGenerator] Exit elevator room not set in database. Cannot guarantee exit room!");
                return;
            }

            // Collect all unconnected sockets from spawned rooms
            List<ConnectionSocket> candidateSockets = new List<ConnectionSocket>();
            foreach (GameObject room in _spawnedRooms)
            {
                if (room == null) continue;

                ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
                foreach (ConnectionSocket socket in sockets)
                {
                    if (socket != null && !socket.IsConnected && socket.ConnectedSocket == null)
                    {
                        // Check if exit room has compatible socket type
                        ConnectionSocket exitSocket = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, socket.SocketType);
                        if (exitSocket != null)
                        {
                            candidateSockets.Add(socket);
                        }
                    }
                }
            }

            // If no unconnected sockets, try ANY compatible socket (will use credit to spawn new room)
            if (candidateSockets.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning("[FloorGenerator] No unconnected sockets available for exit room. Will use any compatible socket.");

                foreach (GameObject room in _spawnedRooms)
                {
                    if (room == null) continue;

                    ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();
                    foreach (ConnectionSocket socket in sockets)
                    {
                        if (socket != null)
                        {
                            ConnectionSocket exitSocket = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, socket.SocketType);
                            if (exitSocket != null)
                            {
                                candidateSockets.Add(socket);
                            }
                        }
                    }
                }
            }

            if (candidateSockets.Count == 0)
            {
                Debug.LogError("[FloorGenerator] CRITICAL: No compatible sockets found for exit room! Floor generation incomplete.");
                return;
            }

            // Shuffle the list to try sockets in random order
            for (int i = candidateSockets.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                ConnectionSocket temp = candidateSockets[i];
                candidateSockets[i] = candidateSockets[j];
                candidateSockets[j] = temp;
            }

            // Try each socket until one works
            foreach (ConnectionSocket selectedSocket in candidateSockets)
            {
                // Find the compatible socket in the exit room prefab
                ConnectionSocket exitSocketPrefab = FindCompatibleSocketInPrefab(exitRoomEntry.prefab, selectedSocket.SocketType);

                if (exitSocketPrefab == null)
                    continue;

                // Store prefab's original transform state
                Vector3 prefabOriginalPos = exitRoomEntry.prefab.transform.position;
                Quaternion prefabOriginalRot = exitRoomEntry.prefab.transform.rotation;
                
                // Temporarily reset prefab to identity for clean calculation
                exitRoomEntry.prefab.transform.position = Vector3.zero;
                exitRoomEntry.prefab.transform.rotation = Quaternion.identity;

                // Calculate alignment using DoorConnectionSystem
                var (roomPosition, roomRotation) = _connectionSystem.CalculateTargetRoomTransform(
                    selectedSocket, 
                    exitSocketPrefab, 
                    exitRoomEntry.prefab.transform
                );
                
                // Restore prefab's original transform
                exitRoomEntry.prefab.transform.position = prefabOriginalPos;
                exitRoomEntry.prefab.transform.rotation = prefabOriginalRot;

                // Check broad-phase collision before spawning
                BoundsChecker exitBoundsChecker = exitRoomEntry.prefab.GetComponent<BoundsChecker>();
                if (exitBoundsChecker != null)
                {
                    // Transform bounds to world space using calculated position/rotation
                    exitRoomEntry.prefab.transform.position = roomPosition;
                    exitRoomEntry.prefab.transform.rotation = roomRotation;
                    
                    Bounds worldBounds = exitBoundsChecker.GetPaddedBounds();
                    
                    // Restore prefab transform
                    exitRoomEntry.prefab.transform.position = prefabOriginalPos;
                    exitRoomEntry.prefab.transform.rotation = prefabOriginalRot;

                    if (OccupiedSpaceRegistry.Instance.IsSpaceOccupied(worldBounds, (BoundsChecker)null))
                    {
                        if (_showDebugLogs)
                            Debug.Log($"[FloorGenerator] Exit room would collide at socket '{selectedSocket.name}', trying next socket...");
                        continue;
                    }
                }

                // Spawn the exit room
                GameObject exitRoom = SpawnRoom(
                    exitRoomEntry.prefab,
                    roomPosition,
                    roomRotation,
                    "ExitRoom",
                    registerNow: true
                );

                if (exitRoom == null)
                {
                    Debug.LogWarning("[FloorGenerator] Failed to instantiate exit room, trying next socket...");
                    continue;
                }

                _spawnedRooms.Add(exitRoom);

                // Find the matching socket in the spawned exit room
                ConnectionSocket spawnedExitSocket = FindMatchingSocketInInstance(exitRoom, exitSocketPrefab);

                if (spawnedExitSocket == null)
                {
                    Debug.LogWarning("[FloorGenerator] Could not find exit socket in spawned exit room, trying next socket...");
                    
                    // Clean up failed spawn
                    _spawnedRooms.Remove(exitRoom);
                    if (Application.isPlaying)
                        Destroy(exitRoom);
                    else
                        DestroyImmediate(exitRoom);
                    
                    continue;
                }

                // Connect the sockets using DoorConnectionSystem (performs narrow-phase check)
                bool connectionSuccess = _connectionSystem.ConnectRooms(selectedSocket, spawnedExitSocket, exitRoom.transform, _doorPrefab);

                if (connectionSuccess)
                {
                    // Success! Deduct the reserved credit
                    _creditsRemaining--;
                    _connectionsMade++;
                    _exitRoomSpawned = true;
                    
                    if (_showDebugLogs)
                        Debug.Log($"[FloorGenerator] ✓ Exit room spawned at '{selectedSocket.name}' (1 credit used)");
                    
                    return; // Exit room placed successfully
                }
                else
                {
                    // Narrow-phase collision detected, clean up and try next socket
                    if (_showDebugLogs)
                        Debug.Log($"[FloorGenerator] Narrow-phase collision for exit room at socket '{selectedSocket.name}', trying next socket...");
                    
                    _spawnedRooms.Remove(exitRoom);
                    
                    BoundsChecker boundsChecker = exitRoom.GetComponent<BoundsChecker>();
                    if (boundsChecker != null)
                        boundsChecker.UnregisterFromRegistry();
                    
                    if (Application.isPlaying)
                        Destroy(exitRoom);
                    else
                        DestroyImmediate(exitRoom);
                }
            }

            // If we get here, no socket worked
            Debug.LogError("[FloorGenerator] CRITICAL: Failed to place exit room at any compatible socket! Floor generation incomplete.");
        }

        /// <summary>
        /// Spawns blockades on all unconnected sockets after generation completes.
        /// Each socket can have its own list of blockade prefabs.
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
                    {
                        if (_showDebugLogs && socket != null && (socket.IsConnected || socket.ConnectedSocket != null))
                            Debug.Log($"[FloorGenerator] Skipping connected socket '{socket.name}' (IsConnected: {socket.IsConnected}, ConnectedTo: {(socket.ConnectedSocket != null ? socket.ConnectedSocket.name : "null")})");
                        continue;
                    }

                    GameObject blockade = socket.SpawnBlockade(transform);

                    if (blockade != null)
                    {
                        _blockadeSpawnCount++;
                        _spawnedBlockades.Add(blockade);

                        if (_showDebugLogs)
                            Debug.Log($"[FloorGenerator] Spawned blockade '{blockade.name}' at unconnected socket '{socket.name}'");
                    }
                }
            }

            if (_showDebugLogs && _blockadeSpawnCount > 0)
                Debug.Log($"[FloorGenerator] Spawned {_blockadeSpawnCount} blockades on unconnected sockets");
        }

        /// <summary>
        /// Attempts to spawn and connect a room using TWO-PHASE collision detection.
        /// This is the key method that implements the proper collision checking.
        /// </summary>
        private bool TrySpawnAndConnectRoomWithTwoPhaseCheck(ConnectionSocket sourceSocket)
        {
            BoundsChecker sourceBounds = sourceSocket.GetComponentInParent<BoundsChecker>();

            if (sourceBounds == null)
            {
                Debug.LogWarning($"[FloorGenerator] Source socket '{sourceSocket.name}' has no BoundsChecker in parent!");
                return false;
            }

            List<RoomPrefabDatabase.RoomEntry> compatibleRooms = _roomDatabase.GetRoomsWithSocketType(sourceSocket.SocketType);

            if (compatibleRooms.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[FloorGenerator] No compatible rooms for socket type {sourceSocket.SocketType}");
                return false;
            }

            for (int attempt = 0; attempt < _maxAttemptsPerSocket; attempt++)
            {
                RoomPrefabDatabase.RoomEntry selectedRoom = SelectRoomWithPreferences(compatibleRooms, sourceSocket.SocketType);

                if (selectedRoom == null || selectedRoom.prefab == null)
                {
                    Debug.LogWarning("[FloorGenerator] Selected room is null!");
                    continue;
                }

                ConnectionSocket targetSocket = FindCompatibleSocketInPrefab(selectedRoom.prefab, sourceSocket.SocketType);

                if (targetSocket == null)
                {
                    if (_showDebugLogs)
                        Debug.LogWarning($"[FloorGenerator] Room '{selectedRoom.displayName}' has no compatible socket!");
                    continue;
                }

                Quaternion proposedRotation = Quaternion.LookRotation(-sourceSocket.Forward) * Quaternion.Inverse(targetSocket.Rotation) * selectedRoom.prefab.transform.rotation;
                
                Vector3 targetSocketLocalPos = targetSocket.transform.localPosition;
                Vector3 targetSocketWorldOffset = proposedRotation * targetSocketLocalPos;
                Vector3 proposedPosition = sourceSocket.Position - targetSocketWorldOffset;

                BoundsChecker targetBoundsChecker = selectedRoom.prefab.GetComponent<BoundsChecker>();
                if (targetBoundsChecker == null)
                {
                    Debug.LogWarning($"[FloorGenerator] Room '{selectedRoom.displayName}' has no BoundsChecker!");
                    continue;
                }

                Bounds paddedBounds = targetBoundsChecker.GetPaddedBounds(proposedPosition, proposedRotation);

                bool broadPhasePass = !OccupiedSpaceRegistry.Instance.IsSpaceOccupied(paddedBounds);

                if (!broadPhasePass)
                {
                    if (_showDebugLogs)
                        Debug.LogWarning($"[FloorGenerator] BROAD-PHASE failed for '{selectedRoom.displayName}' (attempt {attempt + 1}/{_maxAttemptsPerSocket})");
                    continue;
                }

                if (_showDebugLogs)
                    Debug.Log($"[FloorGenerator] BROAD-PHASE passed for '{selectedRoom.displayName}' at {proposedPosition}");

                GameObject newRoom = SpawnRoom(selectedRoom.prefab, proposedPosition, proposedRotation,
                    $"{selectedRoom.displayName}_{_spawnedRooms.Count}", registerNow: false);
                _spawnedRooms.Add(newRoom);

                ConnectionSocket newRoomSocket = FindMatchingSocketInInstance(newRoom, targetSocket);

                if (newRoomSocket == null)
                {
                    Debug.LogError($"[FloorGenerator] Could not find matching socket in instantiated room!");
                    if (Application.isPlaying)
                        Destroy(newRoom);
                    else
                        DestroyImmediate(newRoom);
                    _spawnedRooms.Remove(newRoom);
                    continue;
                }

                bool narrowPhasePass = _connectionSystem.ConnectRooms(sourceSocket, newRoomSocket, newRoom.transform, _doorPrefab);

                if (narrowPhasePass)
                {
                    if (_showDebugLogs)
                        Debug.Log($"[FloorGenerator] NARROW-PHASE passed! Connected '{selectedRoom.displayName}' at final position {newRoom.transform.position}");

                    if (!Application.isPlaying)
                    {
                        BoundsChecker boundsChecker = newRoom.GetComponent<BoundsChecker>();
                        if (boundsChecker != null)
                        {
                            boundsChecker.RegisterWithRegistry();
                            if (_showDebugLogs)
                                Debug.Log($"[FloorGenerator] Manually registered '{newRoom.name}' at final position {newRoom.transform.position}");
                        }
                    }

                    AddRoomSocketsToList(newRoom, newRoomSocket);

                    return true;
                }
                else
                {
                    if (_showDebugLogs)
                        Debug.LogWarning($"[FloorGenerator] NARROW-PHASE failed for '{newRoom.name}' (attempt {attempt + 1}/{_maxAttemptsPerSocket})");

                    if (Application.isPlaying)
                        Destroy(newRoom);
                    else
                        DestroyImmediate(newRoom);
                    
                    _spawnedRooms.Remove(newRoom);
                    continue;
                }
            }

            if (_showDebugLogs)
                Debug.LogWarning($"[FloorGenerator] Failed to place room at socket '{sourceSocket.name}' after {_maxAttemptsPerSocket} attempts");

            return false;
        }

        /// <summary>
        /// Finds a compatible socket in a PREFAB (not instantiated).
        /// </summary>
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

        /// <summary>
        /// Finds the matching socket in an instantiated room that corresponds to the prefab socket.
        /// </summary>
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

        private ConnectionSocket FindCompatibleSocket(GameObject room, Door.DoorType socketType, ConnectionSocket excludeSocket = null)
        {
            ConnectionSocket[] sockets = room.GetComponentsInChildren<ConnectionSocket>();

            foreach (ConnectionSocket socket in sockets)
            {
                if (!socket.IsConnected &&
                    socket.IsCompatibleWith(socketType) &&
                    socket != excludeSocket)
                {
                    return socket;
                }
            }

            return null;
        }

        /// <summary>
        /// Changed from Queue (Enqueue) to List (Add) for random selection.
        /// </summary>
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

            if (registerNow && !Application.isPlaying)
            {
                BoundsChecker boundsChecker = room.GetComponent<BoundsChecker>();
                if (boundsChecker != null)
                {
                    boundsChecker.RegisterWithRegistry();
                    if (_showDebugLogs)
                        Debug.Log($"[FloorGenerator] Manually registered room '{roomName}' in Edit mode at {position}");
                }
            }

            return room;
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
                        if (socket != null)
                        {
                            socket.Disconnect();
                        }
                    }
                }
            }

            foreach (GameObject room in _spawnedRooms)
            {
                if (room != null)
                {
                    BoundsChecker boundsChecker = room.GetComponent<BoundsChecker>();
                    if (boundsChecker != null)
                    {
                        boundsChecker.UnregisterFromRegistry();
                    }

                    if (Application.isPlaying)
                        Destroy(room);
                    else
                        DestroyImmediate(room);
                }
            }

            // Destroy all blockades
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
            // Note: _currentRegenerationAttempt is NOT reset here
            // It's only reset in GenerateFloor() to track across regeneration cycles

            if (OccupiedSpaceRegistry.Instance != null)
            {
                OccupiedSpaceRegistry.Instance.ClearRegistry();
            }

            if (_showDebugLogs)
                Debug.Log("[FloorGenerator] Floor cleared.");
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