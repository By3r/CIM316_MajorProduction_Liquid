using System.Collections.Generic;
using UnityEngine;
using _Scripts.Core.Managers;
using _Scripts.Systems.ProceduralGeneration.Items;

namespace _Scripts.Systems.ProceduralGeneration.Enemies
{
    /// <summary>
    /// Defines a spawn point for enemies within a room prefab.
    /// Each spawn point can have a list of potential enemies with individual spawn chances.
    /// Enemies are spawned during level generation based on probability.
    /// Mirrors the ItemSpawnPoint pattern for consistency.
    ///
    /// USAGE IN UNITY:
    /// 1. Create an empty GameObject as a child of your room prefab
    /// 2. Add this component to it
    /// 3. Position it where you want enemies to appear
    /// 4. Drag enemy prefabs into the Spawnable Enemies list
    /// 5. Set spawn chances (0-100%) for each
    /// 6. The RoomEnemySpawner on the room root will find and trigger these automatically
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        #region Serialized Fields

        [Tooltip("List of enemies that can spawn at this point with their spawn chances.")]
        [SerializeField] private List<SpawnableEnemy> _spawnableEnemies = new List<SpawnableEnemy>();

        [Tooltip("Should an enemy always spawn here, or respect individual spawn chances?")]
        [SerializeField] private bool _guaranteedSpawn = false;

        [Tooltip("If no enemy spawns from the list, should we try a fallback?")]
        [SerializeField] private bool _useFallbackEnemy = false;

        [Tooltip("Fallback enemy to spawn if all others fail (100% chance).")]
        [SerializeField] private GameObject _fallbackEnemyPrefab;

        [Header("Positioning")]
        [Tooltip("Snap enemy to ground below using raycast? (happens once at spawn)")]
        [SerializeField] private bool _snapToGround = true;

        [Tooltip("Max distance to raycast when snapping to ground.")]
        [SerializeField] private float _maxGroundCheckDistance = 10f;

