#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration
{
    /// <summary>
    /// Diagnostic tool that simulates generation checks on a room prefab.
    /// Mirrors every failure point in FloorGenerator to answer:
    ///   - Can this room be picked for generation?
    ///   - Can other rooms connect to it?
    ///   - Which specific rooms would connect or fail, and why?
    /// </summary>
    public class RoomDiagnosticTool : EditorWindow
    {
        #region Nested Types

        private enum DiagLevel { Pass, Warning, Fail }

        private class DiagMessage
        {
            public DiagLevel Level;
            public string Text;

            public DiagMessage(DiagLevel level, string text)
            {
                Level = level;
                Text = text;
            }
        }

        private class SimulationResult
        {
            public RoomPrefabDatabase.RoomEntry RoomEntry;
            public ConnectionSocket MatchingSocket;
            public bool WouldConnect;
            public Vector3 OverlapSize;
            public string FailReason;
            public bool Simulated;
        }

        #endregion

        #region Fields

        private GameObject _roomPrefab;
        private RoomPrefabDatabase _database;
        private Vector2 _scrollPosition;

        // Cached analysis data
        private bool _hasRun;
        private List<DiagMessage> _healthMessages = new();
        private ConnectionSocket[] _sockets = System.Array.Empty<ConnectionSocket>();
        private BoundsChecker _boundsChecker;
        private RoomPrefabDatabase.RoomEntry _databaseEntry;
        private bool _isInDatabase;
        private bool _databaseCacheStale;
        private Dictionary<Door.DoorType, int> _reachableCounts = new();
        private Dictionary<Door.DoorType, List<RoomCategory>> _reachableCategories = new();

        // Connection simulation
        private int _selectedSocketIndex;
        private string[] _socketNames = System.Array.Empty<string>();
        private List<SimulationResult> _simulationResults = new();

        // Foldouts
        private bool _foldHealth = true;
        private bool _foldSockets = true;
        private bool _foldDatabase = true;
        private bool _foldSimulation = true;

        // Styles (lazy init)
        private GUIStyle _passStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _failStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInit;

        #endregion

        #region Window Setup

        [MenuItem("Tools/Room Diagnostic Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<RoomDiagnosticTool>("Room Diagnostics");
            window.minSize = new Vector2(420, 500);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _passStyle = new GUIStyle(EditorStyles.label) { richText = true };
            _warnStyle = new GUIStyle(EditorStyles.label) { richText = true };
            _failStyle = new GUIStyle(EditorStyles.label) { richText = true };
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _boxStyle = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
        }

        #endregion

        #region OnGUI

        private void OnGUI()
        {
            InitStyles();

            // Header
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Room Generation Diagnostic Tool", _headerStyle);
            EditorGUILayout.Space(2);

            // Input fields
            EditorGUI.BeginChangeCheck();
            _roomPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Room Prefab", _roomPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _hasRun = false;
                _simulationResults.Clear();
            }

            EditorGUI.BeginChangeCheck();
            _database = (RoomPrefabDatabase)EditorGUILayout.ObjectField(
                "Room Database", _database, typeof(RoomPrefabDatabase), false);
            if (EditorGUI.EndChangeCheck())
            {
                _hasRun = false;
                _simulationResults.Clear();
            }

            if (_roomPrefab == null)
            {
                EditorGUILayout.HelpBox("Drag a room prefab here to analyze.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("Run Full Diagnostic", GUILayout.Height(28)))
            {
                RunFullDiagnostic();
            }
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("Auto-Fix Bounds", GUILayout.Height(28)))
            {
                AutoFixBounds();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!_hasRun)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "Click 'Run Full Diagnostic' to analyze this room prefab.\n\n" +
                    "This tool checks:\n" +
                    "  1. Prefab health (BoundsChecker, sockets, renderers, scale)\n" +
                    "  2. Socket configuration (type, forward, DoorSpawnPoint, blockades)\n" +
                    "  3. Database status (is this room in the pool? stale cache?)\n" +
                    "  4. Connection simulation (which rooms can actually connect?)",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);

            // Scrollable results
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPrefabHealthSection();
            EditorGUILayout.Space(4);
            DrawSocketDetailsSection();
            EditorGUILayout.Space(4);
            DrawDatabaseStatusSection();
            EditorGUILayout.Space(4);
            DrawConnectionSimulationSection();

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Section 1: Prefab Health

        private void DrawPrefabHealthSection()
        {
            _foldHealth = EditorGUILayout.BeginFoldoutHeaderGroup(_foldHealth, "Prefab Health");
            if (!_foldHealth) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

            EditorGUILayout.BeginVertical(_boxStyle);
            foreach (DiagMessage msg in _healthMessages)
            {
                DrawDiagMessage(msg);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region Section 2: Socket Details

        private void DrawSocketDetailsSection()
        {
            _foldSockets = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSockets, $"Socket Details ({_sockets.Length})");
            if (!_foldSockets) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

            if (_sockets.Length == 0)
            {
                EditorGUILayout.HelpBox("No ConnectionSockets found on this prefab.", MessageType.Error);
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.BeginVertical(_boxStyle);
            for (int i = 0; i < _sockets.Length; i++)
            {
                ConnectionSocket socket = _sockets[i];
                if (socket == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Socket header
                EditorGUILayout.LabelField($"Socket {i}: \"{socket.gameObject.name}\"", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                // Type
                EditorGUILayout.LabelField("Type", socket.SocketType.ToString());

                // Forward angle offset
                float angle = GetForwardAngleOffset(socket);
                if (angle > 0.1f)
                    EditorGUILayout.LabelField("Forward Offset", $"{angle:F1} degrees");
                else
                    EditorGUILayout.LabelField("Forward Offset", "0 (using raw transform.forward)");

                // Socket bounds
                if (socket.HasBounds)
                    DrawDiagLine(DiagLevel.Pass, $"Socket bounds calculated (center offset from pivot)");
                else
                    DrawDiagLine(DiagLevel.Warning, "Socket bounds NOT calculated! Connection uses pivot, not center. Click 'Calculate Socket Bounds' on the socket.");

                // DoorSpawnPoint
                if (socket.DoorSpawnPointTransform != null)
                    DrawDiagLine(DiagLevel.Pass, "DoorSpawnPoint assigned");
                else
                    DrawDiagLine(DiagLevel.Warning, "DoorSpawnPoint not assigned (door spawns at socket center)");

                // Door prefab
                if (socket.DoorPrefab != null)
                    DrawDiagLine(DiagLevel.Pass, $"Door prefab: {socket.DoorPrefab.name}");
                else
                    DrawDiagLine(DiagLevel.Pass, "No door prefab (will use database)");

                // Blockades
                if (socket.SpawnBlockadeIfUnconnected)
                {
                    if (socket.BlockadePrefabs != null && socket.BlockadePrefabs.Count > 0)
                    {
                        int validCount = socket.BlockadePrefabs.Count(b => b != null);
                        DrawDiagLine(DiagLevel.Pass, $"Blockade: {validCount} prefab(s)");
                    }
                    else
                    {
                        DrawDiagLine(DiagLevel.Warning, "Blockade enabled but no prefabs assigned!");
                    }
                }
                else
                {
                    DrawDiagLine(DiagLevel.Warning, "Blockade spawning disabled");
                }

                // Database reachability for this socket type
                if (_database != null && _reachableCounts.TryGetValue(socket.SocketType, out int count))
                {
                    if (count > 0)
                        DrawDiagLine(DiagLevel.Pass, $"{count} rooms in database can connect ({socket.SocketType})");
                    else
                        DrawDiagLine(DiagLevel.Fail, $"NO rooms in database have a {socket.SocketType} socket!");
                }

                // Ping button
                if (GUILayout.Button("Select in Hierarchy", EditorStyles.miniButton, GUILayout.Width(150)))
                {
                    Selection.activeGameObject = socket.gameObject;
                    EditorGUIUtility.PingObject(socket.gameObject);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region Section 3: Database Status

        private void DrawDatabaseStatusSection()
        {
            _foldDatabase = EditorGUILayout.BeginFoldoutHeaderGroup(_foldDatabase, "Database Status");
            if (!_foldDatabase) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

            EditorGUILayout.BeginVertical(_boxStyle);

            if (_database == null)
            {
                EditorGUILayout.HelpBox("No RoomPrefabDatabase assigned. Drag one in above to check database status.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            // Is room in database?
            if (_isInDatabase)
            {
                DrawDiagLine(DiagLevel.Pass, $"Found in database as \"{_databaseEntry.displayName}\"");

                // Enabled?
                if (_databaseEntry.isEnabled)
                    DrawDiagLine(DiagLevel.Pass, $"Enabled, spawn weight: {_databaseEntry.spawnWeight}");
                else
                    DrawDiagLine(DiagLevel.Fail, "DISABLED in database! Won't spawn.");

                // Category
                EditorGUILayout.LabelField("Category", _databaseEntry.category.ToString());

                // Sector
                EditorGUILayout.LabelField("Sector", _databaseEntry.sectorNumber.ToString());

                // Socket cache check
                if (_databaseCacheStale)
                {
                    DrawDiagLine(DiagLevel.Warning,
                        $"Database cache STALE! DB thinks: {_databaseEntry.socketCount} sockets, " +
                        $"Actual: {_sockets.Length}. Click 'Refresh' on the database.");
                }
                else
                {
                    DrawDiagLine(DiagLevel.Pass,
                        $"Socket cache up to date ({_databaseEntry.socketCount} sockets, " +
                        $"types: {string.Join(", ", _databaseEntry.socketTypes)})");
                }

                // IsValid check
                if (_databaseEntry.IsValid())
                    DrawDiagLine(DiagLevel.Pass, "Passes RoomEntry.IsValid() check");
                else
                    DrawDiagLine(DiagLevel.Fail, "FAILS RoomEntry.IsValid() — won't be selected by generator");
            }
            else
            {
                DrawDiagLine(DiagLevel.Fail, "NOT found in database! Generator cannot pick this room.");
                EditorGUILayout.HelpBox(
                    "This prefab is not in the RoomPrefabDatabase.\n" +
                    "Add it to the database's room list for it to appear in generation.",
                    MessageType.Error);
            }

            // Reachability summary
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Reachability (who can connect to this room)", EditorStyles.miniBoldLabel);

            foreach (var kvp in _reachableCounts)
            {
                string categories = "";
                if (_reachableCategories.TryGetValue(kvp.Key, out var cats) && cats.Count > 0)
                {
                    categories = $" ({string.Join(", ", cats.Distinct())})";
                }
                EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value} compatible rooms{categories}");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region Section 4: Connection Simulation

        private void DrawConnectionSimulationSection()
        {
            _foldSimulation = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSimulation, "Connection Simulation");
            if (!_foldSimulation) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

            if (_database == null)
            {
                EditorGUILayout.HelpBox("Assign a RoomPrefabDatabase to simulate connections.", MessageType.Warning);
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            if (_sockets.Length == 0)
            {
                EditorGUILayout.HelpBox("No sockets on this prefab — nothing to simulate.", MessageType.Warning);
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.BeginVertical(_boxStyle);

            // Socket selector
            EditorGUI.BeginChangeCheck();
            _selectedSocketIndex = EditorGUILayout.Popup("Source Socket", _selectedSocketIndex, _socketNames);
            if (EditorGUI.EndChangeCheck())
            {
                _simulationResults.Clear();
            }

            EditorGUILayout.Space(4);

            // Simulate buttons
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
            if (GUILayout.Button("Simulate All Connections", GUILayout.Height(24)))
            {
                RunConnectionSimulation(_sockets[_selectedSocketIndex]);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Results
            if (_simulationResults.Count > 0)
            {
                EditorGUILayout.Space(6);

                int passCount = _simulationResults.Count(r => r.WouldConnect);
                int failCount = _simulationResults.Count(r => r.Simulated && !r.WouldConnect);
                int pendingCount = _simulationResults.Count(r => !r.Simulated);

                EditorGUILayout.LabelField(
                    $"Results: {passCount} would connect, {failCount} would fail" +
                    (pendingCount > 0 ? $", {pendingCount} not yet simulated" : ""),
                    EditorStyles.miniBoldLabel);

                EditorGUILayout.Space(4);

                foreach (SimulationResult result in _simulationResults)
                {
                    DrawSimulationResult(result);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSimulationResult(SimulationResult result)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Room info line
            string roomName = !string.IsNullOrEmpty(result.RoomEntry.displayName)
                ? result.RoomEntry.displayName
                : (result.RoomEntry.prefab != null ? result.RoomEntry.prefab.name : "???");
            string category = result.RoomEntry.category.ToString();
            string weight = $"Wt:{result.RoomEntry.spawnWeight}";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{roomName}", EditorStyles.boldLabel, GUILayout.Width(180));
            EditorGUILayout.LabelField($"[{category}]", GUILayout.Width(100));
            EditorGUILayout.LabelField(weight, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            // Result
            if (result.Simulated)
            {
                if (result.WouldConnect)
                {
                    DrawDiagLine(DiagLevel.Pass, "Would connect successfully");
                }
                else
                {
                    DrawDiagLine(DiagLevel.Fail, result.FailReason);
                    if (result.OverlapSize != Vector3.zero)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField(
                            $"Overlap: {result.OverlapSize.x:F1} x {result.OverlapSize.y:F1} x {result.OverlapSize.z:F1}");
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Not yet simulated", EditorStyles.miniLabel);
            }

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (!result.Simulated)
            {
                if (GUILayout.Button("Simulate", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    SimulateSingleConnection(_sockets[_selectedSocketIndex], result);
                }
            }
            if (result.RoomEntry.prefab != null)
            {
                if (GUILayout.Button("Select in Project", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    Selection.activeObject = result.RoomEntry.prefab;
                    EditorGUIUtility.PingObject(result.RoomEntry.prefab);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }

        #endregion

        #region Diagnostic Logic

        private void RunFullDiagnostic()
        {
            _hasRun = true;
            _healthMessages.Clear();
            _simulationResults.Clear();
            _reachableCounts.Clear();
            _reachableCategories.Clear();

            // --- Prefab Health ---
            _boundsChecker = _roomPrefab.GetComponent<BoundsChecker>();
            if (_boundsChecker == null)
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Fail,
                    "CRITICAL: Missing BoundsChecker on room root! Room cannot be placed by generator."));
            }
            else
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass, "BoundsChecker found"));

                Bounds bounds = _boundsChecker.GetBounds();

                // Default/zero bounds check
                if (bounds.size == Vector3.one * 10f || bounds.size.magnitude < 0.01f)
                {
                    _healthMessages.Add(new DiagMessage(DiagLevel.Warning,
                        $"Bounds appear to be default or zero ({bounds.size}). Click 'Auto-Fix Bounds' to recalculate."));
                }
                else
                {
                    _healthMessages.Add(new DiagMessage(DiagLevel.Pass,
                        $"Bounds: center={FormatV3(bounds.center)}, size={FormatV3(bounds.size)}"));
                }

                // Check against actual geometry
                CheckBoundsVsGeometry();
            }

            // Sockets
            _sockets = _roomPrefab.GetComponentsInChildren<ConnectionSocket>(true);
            if (_sockets.Length == 0)
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Fail,
                    "No ConnectionSockets found! Room has no doorways for generation."));
            }
            else
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass,
                    $"{_sockets.Length} ConnectionSocket(s) found"));

                // Socket types summary
                var types = _sockets.Select(s => s.SocketType).Distinct().ToList();
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass,
                    $"Socket types: {string.Join(", ", types)}"));
            }

            // Build socket names for dropdown
            _socketNames = new string[_sockets.Length];
            for (int i = 0; i < _sockets.Length; i++)
            {
                _socketNames[i] = $"[{i}] {_sockets[i].gameObject.name} ({_sockets[i].SocketType})";
            }
            _selectedSocketIndex = Mathf.Clamp(_selectedSocketIndex, 0, Mathf.Max(0, _sockets.Length - 1));

            // Renderers
            Renderer[] renderers = _roomPrefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Warning, "No renderers found on this prefab!"));
            }
            else
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass, $"{renderers.Length} renderer(s)"));
            }

            // Scale
            Vector3 scale = _roomPrefab.transform.localScale;
            if (scale != Vector3.one)
            {
                if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.y, scale.z))
                {
                    _healthMessages.Add(new DiagMessage(DiagLevel.Warning,
                        $"Non-uniform scale: {FormatV3(scale)}. Can cause collision detection issues."));
                }
                else
                {
                    _healthMessages.Add(new DiagMessage(DiagLevel.Warning,
                        $"Uniform but non-default scale: {FormatV3(scale)}"));
                }
            }
            else
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass, "Scale is (1, 1, 1)"));
            }

            // --- Database Status ---
            CheckDatabaseStatus();

            Repaint();
        }

        private void CheckBoundsVsGeometry()
        {
            Renderer[] renderers = _roomPrefab.GetComponentsInChildren<Renderer>();
            List<Renderer> validRenderers = new();
            foreach (Renderer r in renderers)
            {
                if (r.GetComponentInParent<ConnectionSocket>() != null) continue;
                validRenderers.Add(r);
            }

            if (validRenderers.Count == 0) return;

            // Calculate actual local bounds (same as BoundsChecker.CalculateBoundsFromRenderers)
            Bounds localBounds = new Bounds(
                _roomPrefab.transform.InverseTransformPoint(validRenderers[0].bounds.center),
                Vector3.zero);

            foreach (Renderer r in validRenderers)
            {
                Vector3 localMin = _roomPrefab.transform.InverseTransformPoint(r.bounds.min);
                Vector3 localMax = _roomPrefab.transform.InverseTransformPoint(r.bounds.max);
                localBounds.Encapsulate(localMin);
                localBounds.Encapsulate(localMax);
            }

            Bounds currentBounds = _boundsChecker.GetBounds();
            Vector3 sizeDiff = localBounds.size - currentBounds.size;
            if (sizeDiff.magnitude > 0.5f)
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Warning,
                    $"Bounds don't match geometry! Diff: {FormatV3(sizeDiff)}. Click 'Auto-Fix Bounds'."));
            }
            else
            {
                _healthMessages.Add(new DiagMessage(DiagLevel.Pass, "Bounds match actual geometry"));
            }
        }

        private void CheckDatabaseStatus()
        {
            if (_database == null)
            {
                _isInDatabase = false;
                return;
            }

            // Find this prefab in the database
            _isInDatabase = false;
            _databaseEntry = null;

            foreach (RoomPrefabDatabase.RoomEntry entry in _database.AllRooms)
            {
                if (entry.prefab == _roomPrefab)
                {
                    _isInDatabase = true;
                    _databaseEntry = entry;
                    break;
                }
            }

            // Also check special rooms
            if (!_isInDatabase && _database.EntryElevatorRoom?.prefab == _roomPrefab)
            {
                _isInDatabase = true;
                _databaseEntry = _database.EntryElevatorRoom;
            }
            if (!_isInDatabase && _database.ExitElevatorRoom?.prefab == _roomPrefab)
            {
                _isInDatabase = true;
                _databaseEntry = _database.ExitElevatorRoom;
            }
            if (!_isInDatabase && _database.SafeRoom?.prefab == _roomPrefab)
            {
                _isInDatabase = true;
                _databaseEntry = _database.SafeRoom;
            }

            // Check cache staleness
            if (_isInDatabase && _databaseEntry != null)
            {
                _databaseCacheStale = _databaseEntry.socketCount != _sockets.Length;

                // Also check if socket types match
                if (!_databaseCacheStale)
                {
                    HashSet<Door.DoorType> actualTypes = new();
                    foreach (var s in _sockets) actualTypes.Add(s.SocketType);

                    HashSet<Door.DoorType> cachedTypes = new(_databaseEntry.socketTypes);
                    _databaseCacheStale = !actualTypes.SetEquals(cachedTypes);
                }
            }

            // Reachability: for each unique socket type on this room,
            // count how many OTHER rooms in the database can connect
            _reachableCounts.Clear();
            _reachableCategories.Clear();

            HashSet<Door.DoorType> myTypes = new();
            foreach (var s in _sockets) myTypes.Add(s.SocketType);

            foreach (Door.DoorType type in myTypes)
            {
                List<RoomPrefabDatabase.RoomEntry> compatible = _database.GetRoomsWithSocketType(type);

                // Exclude self
                compatible = compatible.Where(r => r.prefab != _roomPrefab).ToList();

                _reachableCounts[type] = compatible.Count;
                _reachableCategories[type] = compatible.Select(r => r.category).Distinct().ToList();
            }
        }

        #endregion

        #region Connection Simulation Logic

        private void RunConnectionSimulation(ConnectionSocket sourceSocket)
        {
            _simulationResults.Clear();

            if (_database == null || sourceSocket == null) return;

            // Get all rooms that can connect to this socket type
            List<RoomPrefabDatabase.RoomEntry> compatibleRooms =
                _database.GetRoomsWithSocketType(sourceSocket.SocketType);

            foreach (RoomPrefabDatabase.RoomEntry roomEntry in compatibleRooms)
            {
                if (roomEntry.prefab == null) continue;

                SimulationResult result = new SimulationResult
                {
                    RoomEntry = roomEntry,
                    Simulated = false
                };

                // Find matching socket in target prefab
                ConnectionSocket[] targetSockets = roomEntry.prefab.GetComponentsInChildren<ConnectionSocket>(true);
                ConnectionSocket targetSocket = null;
                foreach (var ts in targetSockets)
                {
                    if (ts.IsCompatibleWith(sourceSocket.SocketType))
                    {
                        targetSocket = ts;
                        break;
                    }
                }

                result.MatchingSocket = targetSocket;

                if (targetSocket == null)
                {
                    result.Simulated = true;
                    result.WouldConnect = false;
                    result.FailReason = "No compatible socket found in prefab (cache may be stale)";
                    _simulationResults.Add(result);
                    continue;
                }

                // Run the actual simulation
                SimulateSingleConnection(sourceSocket, result);
                _simulationResults.Add(result);
            }

            // Sort: passes first, then fails
            _simulationResults.Sort((a, b) =>
            {
                if (a.WouldConnect && !b.WouldConnect) return -1;
                if (!a.WouldConnect && b.WouldConnect) return 1;
                return string.Compare(
                    a.RoomEntry.displayName ?? a.RoomEntry.prefab?.name,
                    b.RoomEntry.displayName ?? b.RoomEntry.prefab?.name,
                    System.StringComparison.Ordinal);
            });

            Repaint();
        }

        /// <summary>
        /// Simulates a single room connection without instantiating anything.
        /// Mirrors the math from FloorGenerator.TrySpawnAndConnectRoomWithTwoPhaseCheck() lines 548-562.
        /// </summary>
        private void SimulateSingleConnection(ConnectionSocket sourceSocket, SimulationResult result)
        {
            result.Simulated = true;

            if (result.MatchingSocket == null)
            {
                // Find it now
                ConnectionSocket[] targetSockets = result.RoomEntry.prefab.GetComponentsInChildren<ConnectionSocket>(true);
                foreach (var ts in targetSockets)
                {
                    if (ts.IsCompatibleWith(sourceSocket.SocketType))
                    {
                        result.MatchingSocket = ts;
                        break;
                    }
                }

                if (result.MatchingSocket == null)
                {
                    result.WouldConnect = false;
                    result.FailReason = "No compatible socket found in prefab";
                    return;
                }
            }

            ConnectionSocket targetSocket = result.MatchingSocket;
            GameObject targetPrefab = result.RoomEntry.prefab;

            // Check target has BoundsChecker
            BoundsChecker targetBounds = targetPrefab.GetComponent<BoundsChecker>();
            if (targetBounds == null)
            {
                result.WouldConnect = false;
                result.FailReason = "Target room missing BoundsChecker!";
                return;
            }

            // The generation system only uses broad-phase collision (OccupiedSpaceRegistry)
            // which checks against ALL placed rooms except the source room.
            // There is no narrow-phase check between the connecting pair anymore.
            // So if socket types match and both have BoundsCheckers, the connection succeeds.
            //
            // At runtime, the only thing that would prevent placement is if the target room's
            // bounds overlap with a THIRD room that's already placed (broad-phase).
            // We can't simulate that here since it depends on the full floor layout.

            result.WouldConnect = true;
            result.FailReason = "";
        }

        #endregion

        #region Auto-Fix

        private void AutoFixBounds()
        {
            BoundsChecker boundsChecker = _roomPrefab.GetComponent<BoundsChecker>();
            if (boundsChecker == null)
            {
                Debug.LogError($"[RoomDiagnostic] Room '{_roomPrefab.name}' is missing BoundsChecker component!");
                return;
            }

            Undo.RecordObject(boundsChecker, "Auto-Fix Bounds");
            boundsChecker.CalculateBoundsFromRenderers();
            EditorUtility.SetDirty(boundsChecker);
            EditorUtility.SetDirty(_roomPrefab);

            Debug.Log($"[RoomDiagnostic] Auto-fixed bounds for '{_roomPrefab.name}': " +
                      $"Center={FormatV3(boundsChecker.GetBounds().center)}, Size={FormatV3(boundsChecker.GetBounds().size)}");

            // Re-run diagnostic with updated bounds
            RunFullDiagnostic();
        }

        #endregion

        #region Drawing Helpers

        private void DrawDiagMessage(DiagMessage msg)
        {
            string prefix;
            Color color;

            switch (msg.Level)
            {
                case DiagLevel.Pass:
                    prefix = "<color=#55CC55>PASS</color>";
                    color = new Color(0.3f, 0.9f, 0.3f);
                    break;
                case DiagLevel.Warning:
                    prefix = "<color=#FFBB33>WARN</color>";
                    color = new Color(1f, 0.75f, 0.2f);
                    break;
                case DiagLevel.Fail:
                    prefix = "<color=#FF4444>FAIL</color>";
                    color = new Color(1f, 0.3f, 0.3f);
                    break;
                default:
                    prefix = "";
                    color = Color.white;
                    break;
            }

            EditorGUILayout.LabelField($"  {prefix}  {msg.Text}", _passStyle);
        }

        private void DrawDiagLine(DiagLevel level, string text)
        {
            DrawDiagMessage(new DiagMessage(level, text));
        }

        private static string FormatV3(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }

        /// <summary>
        /// Gets the forward angle offset via serialized property (since it's a private field).
        /// </summary>
        private float GetForwardAngleOffset(ConnectionSocket socket)
        {
            SerializedObject so = new SerializedObject(socket);
            SerializedProperty prop = so.FindProperty("_forwardAngleOffset");
            return prop != null ? prop.floatValue : 0f;
        }

        #endregion
    }
}
#endif
