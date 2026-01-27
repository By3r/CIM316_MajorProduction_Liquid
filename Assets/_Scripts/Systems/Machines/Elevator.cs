using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using _Scripts.Core.Managers;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// Elevator controller for floor transitions.
    /// Player interacts with control panel to open floor selection UI.
    /// Requires PowerCell to travel to new (unvisited) floors.
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        #region Events

        public event Action OnFloorUIOpened;
        public event Action OnFloorUIClosed;
        public event Action<int> OnFloorTransitionStarted;
        public event Action<int> OnFloorTransitionComplete;

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField] private PowerCellSlot _powerCellSlot;
        [SerializeField] private ElevatorFloorUI _floorUI;
        [SerializeField] private Transform _controlPanel;

        [Header("Floor Settings")]
        [SerializeField] private int _totalFloors = 20;

        [Header("Transition Settings")]
        [SerializeField] private float _transitionDelay = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _elevatorMoveSound;
        [SerializeField] private AudioClip _uiOpenSound;

        [Header("Events")]
        [SerializeField] private UnityEvent _onTransitionStarted;
        [SerializeField] private UnityEvent _onTransitionComplete;

        #endregion

        #region Private Fields

        private bool _isTransitioning;
        private bool _isUIOpen;

        #endregion

        #region Properties

        public bool IsPowered => _powerCellSlot != null && _powerCellSlot.IsPowered;
        public bool IsTransitioning => _isTransitioning;
        public bool IsUIOpen => _isUIOpen;
        public Transform ControlPanel => _controlPanel;

        public string ControlPanelPrompt
        {
            get
            {
                if (_isTransitioning)
                    return "Elevator in transit...";

                return "Use Control Panel";
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find Floor UI at runtime if not assigned (since it's in the scene, not the prefab)
            if (_floorUI == null)
            {
                _floorUI = FindObjectOfType<ElevatorFloorUI>();
                if (_floorUI == null)
                {
                    Debug.LogWarning("[Elevator] ElevatorFloorUI not found in scene!");
                }
            }

            // Subscribe to floor selection events
            if (_floorUI != null)
            {
                _floorUI.OnFloorSelected += HandleFloorSelected;

                int currentFloor = GetCurrentFloor();
                int highestUnlocked = GetHighestUnlockedFloor();
                _floorUI.Initialize(_totalFloors, currentFloor, highestUnlocked);
            }

            // Subscribe to power state changes
            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged += HandlePowerStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_floorUI != null)
            {
                _floorUI.OnFloorSelected -= HandleFloorSelected;
            }

            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        private void Update()
        {
            // Close UI on escape
            if (_isUIOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseFloorUI();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens the floor selection UI.
        /// Called when player interacts with control panel.
        /// </summary>
        public void OpenFloorUI()
        {
            if (_isTransitioning || _floorUI == null) return;

            int currentFloor = GetCurrentFloor();
            int highestUnlocked = GetHighestUnlockedFloor();

            _floorUI.Show(currentFloor, highestUnlocked);
            _isUIOpen = true;

            PlaySound(_uiOpenSound);
            OnFloorUIOpened?.Invoke();

            // Disable player input while UI is open
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }
        }

        /// <summary>
        /// Closes the floor selection UI.
        /// </summary>
        public void CloseFloorUI()
        {
            if (_floorUI != null)
            {
                _floorUI.Hide();
            }

            _isUIOpen = false;
            OnFloorUIClosed?.Invoke();

            // Re-enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
            }
        }

        #endregion

        #region Private Methods

        private void HandleFloorSelected(int floor)
        {
            int currentFloor = GetCurrentFloor();
            int highestUnlocked = GetHighestUnlockedFloor();

            // Can't go to current floor
            if (floor == currentFloor)
            {
                Debug.Log("[Elevator] Already on this floor");
                return;
            }

            // Check if floor is accessible
            bool isNewFloor = floor > highestUnlocked;

            if (isNewFloor && !IsPowered)
            {
                Debug.Log("[Elevator] Need PowerCell to travel to new floor");
                return;
            }

            // Start transition
            StartCoroutine(TransitionCoroutine(floor, isNewFloor));
        }

        private IEnumerator TransitionCoroutine(int targetFloor, bool consumesPower)
        {
            _isTransitioning = true;
            OnFloorTransitionStarted?.Invoke(targetFloor);
            _onTransitionStarted?.Invoke();

            // Close UI
            CloseFloorUI();

            // Play elevator movement sound
            PlaySound(_elevatorMoveSound);

            // Wait for transition
            yield return new WaitForSeconds(_transitionDelay);

            // Update floor state
            var floorManager = FloorStateManager.Instance;
            if (floorManager != null)
            {
                // Mark current floor as visited before leaving
                floorManager.MarkCurrentFloorAsVisited();

                // Set new floor
                floorManager.CurrentFloorNumber = targetFloor;

                Debug.Log($"[Elevator] Transitioning to floor {targetFloor}");
            }

            // Consume power cell if going to new floor
            if (consumesPower && _powerCellSlot != null && _powerCellSlot.IsPowered)
            {
                // The power cell is consumed - we don't return it to inventory
                _powerCellSlot.SetPoweredState(false, null);
                Debug.Log("[Elevator] PowerCell consumed for new floor access");
            }

            // Publish event for level regeneration (LevelGenerator listens to this)
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnFloorTransitionRequested", targetFloor);
            }

            OnFloorTransitionComplete?.Invoke(targetFloor);
            _onTransitionComplete?.Invoke();

            _isTransitioning = false;
        }

        private void HandlePowerStateChanged(bool isPowered)
        {
            Debug.Log($"[Elevator] Power state changed: {isPowered}");

            // Refresh UI if open
            if (_isUIOpen && _floorUI != null)
            {
                int currentFloor = GetCurrentFloor();
                int highestUnlocked = GetHighestUnlockedFloor();
                _floorUI.Show(currentFloor, highestUnlocked);
            }
        }

        private int GetCurrentFloor()
        {
            var floorManager = FloorStateManager.Instance;
            return floorManager != null ? floorManager.CurrentFloorNumber : 1;
        }

        private int GetHighestUnlockedFloor()
        {
            var floorManager = FloorStateManager.Instance;
            if (floorManager == null) return 1;

            // Find highest visited floor
            int highest = floorManager.CurrentFloorNumber;

            // Check all floors up to total
            for (int i = 1; i <= _totalFloors; i++)
            {
                if (floorManager.HasVisitedFloor(i) && i > highest)
                {
                    highest = i;
                }
            }

            return highest;
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsPowered ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);

            if (_controlPanel != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_controlPanel.position, 0.3f);
            }
        }

        #endregion
    }
}
