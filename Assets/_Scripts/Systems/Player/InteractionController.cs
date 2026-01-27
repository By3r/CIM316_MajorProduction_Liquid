using _Scripts.Core.Managers;
using _Scripts.Systems.ProceduralGeneration.Doors;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.Machines;
using UnityEngine;

namespace _Scripts.Systems.Player
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
        [SerializeField] private bool _showDebugRays;
        #endregion

        #region Private Fields

        private Camera _playerCamera;
        private Door _currentDoor;
        private Pickup _currentPickup;
        private PowerCellSlot _currentPowerCellSlot;
        private Elevator _currentElevator;
        private bool _isLookingAtDoor;
        private bool _isLookingAtPickup;
        private bool _isLookingAtPowerCellSlot;
        private bool _isLookingAtElevatorPanel;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the door the player is currently looking at (null if none).
        /// </summary>
        public Door CurrentDoor => _currentDoor;

        /// <summary>
        /// Gets the pickup the player is currently looking at (null if none).
        /// </summary>
        public Pickup CurrentPickup => _currentPickup;

        /// <summary>
        /// Gets the PowerCellSlot the player is currently looking at (null if none).
        /// </summary>
        public PowerCellSlot CurrentPowerCellSlot => _currentPowerCellSlot;

        /// <summary>
        /// Gets whether the player is currently looking at an interactable door.
        /// </summary>
        public bool IsLookingAtDoor => _isLookingAtDoor;

        /// <summary>
        /// Gets whether the player is currently looking at a pickup.
        /// </summary>
        public bool IsLookingAtPickup => _isLookingAtPickup;

        /// <summary>
        /// Gets whether the player is currently looking at a PowerCellSlot.
        /// </summary>
        public bool IsLookingAtPowerCellSlot => _isLookingAtPowerCellSlot;

        /// <summary>
        /// Gets the Elevator the player is currently looking at (null if none).
        /// </summary>
        public Elevator CurrentElevator => _currentElevator;

        /// <summary>
        /// Gets whether the player is currently looking at an elevator control panel.
        /// </summary>
        public bool IsLookingAtElevatorPanel => _isLookingAtElevatorPanel;

        /// <summary>
        /// Gets whether the player is looking at any interactable.
        /// </summary>
        public bool IsLookingAtInteractable => _isLookingAtDoor || _isLookingAtPickup || _isLookingAtPowerCellSlot || _isLookingAtElevatorPanel;
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
        /// Updates the current door/pickup reference and interaction state.
        /// </summary>
        private void CheckForInteractables()
        {
            if (_playerCamera == null)
            {
                ClearAllTargets();
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
                // Check for Door
                Door door = hit.collider.GetComponent<Door>();
                if (door == null) door = hit.collider.GetComponentInParent<Door>();

                if (door != null)
                {
                    SetDoorTarget(door);
                    if (_showDebugRays) Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green);
                    return;
                }

                // Check for Pickup
                Pickup pickup = hit.collider.GetComponent<Pickup>();
                if (pickup == null) pickup = hit.collider.GetComponentInParent<Pickup>();

                if (pickup != null && !pickup.IsCollected)
                {
                    SetPickupTarget(pickup);
                    if (_showDebugRays) Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.cyan);
                    return;
                }

                // Check for PowerCellSlot
                PowerCellSlot powerCellSlot = hit.collider.GetComponent<PowerCellSlot>();
                if (powerCellSlot == null) powerCellSlot = hit.collider.GetComponentInParent<PowerCellSlot>();

                if (powerCellSlot != null)
                {
                    SetPowerCellSlotTarget(powerCellSlot);
                    if (_showDebugRays) Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.magenta);
                    return;
                }

                // Check for Elevator control panel
                Elevator elevator = hit.collider.GetComponent<Elevator>();
                if (elevator == null) elevator = hit.collider.GetComponentInParent<Elevator>();

                if (elevator != null)
                {
                    // Check if we hit the control panel specifically or the elevator in general
                    bool isControlPanel = elevator.ControlPanel == null ||
                                          hit.collider.transform == elevator.ControlPanel ||
                                          hit.collider.transform.IsChildOf(elevator.ControlPanel);

                    if (isControlPanel)
                    {
                        SetElevatorTarget(elevator);
                        if (_showDebugRays) Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.blue);
                        return;
                    }
                }
            }

            ClearAllTargets();
        }

        private void SetDoorTarget(Door door)
        {
            _isLookingAtDoor = true;
            _currentDoor = door;
            _isLookingAtPickup = false;
            _currentPickup = null;
            _isLookingAtPowerCellSlot = false;
            _currentPowerCellSlot = null;
            _isLookingAtElevatorPanel = false;
            _currentElevator = null;
        }

        private void SetPickupTarget(Pickup pickup)
        {
            _isLookingAtPickup = true;
            _currentPickup = pickup;
            _isLookingAtDoor = false;
            _currentDoor = null;
            _isLookingAtPowerCellSlot = false;
            _currentPowerCellSlot = null;
            _isLookingAtElevatorPanel = false;
            _currentElevator = null;
        }

        private void SetPowerCellSlotTarget(PowerCellSlot slot)
        {
            _isLookingAtPowerCellSlot = true;
            _currentPowerCellSlot = slot;
            _isLookingAtDoor = false;
            _currentDoor = null;
            _isLookingAtPickup = false;
            _currentPickup = null;
            _isLookingAtElevatorPanel = false;
            _currentElevator = null;
        }

        private void SetElevatorTarget(Elevator elevator)
        {
            _isLookingAtElevatorPanel = true;
            _currentElevator = elevator;
            _isLookingAtDoor = false;
            _currentDoor = null;
            _isLookingAtPickup = false;
            _currentPickup = null;
            _isLookingAtPowerCellSlot = false;
            _currentPowerCellSlot = null;
        }

        private void ClearAllTargets()
        {
            _isLookingAtDoor = false;
            _currentDoor = null;
            _isLookingAtPickup = false;
            _currentPickup = null;
            _isLookingAtPowerCellSlot = false;
            _currentPowerCellSlot = null;
            _isLookingAtElevatorPanel = false;
            _currentElevator = null;
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
        /// Attempts to interact with the current target (door or pickup).
        /// </summary>
        private void AttemptInteraction()
        {
            // Try door interaction
            if (_isLookingAtDoor && _currentDoor != null)
            {
                bool success = _currentDoor.Interact();

                if (success)
                {
                    OnSuccessfulInteraction(_currentDoor);
                }
                else
                {
                    OnFailedInteraction(_currentDoor);
                }
                return;
            }

            // Try pickup interaction
            if (_isLookingAtPickup && _currentPickup != null)
            {
                var inventory = PlayerInventory.Instance;
                if (inventory != null && _currentPickup.TryPickup(inventory))
                {
                    OnSuccessfulPickup(_currentPickup);
                }
                else
                {
                    OnFailedPickup(_currentPickup);
                }
                return;
            }

            // Try PowerCellSlot interaction
            if (_isLookingAtPowerCellSlot && _currentPowerCellSlot != null)
            {
                var inventory = PlayerInventory.Instance;
                if (inventory != null)
                {
                    bool success = _currentPowerCellSlot.TogglePowerCell(inventory);
                    if (success)
                    {
                        OnSuccessfulPowerCellSlotInteraction(_currentPowerCellSlot);
                    }
                    else
                    {
                        OnFailedPowerCellSlotInteraction(_currentPowerCellSlot);
                    }
                }
                return;
            }

            // Try Elevator panel interaction
            if (_isLookingAtElevatorPanel && _currentElevator != null)
            {
                if (!_currentElevator.IsTransitioning && !_currentElevator.IsUIOpen)
                {
                    _currentElevator.OpenFloorUI();
                }
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
            // Door interaction failed - could show UI feedback here if needed
        }

        /// <summary>
        /// Called when a pickup is successfully collected.
        /// </summary>
        private void OnSuccessfulPickup(Pickup pickup)
        {
            _currentPickup = null;
            _isLookingAtPickup = false;
        }

        /// <summary>
        /// Called when a pickup cannot be collected (inventory full, etc).
        /// </summary>
        private void OnFailedPickup(Pickup pickup)
        {
            // Pickup failed - could show UI feedback here if needed
        }

        /// <summary>
        /// Called when a PowerCellSlot interaction succeeds.
        /// </summary>
        private void OnSuccessfulPowerCellSlotInteraction(PowerCellSlot slot)
        {
            // PowerCell slot interaction succeeded - could trigger UI feedback here
        }

        /// <summary>
        /// Called when a PowerCellSlot interaction fails.
        /// </summary>
        private void OnFailedPowerCellSlotInteraction(PowerCellSlot slot)
        {
            // PowerCell slot interaction failed - could show UI feedback here if needed
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