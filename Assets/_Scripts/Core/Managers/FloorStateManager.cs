using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// Manages floor states and persistence across the entire game session.
    /// Handles seed-based generation, state tracking, and save/load operations.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class FloorStateManager : MonoBehaviour
    {
        #region Singleton

        private static FloorStateManager _instance;

        public static FloorStateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FloorStateManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("FloorStateManager");
                        _instance = go.AddComponent<FloorStateManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-initialize if configured (for Play Mode and runtime builds)
            if (_autoInitializeOnAwake && !_isInitialized)
            {
                Initialize(_worldSeed); // Use serialized seed (0 = random)
                if (_showDebugLogs)
                {
                    Debug.Log($"[FloorStateManager] Auto-initialized on Awake with seed: {_worldSeed}");
                }
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Initialization")]
        [Tooltip("If true, automatically initializes with the serialized World Seed on Awake. Enable this for Play Mode testing and runtime builds.")]
        [SerializeField] private bool _autoInitializeOnAwake = false;

        [Header("Configuration")]
        [Tooltip("Master seed for the entire game world. All floor seeds derive from this. Set to 0 for random seed.")]
        [SerializeField] private int _worldSeed;

        [Tooltip("Prime number multiplier for floor seed generation. Ensures good distribution.")]
        [SerializeField] private int _seedMultiplier = 7919;

        [Header("Runtime State")]
        [Tooltip("Current floor number the player is on.")]
        [SerializeField] private int _currentFloorNumber = 1;

        [Header("Debug")]
        [Tooltip("Show detailed logging for floor state operations?")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private Dictionary<int, FloorState> _floorStates = new Dictionary<int, FloorState>();
        private bool _isInitialized = false;

        private const string SAVE_FILE_NAME = "floor_states.json";

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the master world seed for this game session.
        /// </summary>
        public int WorldSeed => _worldSeed;

        /// <summary>
        /// Gets or sets the current floor number the player is on.
        /// </summary>
        public int CurrentFloorNumber
        {
            get => _currentFloorNumber;
            set
            {
                if (value < 1)
                {
                    Debug.LogWarning("[FloorStateManager] Attempted to set floor number below 1. Clamping to 1.");
                    _currentFloorNumber = 1;
                }
                else
                {
                    _currentFloorNumber = value;
                }
            }
        }

        /// <summary>
        /// Gets whether the FloorStateManager has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the FloorStateManager with a new world seed.
        /// Call this at the start of a new game.
        /// </summary>
        /// <param name="worldSeed">Master seed for the entire game world. If 0, generates random seed.</param>
        public void Initialize(int worldSeed = 0)
        {
            if (worldSeed == 0)
            {
                _worldSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            else
            {
                _worldSeed = worldSeed;
            }

            _floorStates.Clear();
            _currentFloorNumber = 1;
            _isInitialized = true;

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorStateManager] Initialized with world seed: {_worldSeed}");
            }
        }

        #endregion

        #region Floor Seed Generation

        /// <summary>
        /// Generates a unique, deterministic seed for a specific floor number.
        /// Uses the world seed and prime multiplier for good distribution.
        /// </summary>
        /// <param name="floorNumber">The floor number to generate a seed for.</param>
        /// <returns>Deterministic seed for the specified floor.</returns>
        public int GetFloorSeed(int floorNumber)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[FloorStateManager] Attempted to get floor seed before initialization. Initializing with random seed.");
                Initialize();
            }

            int floorSeed = _worldSeed + (floorNumber * _seedMultiplier);
            
            if (_showDebugLogs)
            {
                Debug.Log($"[FloorStateManager] Generated seed {floorSeed} for floor {floorNumber}");
            }

            return floorSeed;
        }

        #endregion

        #region Floor State Management

        /// <summary>
        /// Gets the floor state for a specific floor number.
        /// Creates a new state if one doesn't exist.
        /// </summary>
        /// <param name="floorNumber">Floor number to get state for.</param>
        /// <returns>FloorState for the specified floor.</returns>
        public FloorState GetOrCreateFloorState(int floorNumber)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[FloorStateManager] Attempted to get floor state before initialization. Initializing with random seed.");
                Initialize();
            }

            if (!_floorStates.ContainsKey(floorNumber))
            {
                FloorState newState = new FloorState
                {
                    floorNumber = floorNumber,
                    generationSeed = GetFloorSeed(floorNumber),
                    isVisited = false,
                    isCleared = false,
                    collectedItems = new Dictionary<string, bool>(),
                    defeatedEnemies = new List<string>(),
                    openedDoors = new Dictionary<string, bool>(),
                    lastPlayerPosition = Vector3.zero,
                    timeSpentOnFloor = 0f
                };

                _floorStates[floorNumber] = newState;

                if (_showDebugLogs)
                {
                    Debug.Log($"[FloorStateManager] Created new floor state for floor {floorNumber} with seed {newState.generationSeed}");
                }
            }

            return _floorStates[floorNumber];
        }

        /// <summary>
        /// Gets the current floor's state.
        /// </summary>
        /// <returns>FloorState for the current floor.</returns>
        public FloorState GetCurrentFloorState()
        {
            return GetOrCreateFloorState(_currentFloorNumber);
        }

        /// <summary>
        /// Checks if a specific floor has been visited before.
        /// </summary>
        /// <param name="floorNumber">Floor number to check.</param>
        /// <returns>True if the floor has been visited.</returns>
        public bool HasVisitedFloor(int floorNumber)
        {
            if (!_floorStates.ContainsKey(floorNumber))
            {
                return false;
            }

            return _floorStates[floorNumber].isVisited;
        }

        /// <summary>
        /// Marks the current floor as visited.
        /// </summary>
        public void MarkCurrentFloorAsVisited()
        {
            FloorState currentState = GetCurrentFloorState();
            currentState.isVisited = true;

            if (_showDebugLogs)
            {
                Debug.Log($"[FloorStateManager] Marked floor {_currentFloorNumber} as visited");
            }
        }

        #endregion

        #region Save/Load System

        /// <summary>
        /// Saves all floor states to JSON file.
        /// </summary>
        public void SaveToJSON()
        {
            try
            {
                SaveData saveData = new SaveData
                {
                    worldSeed = _worldSeed,
                    currentFloorNumber = _currentFloorNumber,
                    floorStates = new List<FloorState>(_floorStates.Values)
                };

                string json = JsonUtility.ToJson(saveData, true);
                string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
                File.WriteAllText(savePath, json);

                if (_showDebugLogs)
                {
                    Debug.Log($"[FloorStateManager] Saved game state to {savePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FloorStateManager] Failed to save game state: {e.Message}");
            }
        }

        /// <summary>
        /// Loads all floor states from JSON file.
        /// </summary>
        /// <returns>True if load was successful.</returns>
        public bool LoadFromJSON()
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

                if (!File.Exists(savePath))
                {
                    if (_showDebugLogs)
                    {
                        Debug.Log($"[FloorStateManager] No save file found at {savePath}");
                    }
                    return false;
                }

                string json = File.ReadAllText(savePath);
                SaveData saveData = JsonUtility.FromJson<SaveData>(json);

                _worldSeed = saveData.worldSeed;
                _currentFloorNumber = saveData.currentFloorNumber;

                _floorStates.Clear();
                foreach (FloorState state in saveData.floorStates)
                {
                    _floorStates[state.floorNumber] = state;
                }

                _isInitialized = true;

                if (_showDebugLogs)
                {
                    Debug.Log($"[FloorStateManager] Loaded game state from {savePath}. World seed: {_worldSeed}, Current floor: {_currentFloorNumber}");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FloorStateManager] Failed to load game state: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a save file exists.
        /// </summary>
        /// <returns>True if a save file exists.</returns>
        public bool SaveFileExists()
        {
            string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
            return File.Exists(savePath);
        }

        /// <summary>
        /// Deletes the save file.
        /// </summary>
        public void DeleteSaveFile()
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    if (_showDebugLogs)
                    {
                        Debug.Log($"[FloorStateManager] Deleted save file at {savePath}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FloorStateManager] Failed to delete save file: {e.Message}");
            }
        }

        #endregion

        #region Debug Utilities

        /// <summary>
        /// Gets statistics about the current session.
        /// </summary>
        /// <returns>Formatted string with session statistics.</returns>
        public string GetSessionStats()
        {
            return $"World Seed: {_worldSeed}\n" +
                   $"Current Floor: {_currentFloorNumber}\n" +
                   $"Floors Visited: {_floorStates.Count}\n" +
                   $"Initialized: {_isInitialized}";
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Represents the complete state of a single floor.
    /// Stores generation seed and all dynamic state changes.
    /// </summary>
    [Serializable]
    public class FloorState
    {
        [Tooltip("Floor number (1-based).")]
        public int floorNumber;

        [Tooltip("Seed used to generate this floor. Allows deterministic regeneration.")]
        public int generationSeed;

        [Tooltip("Has the player visited this floor before?")]
        public bool isVisited;

        [Tooltip("Has the player cleared all objectives on this floor?")]
        public bool isCleared;

        [Tooltip("Items collected on this floor. Key: itemID, Value: collected status.")]
        public Dictionary<string, bool> collectedItems = new Dictionary<string, bool>();

        [Tooltip("List of defeated enemy instance IDs.")]
        public List<string> defeatedEnemies = new List<string>();

        [Tooltip("Doors opened on this floor. Key: doorID, Value: opened status.")]
        public Dictionary<string, bool> openedDoors = new Dictionary<string, bool>();

        [Tooltip("Last known player position on this floor.")]
        public Vector3 lastPlayerPosition;

        [Tooltip("Total time spent on this floor in seconds.")]
        public float timeSpentOnFloor;
    }

    /// <summary>
    /// Wrapper for saving all floor states to JSON.
    /// </summary>
    [Serializable]
    internal class SaveData
    {
        public int worldSeed;
        public int currentFloorNumber;
        public List<FloorState> floorStates;
    }

    #endregion
}