        [Tooltip("Layer mask for ground detection.")]
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        [Tooltip("Additional offset to apply after snapping (e.g., to raise slightly off ground).")]
        [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

        [Header("Spawn Behaviour")]
        [Tooltip("If true, this spawn point is a 'vent' — used for runtime/threat-triggered spawns rather than initial generation.")]
        [SerializeField] private bool _isVent = false;

        [Tooltip("If true, spawned enemy starts inactive and must be awakened (e.g., by noise). Useful for LiquidEnemy near extraction sites.")]
        [SerializeField] private bool _spawnInactive = false;

        [Header("Debug")]
        [Tooltip("Show debug information in console when spawning?")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private GameObject _spawnedEnemy;
        private string _spawnPointId;

        // Static container for all spawned enemies to keep hierarchy clean
        private static Transform _enemiesContainer;

        #endregion

        #region Static Methods

        /// <summary>
        /// Gets or creates the container for spawned enemies.
        /// </summary>
        private static Transform GetEnemiesContainer()
        {
            if (_enemiesContainer == null)
            {
                GameObject container = GameObject.Find("--- ENEMIES ---");
                if (container == null)
                {
                    container = new GameObject("--- ENEMIES ---");
                }
                _enemiesContainer = container.transform;
            }
            return _enemiesContainer;
        }

        /// <summary>
        /// Clears the static container reference (call when reloading scene or transitioning floors).
        /// </summary>
        public static void ClearContainerReference()
        {
            _enemiesContainer = null;
        }

        /// <summary>
        /// Destroys all enemies in the container. Called during floor transitions.
        /// </summary>
        public static void DestroyAllSpawnedEnemies()
        {
            if (_enemiesContainer != null)
            {
                foreach (Transform child in _enemiesContainer)
                {
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the enemy that was spawned at this point (null if none).
        /// </summary>
        public GameObject SpawnedEnemy => _spawnedEnemy;

        /// <summary>
        /// Gets whether an enemy has been spawned at this point.
        /// </summary>
        public bool HasSpawnedEnemy => _spawnedEnemy != null;

        /// <summary>
        /// Gets the list of spawnable enemies at this point.
        /// </summary>
        public List<SpawnableEnemy> SpawnableEnemies => _spawnableEnemies;

        /// <summary>
        /// Gets the unique ID for this spawn point.
        /// </summary>
        public string SpawnPointId => _spawnPointId;

        /// <summary>
        /// Is this a vent (runtime/threat-triggered spawn point) rather than a static generation spawn?
        /// </summary>
        public bool IsVent => _isVent;

        /// <summary>
        /// Should the spawned enemy start inactive (e.g., waiting for noise trigger)?
        /// </summary>
        public bool SpawnInactive => _spawnInactive;

        #endregion

        #region Spawn Point ID Generation

        /// <summary>
        /// Generates a deterministic ID based on the spawn point's hierarchy path.
        /// Same spawn point always has the same ID across regenerations.
        /// </summary>
        private void GenerateSpawnPointId()
        {
            List<string> pathParts = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                pathParts.Insert(0, current.name);

                // Stop at the room root (check for item spawner as room marker, same as ItemSpawnPoint)
                if (current.name.StartsWith("Room_") ||
                    current.GetComponent<RoomItemSpawner>() != null ||
                    current.parent == null)
                {
                    break;
                }

                current = current.parent;
            }

            _spawnPointId = string.Join("/", pathParts);

            if (_showDebugLogs)
            {
                Debug.Log($"[EnemySpawnPoint] Generated ID: {_spawnPointId}");
            }
        }

        #endregion

        #region Spawning Logic

        /// <summary>
        /// Attempts to spawn an enemy at this point based on spawn chances.
        /// Called by RoomEnemySpawner after floor generation.
        /// Vent spawn points are skipped during initial generation (they activate via threat system later).
        /// </summary>
        public void SpawnEnemy()
        {
            // Vents don't spawn during initial generation — they're for runtime/threat spawns
            if (_isVent)
            {
                if (_showDebugLogs)
                    Debug.Log($"[EnemySpawnPoint] '{gameObject.name}' is a vent — skipping initial spawn");
                return;
            }

            SpawnEnemyInternal();
        }

        /// <summary>
        /// Force-spawns an enemy from this vent point. Called by the threat system at runtime.
        /// Works on both vent and non-vent spawn points.
        /// </summary>
        public GameObject SpawnFromVent()
        {
            return SpawnEnemyInternal();
        }

        /// <summary>
        /// Internal spawn logic shared by initial generation and vent spawning.
        /// </summary>
        private GameObject SpawnEnemyInternal()
        {
            if (string.IsNullOrEmpty(_spawnPointId))
            {
                GenerateSpawnPointId();
            }

            if (_spawnableEnemies == null || _spawnableEnemies.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[EnemySpawnPoint] No spawnable enemies configured for '{gameObject.name}'");
                return null;
            }

            // Check for cached result (floor revisit determinism)
            var floorManager = FloorStateManager.Instance;
            FloorState floorState = null;
            string cachedPrefabName = null;
            bool hasCachedResult = false;

            // Use a separate cache key for enemy spawn points to avoid collisions with item spawn points
            string cacheKey = $"enemy_{_spawnPointId}";

            if (floorManager != null && floorManager.IsInitialized)
            {
                floorState = floorManager.GetCurrentFloorState();
                if (floorState.spawnPointResults.TryGetValue(cacheKey, out cachedPrefabName))
                {
                    hasCachedResult = true;
                }
            }

            if (hasCachedResult)
            {
                _spawnedEnemy = SpawnFromCachedResult(cachedPrefabName);
            }
            else
            {
                _spawnedEnemy = SpawnAndCacheResult(floorState, cacheKey);
            }

            if (_spawnedEnemy != null)
            {
                // Auto-find player target for the spawned enemy
                AssignPlayerTarget(_spawnedEnemy);

                if (_showDebugLogs)
                    Debug.Log($"[EnemySpawnPoint] Spawned '{_spawnedEnemy.name}' at '{gameObject.name}' (cached: {hasCachedResult})");
            }

            return _spawnedEnemy;
        }

        /// <summary>
        /// Spawns an enemy based on cached result from a previous floor visit.
        /// </summary>
        private GameObject SpawnFromCachedResult(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                if (_showDebugLogs)
                    Debug.Log($"[EnemySpawnPoint] Cached result: nothing spawned at '{_spawnPointId}'");
                return null;
            }

            GameObject prefabToSpawn = FindPrefabByName(prefabName);
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[EnemySpawnPoint] Could not find cached prefab '{prefabName}' for spawn point '{_spawnPointId}'");
                return null;
            }

            return SpawnSpecificEnemy(prefabToSpawn);
        }

        /// <summary>
        /// Spawns an enemy using random logic and caches the result.
        /// </summary>
        private GameObject SpawnAndCacheResult(FloorState floorState, string cacheKey)
        {
            GameObject spawned = null;
            string spawnedPrefabName = "";

            if (_guaranteedSpawn)
            {
                spawned = TrySpawnRandomEnemy(out spawnedPrefabName);

                if (spawned == null && _useFallbackEnemy && _fallbackEnemyPrefab != null)
                {
                    spawned = SpawnSpecificEnemy(_fallbackEnemyPrefab);
                    spawnedPrefabName = _fallbackEnemyPrefab.name;
                }
            }
            else
            {
                spawned = TrySpawnRandomEnemy(out spawnedPrefabName);
            }

            // Cache the result for floor revisit determinism
            if (floorState != null)
            {
                floorState.spawnPointResults[cacheKey] = spawnedPrefabName;

                if (_showDebugLogs)
                    Debug.Log($"[EnemySpawnPoint] Cached spawn result for '{_spawnPointId}': '{spawnedPrefabName}'");
            }

            return spawned;
        }

        /// <summary>
        /// Finds a prefab by name from the spawnable enemies list.
        /// </summary>
        private GameObject FindPrefabByName(string prefabName)
        {
            foreach (var enemy in _spawnableEnemies)
            {
                if (enemy.enemyPrefab != null && enemy.enemyPrefab.name == prefabName)
                {
                    return enemy.enemyPrefab;
                }
            }

            if (_fallbackEnemyPrefab != null && _fallbackEnemyPrefab.name == prefabName)
            {
                return _fallbackEnemyPrefab;
            }

            return null;
        }

        /// <summary>
        /// Tries to spawn a random enemy from the list based on spawn chances.
        /// </summary>
        private GameObject TrySpawnRandomEnemy(out string spawnedPrefabName)
        {
            spawnedPrefabName = "";

            List<SpawnableEnemy> shuffled = new List<SpawnableEnemy>(_spawnableEnemies);
            ShuffleList(shuffled);

            foreach (SpawnableEnemy spawnableEnemy in shuffled)
            {
                if (spawnableEnemy.enemyPrefab == null)
                    continue;

                float roll = Random.Range(0f, 100f);

                if (roll <= spawnableEnemy.spawnChance)
                {
                    spawnedPrefabName = spawnableEnemy.enemyPrefab.name;
                    return SpawnSpecificEnemy(spawnableEnemy.enemyPrefab);
                }
            }

            return null;
        }

        /// <summary>
        /// Spawns a specific enemy prefab at this spawn point.
        /// </summary>
        private GameObject SpawnSpecificEnemy(GameObject prefab)
        {
            if (prefab == null)
                return null;

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = transform.rotation;

            GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation, GetEnemiesContainer());
            spawnedObject.name = $"{prefab.name}";

            // Snap to ground once
            if (_snapToGround)
            {
                SnapToGroundOnce(spawnedObject);
            }

            // Apply offset
            if (_spawnOffset != Vector3.zero)
            {
                spawnedObject.transform.position += _spawnOffset;
            }

            // If this spawn point is set to spawn inactive, disable the enemy AI until awakened
            if (_spawnInactive)
            {
                EnemyBase enemyBase = spawnedObject.GetComponent<EnemyBase>();
                if (enemyBase != null)
                {
                    enemyBase.enabled = false;
                }
            }

            return spawnedObject;
        }

        /// <summary>
        /// Assigns the player transform to a spawned enemy's playerTarget field.
        /// Finds the player in the scene automatically.
        /// </summary>
        private void AssignPlayerTarget(GameObject enemyObj)
        {
            EnemyBase enemyBase = enemyObj.GetComponent<EnemyBase>();
            if (enemyBase == null)
                return;

            // Try to find player by tag first, then by name
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                player = GameObject.Find("Player");
            }

            if (player != null)
            {
                // Use reflection or a public setter to assign playerTarget
                // since it's a serialized protected field on EnemyBase
                var field = typeof(EnemyBase).GetField("playerTarget",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(enemyBase, player.transform);
                }
                else if (_showDebugLogs)
                {
                    Debug.LogWarning($"[EnemySpawnPoint] Could not find playerTarget field on EnemyBase");
                }
            }
            else if (_showDebugLogs)
            {
                Debug.LogWarning($"[EnemySpawnPoint] Could not find Player in scene to assign to enemy");
            }
        }

