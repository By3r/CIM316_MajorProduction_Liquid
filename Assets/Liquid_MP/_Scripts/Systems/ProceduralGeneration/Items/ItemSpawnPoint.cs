using System.Collections.Generic;
using UnityEngine;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory.Pickups;

namespace _Scripts.Systems.ProceduralGeneration.Items
{
    /// <summary>
    /// Defines a spawn point for items within a room prefab.
    /// Each spawn point can have a list of potential items with individual spawn chances.
    /// Items are spawned during level generation based on probability.
    /// OPTIMIZED: Raycasting happens ONCE at spawn time only, no continuous updates.
    /// </summary>
    public class ItemSpawnPoint : MonoBehaviour
    {
        #region Serialized Fields

        [Tooltip("List of items that can spawn at this point with their spawn chances.")]
        [SerializeField] private List<SpawnableItem> _spawnableItems = new List<SpawnableItem>();

        [Tooltip("Should an item always spawn here, or respect individual spawn chances?")]
        [SerializeField] private bool _guaranteedSpawn = false;

        [Tooltip("If no item spawns, should we try again with a fallback item?")]
        [SerializeField] private bool _useFallbackItem = false;

        [Tooltip("Fallback item to spawn if all others fail (100% chance).")]
        [SerializeField] private GameObject _fallbackItemPrefab;

        [Header("Positioning Settings")]
        [Tooltip("Snap item to ground below using raycast? (happens once at spawn)")]
        [SerializeField] private bool _snapToGround = true;

        [Tooltip("Max distance to raycast when snapping to ground.")]
        [SerializeField] private float _maxGroundCheckDistance = 10f;

