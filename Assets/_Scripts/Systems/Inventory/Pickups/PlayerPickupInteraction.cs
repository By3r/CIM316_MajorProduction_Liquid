using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Systems.Inventory.Pickups
{
    /// <summary>
    /// Handles player interaction with pickups.
    /// Detects nearby pickups and allows interaction with E key.
    /// </summary>
    public class PlayerPickupInteraction : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _interactionRange = 2.5f;
        [SerializeField] private LayerMask _pickupLayerMask;
        [SerializeField] private Transform _interactionOrigin;

        [Header("Input")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("UI")]
        [SerializeField] private GameObject _interactionPrompt;

        private Pickup _currentTarget;
        private PlayerInventory _playerInventory;

        private void Start()
        {
            _playerInventory = GetComponent<PlayerInventory>();
            if (_playerInventory == null)
            {
                _playerInventory = PlayerInventory.Instance;
            }

            if (_interactionOrigin == null)
            {
                _interactionOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (_interactAction != null && _interactAction.action != null)
            {
                _interactAction.action.Enable();
                _interactAction.action.performed += OnInteract;
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null && _interactAction.action != null)
            {
                _interactAction.action.performed -= OnInteract;
            }
        }

        private void Update()
        {
            FindNearestPickup();
            UpdateUI();

            // Fallback input if InputActionReference not set
            if (_interactAction == null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryInteract();
            }
        }

        private void FindNearestPickup()
        {
            Pickup nearest = null;
            float nearestDistance = _interactionRange;

            Collider[] colliders = Physics.OverlapSphere(_interactionOrigin.position, _interactionRange, _pickupLayerMask);

            foreach (var collider in colliders)
            {
                Pickup pickup = collider.GetComponent<Pickup>();
                if (pickup == null) pickup = collider.GetComponentInParent<Pickup>();

                if (pickup != null && !pickup.IsCollected)
                {
                    float distance = Vector3.Distance(_interactionOrigin.position, pickup.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = pickup;
                    }
                }
            }

            // Update highlight
            if (_currentTarget != nearest)
            {
                if (_currentTarget != null)
                {
                    _currentTarget.SetHighlight(false);
                }

                _currentTarget = nearest;

                if (_currentTarget != null)
                {
                    _currentTarget.SetHighlight(true);
                }
            }
        }

        private void UpdateUI()
        {
            if (_interactionPrompt != null)
            {
                _interactionPrompt.SetActive(_currentTarget != null);
            }
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            TryInteract();
        }

        private void TryInteract()
        {
            if (_currentTarget == null || _playerInventory == null) return;

            if (_currentTarget.TryPickup(_playerInventory))
            {
                _currentTarget = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = _interactionOrigin != null ? _interactionOrigin : transform;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin.position, _interactionRange);
        }
    }
}