        /// <summary>
        /// Snaps an object to the ground via one-time raycast.
        /// </summary>
        private void SnapToGroundOnce(GameObject obj)
        {
            Collider objCollider = obj.GetComponent<Collider>();
            if (objCollider == null)
            {
                // Fallback: raycast from object position
                if (Physics.Raycast(obj.transform.position + Vector3.up * 0.5f, Vector3.down,
                    out RaycastHit hit, _maxGroundCheckDistance, _groundLayerMask))
                {
                    obj.transform.position = hit.point;
                }
                return;
            }

            Vector3 bottomPoint = new Vector3(
                objCollider.bounds.center.x,
                objCollider.bounds.min.y,
                objCollider.bounds.center.z
            );

            if (Physics.Raycast(bottomPoint, Vector3.down, out RaycastHit groundHit,
                _maxGroundCheckDistance, _groundLayerMask))
            {
                float distanceToGround = bottomPoint.y - groundHit.point.y;
                obj.transform.position += Vector3.down * distanceToGround;

                if (_showDebugLogs)
                    Debug.Log($"[EnemySpawnPoint] Snapped '{obj.name}' to ground at {groundHit.point}");
            }
        }

        /// <summary>
        /// Fisher-Yates shuffle.
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually spawn a specific enemy (bypasses random selection).
        /// </summary>
        public GameObject ForceSpawnEnemy(GameObject prefab)
        {
            if (_spawnedEnemy != null)
            {
                Debug.LogWarning($"[EnemySpawnPoint] Enemy already spawned at '{gameObject.name}'. Clearing first.");
                ClearSpawnedEnemy();
            }

            _spawnedEnemy = SpawnSpecificEnemy(prefab);

            if (_spawnedEnemy != null)
            {
                AssignPlayerTarget(_spawnedEnemy);
            }

            return _spawnedEnemy;
        }

