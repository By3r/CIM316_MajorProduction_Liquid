using _Scripts.Core;
using _Scripts.ProceduralGeneration.Doors;
using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Handles player interaction with interactable objects (doors, items, etc).
    /// Uses raycasting from the player's camera to detect and interact with objects.
    /// Integrates with the new Unity Input System through InputManager.
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Interaction Settings")]
        [Tooltip("Maximum distance the player can interact with objects.")]
        [SerializeField] private float _interactionDistance = 3f;

        [Tooltip("Layer mask for interactable objects. Set to include doors and other interactables.")]
        [SerializeField] private LayerMask _interactionLayerMask = ~0;

        [Tooltip("Should we show debug raycasts in the scene view?")]
        [SerializeField] private bool _showDebugRays = false;

        [Header("UI Feedback")]
        [Tooltip("Enable UI prompt when looking at interactable objects?")]
        [SerializeField] private bool _showInteractionPrompt = true;

        [Tooltip("Text to display when looking at a closed door.")]
        [SerializeField] private string _openDoorPromptText = "Press [E] to Open";

        [Tooltip("Text to display when looking at an open door that can be closed.")]
        [SerializeField] private string _closeDoorPromptText = "Press [E] to Close";

        [Tooltip("Text to display when looking at an open door that cannot be closed.")]
        [SerializeField] private string _cannotClosePromptText = "Cannot Close";

        #endregion

        #region Private Fields

        private Camera _playerCamera;
        private Door _currentDoor;
        private bool _isLookingAtDoor;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the door the player is currently looking at (null if none).
        /// </summary>
        public Door CurrentDoor => _currentDoor;

        /// <summary>
        /// Gets whether the player is currently looking at an interactable door.
        /// </summary>
        public bool IsLookingAtDoor => _isLookingAtDoor;

        /// <summary>
        /// Gets the interaction prompt text based on what the player is looking at.
        /// </summary>
        public string InteractionPromptText
        {
            get
            {
                if (!_isLookingAtDoor || _currentDoor == null)
                    return string.Empty;

                if (_currentDoor.IsOpen)
                {
                    return _currentDoor.AllowManualClose ? _closeDoorPromptText : _cannotClosePromptText;
                }
                else
                {
                    return _openDoorPromptText;
                }
            }
        }

        /// <summary>
        /// Gets whether an interaction prompt should be displayed.
        /// </summary>
        public bool ShouldShowPrompt => _showInteractionPrompt && _isLookingAtDoor;

        #endregion

        #region Initialization

        private void Awake()
        {
            FindPlayerCamera();
        }

        private void Start()
        {
            ValidateConfiguration();
        }

        private void FindPlayerCamera()
        {
            var cameraTransform = transform.Find("PlayerCamera");
            if (cameraTransform != null)
            {
                _playerCamera = cameraTransform.GetComponent<Camera>();
            }

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            if (_playerCamera == null)
            {
                Debug.LogError("[InteractionController] No camera found! Interaction system will not work.");
            }
        }

        private void ValidateConfiguration()
        {
            if (_interactionDistance <= 0f)
            {
                Debug.LogWarning($"[InteractionController] Interaction distance is {_interactionDistance}. Setting to 3f.");
                _interactionDistance = 3f;
            }

            if (_playerCamera == null)
            {
                Debug.LogError("[InteractionController] Player camera not found! Check camera setup.");
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            CheckForInteractables();
            HandleInteractionInput();
        }

        #endregion

        #region Interaction Logic

        /// <summary>
        /// Checks for interactable objects in front of the player using raycasting.
        /// Updates the current door reference and interaction state.
        /// </summary>
        private void CheckForInteractables()
        {
            if (_playerCamera == null)
            {
                _isLookingAtDoor = false;
                _currentDoor = null;
                return;
            }

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            RaycastHit hit;

            if (_showDebugRays)
            {
                Debug.DrawRay(ray.origin, ray.direction * _interactionDistance, Color.yellow);
            }

            if (Physics.Raycast(ray, out hit, _interactionDistance, _interactionLayerMask))
            {
                Door door = hit.collider.GetComponent<Door>();
                
                if (door != null)
                {
                    _isLookingAtDoor = true;
                    _currentDoor = door;

                    if (_showDebugRays)
                    {
                        Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green);
                    }
                    return;
                }

                door = hit.collider.GetComponentInParent<Door>();
                if (door != null)
                {
                    _isLookingAtDoor = true;
                    _currentDoor = door;

                    if (_showDebugRays)
                    {
                        Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green);
                    }
                    return;
                }
            }

            _isLookingAtDoor = false;
            _currentDoor = null;
        }

        /// <summary>
        /// Handles interaction input and triggers door interaction when appropriate.
        /// Uses InputManager to read the interact button state.
        /// </summary>
        private void HandleInteractionInput()
        {
            if (InputManager.Instance == null)
            {
                Debug.LogWarning("[InteractionController] InputManager not found!");
                return;
            }

            if (InputManager.Instance.InteractPressed)
            {
                AttemptInteraction();
            }
        }

        /// <summary>
        /// Attempts to interact with the current door if one is targeted.
        /// </summary>
        private void AttemptInteraction()
        {
            if (!_isLookingAtDoor || _currentDoor == null)
            {
                return;
            }

            bool success = _currentDoor.Interact();

            if (success)
            {
                OnSuccessfulInteraction(_currentDoor);
            }
            else
            {
                OnFailedInteraction(_currentDoor);
            }
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Called when a door interaction succeeds.
        /// Can be used for feedback, sound effects, or event publishing.
        /// </summary>
        private void OnSuccessfulInteraction(Door door)
        {
            string action = door.IsOpen ? "opened" : "closed";
            Debug.Log($"[InteractionController] Successfully {action} door '{door.gameObject.name}'");

            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnPlayerInteractedWithDoor", new DoorInteractionData
                {
                    Door = door,
                    Player = gameObject,
                    WasOpened = door.IsOpen
                });
            }
        }

        /// <summary>
        /// Called when a door interaction fails.
        /// Provides feedback to the player about why interaction failed.
        /// </summary>
        private void OnFailedInteraction(Door door)
        {
            if (door.IsOpen && !door.AllowManualClose)
            {
                Debug.Log("[InteractionController] Cannot close this door - manual closing is disabled.");
            }
            else if (door.IsAnimating)
            {
                Debug.Log("[InteractionController] Door is currently animating.");
            }
            else
            {
                Debug.Log("[InteractionController] Interaction failed for unknown reason.");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Forces an interaction with the current door if one is available.
        /// Useful for external systems or AI to trigger door interactions.
        /// </summary>
        public void ForceInteractWithCurrentDoor()
        {
            if (_currentDoor != null)
            {
                AttemptInteraction();
            }
        }

        /// <summary>
        /// Sets the interaction distance at runtime.
        /// </summary>
        public void SetInteractionDistance(float distance)
        {
            _interactionDistance = Mathf.Max(0.5f, distance);
        }

        /// <summary>
        /// Enables or disables the interaction prompt UI.
        /// </summary>
        public void SetShowInteractionPrompt(bool show)
        {
            _showInteractionPrompt = show;
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (!_showDebugRays || _playerCamera == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_playerCamera.transform.position, _interactionDistance);

            Gizmos.color = _isLookingAtDoor ? Color.green : Color.yellow;
            Gizmos.DrawRay(_playerCamera.transform.position, _playerCamera.transform.forward * _interactionDistance);

            if (_isLookingAtDoor && _currentDoor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_currentDoor.transform.position, 0.2f);
            }
        }

        #endregion
    }

    #region Event Data Classes

    /// <summary>
    /// Data structure for door interaction events.
    /// </summary>
    public class DoorInteractionData
    {
        public Door Door;
        public GameObject Player;
        public bool WasOpened;
    }

    #endregion
}