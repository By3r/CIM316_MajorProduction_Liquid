using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Items
{
    /// <summary>
    /// Manages all item spawn points within a room prefab.
    /// Automatically finds and triggers all ItemSpawnPoints when the room is instantiated.
    /// Should be attached to the root of room prefabs.
    /// </summary>
    public class RoomItemSpawner : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Spawn Settings")]
        [Tooltip("Automatically spawn items when the room is instantiated?")]
        [SerializeField] private bool _spawnOnAwake = true;

        [Tooltip("Delay before spawning items (useful if waiting for physics to settle).")]
        [SerializeField] private float _spawnDelay = 0f;

        [Header("Debug")]
        [Tooltip("Show debug logs for spawn operations?")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private List<ItemSpawnPoint> _spawnPoints;
        private int _totalItemsSpawned;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets all spawn points in this room.
        /// </summary>
        public List<ItemSpawnPoint> SpawnPoints => _spawnPoints;

        /// <summary>
        /// Gets the total number of items that have been spawned in this room.
        /// </summary>
        public int TotalItemsSpawned => _totalItemsSpawned;

        #endregion

        #region Initialization

        private void Awake()
        {
            FindAllSpawnPoints();

            if (_spawnOnAwake)
            {
                if (_spawnDelay > 0f)
                {
                    Invoke(nameof(SpawnAllItems), _spawnDelay);
                }
                else
                {
                    SpawnAllItems();
                }
            }
        }

        /// <summary>
        /// Finds all ItemSpawnPoint components in this room (including children).
        /// </summary>
        private void FindAllSpawnPoints()
        {
            _spawnPoints = new List<ItemSpawnPoint>(GetComponentsInChildren<ItemSpawnPoint>());

            if (_showDebugLogs)
                Debug.Log($"[RoomItemSpawner] Found {_spawnPoints.Count} spawn points in room '{gameObject.name}'");
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawns items at all spawn points in this room.
        /// </summary>
        public void SpawnAllItems()
        {
            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[RoomItemSpawner] No spawn points found in room '{gameObject.name}'");
                return;
            }

            _totalItemsSpawned = 0;

            foreach (ItemSpawnPoint spawnPoint in _spawnPoints)
            {
                if (spawnPoint == null)
                    continue;

                spawnPoint.SpawnItem();

                if (spawnPoint.HasSpawnedItem)
                {
                    _totalItemsSpawned++;
                }
            }

            if (_showDebugLogs)
                Debug.Log($"[RoomItemSpawner] Spawned {_totalItemsSpawned} items in room '{gameObject.name}'");
        }

        /// <summary>
        /// Clears all spawned items in this room.
        /// </summary>
        public void ClearAllSpawnedItems()
        {
            if (_spawnPoints == null)
                return;

            foreach (ItemSpawnPoint spawnPoint in _spawnPoints)
            {
                if (spawnPoint != null)
                {
                    spawnPoint.ClearSpawnedItem();
                }
            }

            _totalItemsSpawned = 0;

            if (_showDebugLogs)
                Debug.Log($"[RoomItemSpawner] Cleared all spawned items in room '{gameObject.name}'");
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
            foreach (ItemSpawnPoint sp in _spawnPoints)
            {
                if (sp != null && sp.HasSpawnedItem)
                    activeSpawns++;
            }

            return $"Spawn Points: {_spawnPoints.Count} | Items Spawned: {activeSpawns}";
        }

        #endregion
    }
}