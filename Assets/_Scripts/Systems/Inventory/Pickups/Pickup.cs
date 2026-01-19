using UnityEngine;

namespace _Scripts.Systems.Inventory.Pickups
{
    /// <summary>
    /// Base class for all pickups in the game.
    /// </summary>
    public abstract class Pickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] protected string _pickupId;
        [SerializeField] protected bool _autoPickup = false;
        [SerializeField] protected float _interactionRange = 2f;

        [Header("Visual Feedback")]
        [SerializeField] protected GameObject _highlightEffect;

        protected bool _isCollected = false;

        public string PickupId => _pickupId;
        public bool IsCollected => _isCollected;

        protected virtual void Awake()
        {
            if (string.IsNullOrEmpty(_pickupId))
            {
                _pickupId = System.Guid.NewGuid().ToString();
            }
        }

        protected virtual void Start()
        {
            // Check if already collected this floor session
            CheckIfAlreadyCollected();
        }

        protected virtual void CheckIfAlreadyCollected()
        {
            var floorManager = _Scripts.Core.Managers.FloorStateManager.Instance;
            if (floorManager != null && floorManager.IsInitialized)
            {
                var floorState = floorManager.GetCurrentFloorState();
                if (floorState.collectedItems.ContainsKey(_pickupId) && floorState.collectedItems[_pickupId])
                {
                    _isCollected = true;
                    gameObject.SetActive(false);
                }
            }
        }

        protected virtual void MarkAsCollected()
        {
            _isCollected = true;

            var floorManager = _Scripts.Core.Managers.FloorStateManager.Instance;
            if (floorManager != null && floorManager.IsInitialized)
            {
                var floorState = floorManager.GetCurrentFloorState();
                floorState.collectedItems[_pickupId] = true;
            }
        }

        public virtual void SetHighlight(bool active)
        {
            if (_highlightEffect != null)
            {
                _highlightEffect.SetActive(active);
            }
        }

        public abstract bool TryPickup(PlayerInventory inventory);

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (_isCollected || !_autoPickup) return;

            if (other.CompareTag("Player"))
            {
                var inventory = other.GetComponent<PlayerInventory>();
                if (inventory == null) inventory = PlayerInventory.Instance;

                if (inventory != null && TryPickup(inventory))
                {
                    OnPickupSuccess();
                }
            }
        }

        protected virtual void OnPickupSuccess()
        {
            MarkAsCollected();
            gameObject.SetActive(false);
        }
    }
}