        [Tooltip("Layer mask for ground detection.")]
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        [Tooltip("Additional offset to apply after snapping (e.g., to raise slightly off ground).")]
        [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

        [Header("Debug")]
        [Tooltip("Show debug information in console when spawning?")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private GameObject _spawnedItem;
        private string _spawnPointId;

        // Static container for all spawned pickups to keep hierarchy clean
        private static Transform _pickupsContainer;

        #endregion

        #region Static Methods

        /// <summary>
        /// Gets or creates the container for spawned pickups.
        /// </summary>
        private static Transform GetPickupsContainer()
        {
            if (_pickupsContainer == null)
            {
                GameObject container = GameObject.Find("--- PICKUPS ---");
                if (container == null)
                {
                    container = new GameObject("--- PICKUPS ---");
                }
                _pickupsContainer = container.transform;
            }
            return _pickupsContainer;
        }

        /// <summary>
        /// Clears the static container reference (call when reloading scene).
        /// </summary>
        public static void ClearContainerReference()
        {
            _pickupsContainer = null;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the item that was spawned at this point (null if none spawned).
        /// </summary>
        public GameObject SpawnedItem => _spawnedItem;

        /// <summary>
        /// Gets whether an item has been spawned at this point.
        /// </summary>
        public bool HasSpawnedItem => _spawnedItem != null;

        /// <summary>
        /// Gets the list of spawnable items at this point.
        /// </summary>
        public List<SpawnableItem> SpawnableItems => _spawnableItems;

        /// <summary>
        /// Gets the unique ID for this spawn point (generated from hierarchy path).
        /// </summary>
        public string SpawnPointId => _spawnPointId;

        #endregion

        #region Spawn Point ID Generation

        /// <summary>
        /// Generates a deterministic ID based on the spawn point's hierarchy path.
        /// This ensures the same spawn point always has the same ID across regenerations.
        /// </summary>
        private void GenerateSpawnPointId()
        {
            // Build path from this object up to the room root
            // Format: "RoomPrefabName/ChildPath/SpawnPointName"
            List<string> pathParts = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                pathParts.Insert(0, current.name);

                // Stop at the room root (look for common room markers)
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
                Debug.Log($"[ItemSpawnPoint] Generated ID: {_spawnPointId}");
            }
        }

        #endregion

        #region Spawning Logic

        /// <summary>
        /// Attempts to spawn an item at this point based on spawn chances.
        /// Called by the room spawner or level generator.
        /// OPTIMIZED: Only does raycast once during this call.
        /// On revisited floors, spawns the same item that was spawned before (deterministic).
        /// </summary>
        public void SpawnItem()
        {
            // Generate ID if not already done
            if (string.IsNullOrEmpty(_spawnPointId))
            {
                GenerateSpawnPointId();
            }

            if (_spawnableItems == null || _spawnableItems.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[ItemSpawnPoint] No spawnable items configured for '{gameObject.name}'");
                return;
            }

            // Check if we have a cached result for this spawn point
            var floorManager = FloorStateManager.Instance;
            FloorState floorState = null;
            string cachedPrefabName = null;
            bool hasCachedResult = false;

            if (floorManager != null && floorManager.IsInitialized)
            {
                floorState = floorManager.GetCurrentFloorState();
                if (floorState.spawnPointResults.TryGetValue(_spawnPointId, out cachedPrefabName))
                {
                    hasCachedResult = true;
                }
            }

            if (hasCachedResult)
            {
                // Revisit: spawn the exact same item (or nothing if empty)
                _spawnedItem = SpawnFromCachedResult(cachedPrefabName, floorState);
            }
            else
            {
                // First visit: roll normally and cache the result
                _spawnedItem = SpawnAndCacheResult(floorState);
            }

            if (_spawnedItem != null && _showDebugLogs)
            {
                Debug.Log($"[ItemSpawnPoint] Spawned '{_spawnedItem.name}' at '{gameObject.name}' (cached: {hasCachedResult})");
            }
        }

        /// <summary>
        /// Spawns an item based on cached result from a previous visit.
        /// </summary>
        private GameObject SpawnFromCachedResult(string prefabName, FloorState floorState)
        {
            // Empty string means nothing spawned originally
            if (string.IsNullOrEmpty(prefabName))
            {
                if (_showDebugLogs)
                    Debug.Log($"[ItemSpawnPoint] Cached result: nothing spawned at '{_spawnPointId}'");
                return null;
            }

            // Check if this item was already collected
            string pickupId = GetPickupIdForSpawnPoint();
            if (floorState != null && floorState.collectedItems.TryGetValue(pickupId, out bool collected) && collected)
            {
                if (_showDebugLogs)
                    Debug.Log($"[ItemSpawnPoint] Item at '{_spawnPointId}' was already collected, not spawning");
                return null;
            }

            // Find the prefab by name
            GameObject prefabToSpawn = FindPrefabByName(prefabName);
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[ItemSpawnPoint] Could not find cached prefab '{prefabName}' for spawn point '{_spawnPointId}'");
                return null;
            }

            return SpawnSpecificItem(prefabToSpawn, pickupId);
        }

        /// <summary>
        /// Spawns an item using normal random logic and caches the result.
        /// </summary>
        private GameObject SpawnAndCacheResult(FloorState floorState)
        {
            GameObject spawned = null;
            string spawnedPrefabName = "";

            // If guaranteed spawn, keep trying until something spawns
            if (_guaranteedSpawn)
            {
                spawned = TrySpawnRandomItem(out spawnedPrefabName);

                // If still nothing spawned and we have a fallback, use it
                if (spawned == null && _useFallbackItem && _fallbackItemPrefab != null)
                {
                    string pickupId = GetPickupIdForSpawnPoint();
                    spawned = SpawnSpecificItem(_fallbackItemPrefab, pickupId);
                    spawnedPrefabName = _fallbackItemPrefab.name;
                }
            }
            else
            {
                // Normal spawn - respect individual chances
                spawned = TrySpawnRandomItem(out spawnedPrefabName);
            }

            // Cache the result (even if nothing spawned)
            if (floorState != null)
            {
                floorState.spawnPointResults[_spawnPointId] = spawnedPrefabName;

                if (_showDebugLogs)
                    Debug.Log($"[ItemSpawnPoint] Cached spawn result for '{_spawnPointId}': '{spawnedPrefabName}'");
            }

            return spawned;
        }

        /// <summary>
        /// Finds a prefab by name from the spawnable items list.
        /// </summary>
        private GameObject FindPrefabByName(string prefabName)
        {
            foreach (var item in _spawnableItems)
            {
                if (item.itemPrefab != null && item.itemPrefab.name == prefabName)
                {
                    return item.itemPrefab;
                }
            }

            // Also check fallback
            if (_fallbackItemPrefab != null && _fallbackItemPrefab.name == prefabName)
            {
                return _fallbackItemPrefab;
            }

            return null;
        }

        /// <summary>
        /// Gets the pickup ID that will be assigned to items spawned at this point.
        /// </summary>
        private string GetPickupIdForSpawnPoint()
        {
            return $"{_spawnPointId}_pickup";
        }

        /// <summary>
        /// Tries to spawn a random item from the list based on spawn chances.
        /// Returns null if no item was selected.
        /// </summary>
        private GameObject TrySpawnRandomItem(out string spawnedPrefabName)
        {
            spawnedPrefabName = "";

            // Shuffle the list to add randomness
            List<SpawnableItem> shuffledItems = new List<SpawnableItem>(_spawnableItems);
            ShuffleList(shuffledItems);

            // Try each item until one succeeds
            foreach (SpawnableItem spawnableItem in shuffledItems)
            {
                if (spawnableItem.itemPrefab == null)
                    continue;

                float roll = Random.Range(0f, 100f);

                if (roll <= spawnableItem.spawnChance)
                {
                    string pickupId = GetPickupIdForSpawnPoint();
                    spawnedPrefabName = spawnableItem.itemPrefab.name;
                    return SpawnSpecificItem(spawnableItem.itemPrefab, pickupId);
                }
            }

            return null;
        }

        /// <summary>
        /// Spawns a specific item prefab at this spawn point.
        /// OPTIMIZED: Does ONE raycast to snap to ground, then done.
        /// </summary>
        private GameObject SpawnSpecificItem(GameObject prefab, string pickupId = null)
        {
            if (prefab == null)
                return null;

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = transform.rotation;

            GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation, GetPickupsContainer());
            spawnedObject.name = $"{prefab.name}";

            // Assign pickup ID for persistence tracking
            if (!string.IsNullOrEmpty(pickupId))
            {
                Pickup pickup = spawnedObject.GetComponent<Pickup>();
                if (pickup != null)
                {
                    pickup.SetPickupId(pickupId);
                }
            }

            // SNAP TO GROUND ONCE - no continuous updates
            if (_snapToGround)
            {
                SnapItemToGroundOnce(spawnedObject);
            }

            // Apply final offset
            if (_spawnOffset != Vector3.zero)
            {
                spawnedObject.transform.position += _spawnOffset;
            }

            return spawnedObject;
        }

