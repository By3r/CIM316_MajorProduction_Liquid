using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.ProceduralGeneration.ItemSpawning
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

        [Header("Spawn Settings")]
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

        #endregion

        #region Spawning Logic

        /// <summary>
        /// Attempts to spawn an item at this point based on spawn chances.
        /// Called by the room spawner or level generator.
        /// OPTIMIZED: Only does raycast once during this call.
        /// </summary>
        public void SpawnItem()
        {
            if (_spawnableItems == null || _spawnableItems.Count == 0)
            {
                if (_showDebugLogs)
                    Debug.LogWarning($"[ItemSpawnPoint] No spawnable items configured for '{gameObject.name}'");
                return;
            }

            // If guaranteed spawn, keep trying until something spawns
            if (_guaranteedSpawn)
            {
                _spawnedItem = TrySpawnRandomItem();
                
                // If still nothing spawned and we have a fallback, use it
                if (_spawnedItem == null && _useFallbackItem && _fallbackItemPrefab != null)
                {
                    _spawnedItem = SpawnSpecificItem(_fallbackItemPrefab);
                }
            }
            else
            {
                // Normal spawn - respect individual chances
                _spawnedItem = TrySpawnRandomItem();
            }

            if (_spawnedItem != null && _showDebugLogs)
            {
                Debug.Log($"[ItemSpawnPoint] Spawned '{_spawnedItem.name}' at '{gameObject.name}'");
            }
        }

        /// <summary>
        /// Tries to spawn a random item from the list based on spawn chances.
        /// Returns null if no item was selected.
        /// </summary>
        private GameObject TrySpawnRandomItem()
        {
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
                    return SpawnSpecificItem(spawnableItem.itemPrefab);
                }
            }

            return null;
        }

        /// <summary>
        /// Spawns a specific item prefab at this spawn point.
        /// OPTIMIZED: Does ONE raycast to snap to ground, then done.
        /// </summary>
        private GameObject SpawnSpecificItem(GameObject prefab)
        {
            if (prefab == null)
                return null;

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = transform.rotation;

            GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation);
            spawnedObject.name = $"{prefab.name}";

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