        /// <summary>
        /// Clears/destroys the spawned enemy at this point.
        /// </summary>
        public void ClearSpawnedEnemy()
        {
            if (_spawnedEnemy != null)
            {
                if (Application.isPlaying)
                    Destroy(_spawnedEnemy);
                else
                    DestroyImmediate(_spawnedEnemy);

                _spawnedEnemy = null;
            }
        }

        /// <summary>
        /// Adds a new spawnable enemy to this point at runtime.
        /// </summary>
        public void AddSpawnableEnemy(GameObject prefab, float spawnChance)
        {
            _spawnableEnemies.Add(new SpawnableEnemy
            {
                enemyPrefab = prefab,
                spawnChance = Mathf.Clamp(spawnChance, 0f, 100f)
            });
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Red sphere for enemy spawn points, yellow for vents
            Gizmos.color = _isVent ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Small arrow pointing forward (spawn facing direction)
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isVent ? new Color(1f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Defines an enemy that can spawn with its associated spawn chance.
    /// </summary>
    [System.Serializable]
    public class SpawnableEnemy
    {
        [Tooltip("The enemy prefab to spawn. Must have an EnemyBase component.")]
        public GameObject enemyPrefab;

        [Tooltip("Chance this enemy will spawn (0-100%).")]
        [Range(0f, 100f)]
        public float spawnChance = 50f;
    }

    #endregion
}
