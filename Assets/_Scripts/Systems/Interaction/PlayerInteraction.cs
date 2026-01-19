/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate PlayerInteraction system
 */
#if false

using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using _Scripts.Systems.Inventory;

namespace _Scripts.Systems.Interaction
{
    /// <summary>
    /// Handles player interaction with Interactable objects.
    /// Uses raycast or overlap detection to find nearby interactables.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float _detectionRange = 3f;
        [SerializeField] private LayerMask _interactableLayerMask;
        [SerializeField] private Transform _raycastOrigin;
        [SerializeField] private bool _useRaycast = true;

        [Header("Input")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("UI")]
        [SerializeField] private GameObject _interactionPromptUI;
        [SerializeField] private TextMeshProUGUI _promptText;

        private Interactable _currentTarget;
        private PlayerInventory _playerInventory;
        private Camera _playerCamera;

        private void Start()
        {
            _playerInventory = GetComponent<PlayerInventory>();
            if (_playerInventory == null)
            {
                _playerInventory = PlayerInventory.Instance;
            }

            _playerCamera = Camera.main;

            if (_raycastOrigin == null && _playerCamera != null)
            {
                _raycastOrigin = _playerCamera.transform;
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
            FindInteractable();
            UpdateUI();

            // Fallback input
            if (_interactAction == null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryInteract();
            }
        }

        private void FindInteractable()
        {
            Interactable found = null;

            if (_useRaycast && _raycastOrigin != null)
            {
                found = FindByRaycast();
            }
            else
            {
                found = FindByOverlap();
            }

            // Update highlight
            if (_currentTarget != found)
            {
                if (_currentTarget != null)
                {
                    _currentTarget.SetHighlighted(false);
                }

                _currentTarget = found;

                if (_currentTarget != null)
                {
                    _currentTarget.SetHighlighted(true);
                }
            }
        }

        private Interactable FindByRaycast()
        {
            Ray ray = new Ray(_raycastOrigin.position, _raycastOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _detectionRange, _interactableLayerMask))
            {
                Interactable interactable = hit.collider.GetComponent<Interactable>();
                if (interactable == null)
                {
                    interactable = hit.collider.GetComponentInParent<Interactable>();
                }

                if (interactable != null && interactable.CanInteract())
                {
                    return interactable;
                }
            }

            return null;
        }

        private Interactable FindByOverlap()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRange, _interactableLayerMask);

            Interactable nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                Interactable interactable = collider.GetComponent<Interactable>();
                if (interactable == null)
                {
                    interactable = collider.GetComponentInParent<Interactable>();
                }

                if (interactable != null && interactable.CanInteract())
                {
                    float distance = Vector3.Distance(transform.position, interactable.transform.position);
                    if (distance < nearestDistance && distance <= interactable.InteractionRange)
                    {
                        nearestDistance = distance;
                        nearest = interactable;
                    }
                }
            }

            return nearest;
        }

        private void UpdateUI()
        {
            bool showPrompt = _currentTarget != null;

            if (_interactionPromptUI != null)
            {
                _interactionPromptUI.SetActive(showPrompt);
            }

            if (_promptText != null && showPrompt)
            {
                _promptText.text = $"[E] {_currentTarget.InteractionPrompt}";
            }
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            TryInteract();
        }

        private void TryInteract()
        {
            if (_currentTarget != null && _currentTarget.CanInteract())
            {
                _currentTarget.Interact(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            if (_raycastOrigin != null && _useRaycast)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(_raycastOrigin.position, _raycastOrigin.forward * _detectionRange);
            }
        }
    }
}

#endif