        /// <summary>
        /// Snaps an item to the ground by raycasting from the bottom of its collider.
        /// OPTIMIZED: Called ONCE at spawn, never again.
        /// </summary>
        private void SnapItemToGroundOnce(GameObject item)
        {
            Collider itemCollider = item.GetComponent<Collider>();

            if (itemCollider == null)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[ItemSpawnPoint] Item '{item.name}' has no collider for ground snapping!");
                return;
            }

            // Get the bottom point of the collider
            Vector3 bottomPoint = GetColliderBottomPoint(itemCollider);

            // ONE raycast down from the bottom point
            RaycastHit hit;
            if (Physics.Raycast(bottomPoint, Vector3.down, out hit, _maxGroundCheckDistance, _groundLayerMask))
            {
                // Calculate offset needed to place bottom on ground
                float distanceToGround = bottomPoint.y - hit.point.y;
                item.transform.position += Vector3.down * distanceToGround;

                if (_showDebugLogs)
                    Debug.Log($"[ItemSpawnPoint] Snapped '{item.name}' to ground at {hit.point}");
            }
            else if (_showDebugLogs)
            {
                Debug.LogWarning($"[ItemSpawnPoint] Could not find ground below '{item.name}' within {_maxGroundCheckDistance}m");
            }
        }

        /// <summary>
        /// Gets the bottom-most point of a collider in world space.
        /// </summary>
        private Vector3 GetColliderBottomPoint(Collider collider)
        {
            Bounds bounds = collider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        /// <summary>
        /// Fisher-Yates shuffle algorithm for randomizing list order.
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
        /// Manually spawn a specific item at this point (bypasses random selection).
        /// Useful for testing or forced spawns.
        /// </summary>
        public GameObject ForceSpawnItem(GameObject prefab)
        {
            if (_spawnedItem != null)
            {
                Debug.LogWarning($"[ItemSpawnPoint] Item already spawned at '{gameObject.name}'. Clearing first.");
                ClearSpawnedItem();
            }

            _spawnedItem = SpawnSpecificItem(prefab);
            return _spawnedItem;
        }

        /// <summary>
        /// Clears any spawned item at this point.
        /// </summary>
        public void ClearSpawnedItem()
        {
            if (_spawnedItem != null)
            {
                if (Application.isPlaying)
                    Destroy(_spawnedItem);
                else
                    DestroyImmediate(_spawnedItem);

                _spawnedItem = null;
            }
        }

        /// <summary>
        /// Adds a new spawnable item to this spawn point.
        /// </summary>
        public void AddSpawnableItem(GameObject prefab, float spawnChance)
        {
            _spawnableItems.Add(new SpawnableItem
            {
                itemPrefab = prefab,
                spawnChance = Mathf.Clamp(spawnChance, 0f, 100f)
            });
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Defines an item that can spawn with its associated spawn chance.
    /// </summary>
    [System.Serializable]
    public class SpawnableItem
    {
        [Tooltip("The item prefab to spawn.")]
        public GameObject itemPrefab;

        [Tooltip("Chance this item will spawn (0-100%).")]
        [Range(0f, 100f)]
        public float spawnChance = 50f;
    }

    #endregion
}
