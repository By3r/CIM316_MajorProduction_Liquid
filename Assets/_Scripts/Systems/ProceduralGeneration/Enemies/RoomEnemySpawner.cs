using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Enemies
{
    /// <summary>
    /// Manages all enemy spawn points within a room prefab.
    /// Automatically finds and triggers all EnemySpawnPoints when the room is instantiated.
    /// Should be attached to the root of room prefabs (alongside RoomItemSpawner).
    /// Mirrors the RoomItemSpawner pattern for consistency.
    ///
    /// USAGE IN UNITY:
    /// 1. Select the root GameObject of your room prefab
    /// 2. Add Component → RoomEnemySpawner
    /// 3. That's it — it automatically finds all EnemySpawnPoint children and spawns from them
    /// </summary>
    public class RoomEnemySpawner : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Spawn Settings")]
        [Tooltip("Automatically spawn enemies when the room is instantiated?")]
        [SerializeField] private bool _spawnOnAwake = true;

        [Tooltip("Delay before spawning enemies (useful if waiting for nav grid to rebuild).")]
        [SerializeField] private float _spawnDelay = 0f;

        [Header("Debug")]
        [Tooltip("Show debug logs for spawn operations?")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private List<EnemySpawnPoint> _spawnPoints;
        private List<EnemySpawnPoint> _ventPoints;
        private int _totalEnemiesSpawned;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets all spawn points in this room (static + vents).
        /// </summary>
        public List<EnemySpawnPoint> SpawnPoints => _spawnPoints;

        /// <summary>
        /// Gets only vent spawn points in this room (for runtime/threat spawning).
        /// </summary>
        public List<EnemySpawnPoint> VentPoints => _ventPoints;

        /// <summary>
        /// Gets the total number of enemies that have been spawned in this room.
        /// </summary>
        public int TotalEnemiesSpawned => _totalEnemiesSpawned;

        #endregion

        #region Initialization

        private void Awake()
        {
            FindAllSpawnPoints();

            if (_spawnOnAwake)
            {
                if (_spawnDelay > 0f)
                {
                    Invoke(nameof(SpawnAllEnemies), _spawnDelay);
                }
                else
                {
                    SpawnAllEnemies();
                }
            }
        }

        /// <summary>
        /// Finds all EnemySpawnPoint components in this room (including children).
        /// Separates them into static spawn points and vents.
        /// </summary>
        private void FindAllSpawnPoints()
        {
            EnemySpawnPoint[] allPoints = GetComponentsInChildren<EnemySpawnPoint>();
            _spawnPoints = new List<EnemySpawnPoint>();
            _ventPoints = new List<EnemySpawnPoint>();

            foreach (EnemySpawnPoint point in allPoints)
            {
                if (point.IsVent)
                {
                    _ventPoints.Add(point);
                }
                else
                {
                    _spawnPoints.Add(point);
                }
            }

            if (_showDebugLogs)
                Debug.Log($"[RoomEnemySpawner] Found {_spawnPoints.Count} spawn points and {_ventPoints.Count} vents in room '{gameObject.name}'");
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawns enemies at all static (non-vent) spawn points in this room.
        /// Vents are skipped — they only activate via the threat system.
        /// </summary>
        public void SpawnAllEnemies()
        {
            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.Log($"[RoomEnemySpawner] No static spawn points in room '{gameObject.name}'");
                return;
            }

            _totalEnemiesSpawned = 0;

            foreach (EnemySpawnPoint spawnPoint in _spawnPoints)
            {
                if (spawnPoint == null)
                    continue;

                spawnPoint.SpawnEnemy();

                if (spawnPoint.HasSpawnedEnemy)
                {
                    _totalEnemiesSpawned++;
                }
            }

            if (_showDebugLogs)
                Debug.Log($"[RoomEnemySpawner] Spawned {_totalEnemiesSpawned} enemies in room '{gameObject.name}'");
        }

        /// <summary>
        /// Clears all spawned enemies in this room.
        /// </summary>
        public void ClearAllSpawnedEnemies()
        {
            if (_spawnPoints != null)
            {
                foreach (EnemySpawnPoint spawnPoint in _spawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        spawnPoint.ClearSpawnedEnemy();
                    }
                }
            }

            if (_ventPoints != null)
            {
                foreach (EnemySpawnPoint ventPoint in _ventPoints)
                {
                    if (ventPoint != null)
                    {
                        ventPoint.ClearSpawnedEnemy();
                    }
                }
            }

            _totalEnemiesSpawned = 0;

            if (_showDebugLogs)
                Debug.Log($"[RoomEnemySpawner] Cleared all spawned enemies in room '{gameObject.name}'");
        }

        /// <summary>
        /// Refreshes the list of spawn points (useful if spawn points are added/removed at runtime).
        /// </summary>
        public void RefreshSpawnPoints()
        {
            FindAllSpawnPoints();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets statistics about spawn points in this room.
        /// </summary>
        public string GetSpawnStatistics()
        {
            if (_spawnPoints == null)
                return "No spawn points found.";

            int activeSpawns = 0;
            foreach (EnemySpawnPoint sp in _spawnPoints)
            {
                if (sp != null && sp.HasSpawnedEnemy)
                    activeSpawns++;
            }

            return $"Static Spawns: {_spawnPoints.Count} | Vents: {(_ventPoints != null ? _ventPoints.Count : 0)} | Enemies Active: {activeSpawns}";
        }

        #endregion
    }
}
