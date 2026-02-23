using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.ProceduralGeneration;

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
            // Use singleton if not assigned directly (prefab won't have scene reference)
            if (_floorUI == null)
            {
                _floorUI = ElevatorFloorUI.Instance;
                if (_floorUI == null)
                {
                    Debug.LogWarning("[Elevator] ElevatorFloorUI not found in scene!");
                }
            }

            // Subscribe to floor selection events
            if (_floorUI != null)
            {
                _floorUI.OnFloorSelected += HandleFloorSelected;
                _floorUI.OnUIClosed += HandleUIClosedByKeyboard;

                int currentFloor = GetCurrentFloor();
                int highestUnlocked = GetHighestUnlockedFloor();
                _floorUI.Initialize(_totalFloors, currentFloor, highestUnlocked);
            }

            // Subscribe to power state changes
            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged += HandlePowerStateChanged;
            }

            // Restore PowerCellSlot state from previous floor transition
            // (this Elevator is freshly spawned — the old one was destroyed during floor gen)
            var fm = FloorStateManager.Instance;
            if (fm != null && fm.PowerCellSlotWasPowered && _powerCellSlot != null)
            {
                InventoryItemData pcData = null;
                if (!string.IsNullOrEmpty(fm.PowerCellSlotItemId))
                {
                    pcData = ItemDatabase.FindByItemId(fm.PowerCellSlotItemId);
                }
                _powerCellSlot.SetPoweredState(true, pcData);
            }
        }

        private void OnDestroy()
        {
            if (_floorUI != null)
            {
                _floorUI.OnFloorSelected -= HandleFloorSelected;
                _floorUI.OnUIClosed -= HandleUIClosedByKeyboard;
            }

            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        /// <summary>
        /// Called when the UI is closed via keyboard (TAB, E, or Escape).
        /// </summary>
        private void HandleUIClosedByKeyboard()
        {
            _isUIOpen = false;
            OnFloorUIClosed?.Invoke();

            // Re-enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
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
            if (_isTransitioning || _floorUI == null)
            {
                return;
            }

            int currentFloor = GetCurrentFloor();
            int highestUnlocked = GetHighestUnlockedFloor();

            _floorUI.Show(currentFloor, highestUnlocked, IsPowered);
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
                return;
            }

            // Check if floor is accessible
            bool isNewFloor = floor > highestUnlocked;

            if (isNewFloor && !IsPowered)
            {
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

            // Wait for transition (elevator moving simulation)
            yield return new WaitForSeconds(_transitionDelay);

            // Update floor state
            var floorManager = FloorStateManager.Instance;
            if (floorManager != null)
            {
                // Save the working generation seed before leaving this floor
                // This ensures we can regenerate the exact same layout on return
                var floorGenerator = FindObjectOfType<FloorGenerator>();
                if (floorGenerator != null)
                {
                    floorManager.SaveCurrentFloorGenerationSeed(floorGenerator.CurrentSeed);
                }

                // Save player inventory before floor transition
                if (PlayerInventory.Instance != null)
                {
                    var invData = PlayerInventory.Instance.ToSaveData();
                    floorManager.SavePlayerInventory(invData);
                }

                // Save PowerCellSlot state before transition
                // (so we can restore it after the Elevator prefab gets rebuilt)
                if (_powerCellSlot != null)
                {
                    floorManager.SavePowerCellSlotState(
                        _powerCellSlot.IsPowered,
                        _powerCellSlot.IsPowered ? "powercell" : "");
                }

                // Sync dropped item positions (physics may have moved them since drop)
                SyncDroppedItemPositions(floorManager);

                // Mark current floor as visited before leaving
                floorManager.MarkCurrentFloorAsVisited();

                // Set new floor
                floorManager.CurrentFloorNumber = targetFloor;
            }

            // Consume power cell if going to new floor — clear saved state too
            if (consumesPower && _powerCellSlot != null && _powerCellSlot.IsPowered)
            {
                _powerCellSlot.SetPoweredState(false, null);
                if (floorManager != null)
                {
                    floorManager.SavePowerCellSlotState(false, "");
                }
            }

            // Publish event for level regeneration (LevelGenerator listens to this)
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnFloorTransitionRequested", targetFloor);
            }

            // Wait a frame for floor generation to complete
            yield return null;

            // Restore player inventory after floor generation
            if (floorManager != null && PlayerInventory.Instance != null)
            {
                InventorySaveData savedInventory = floorManager.GetSavedInventory();
                PlayerInventory.Instance.RestoreFromSaveData(savedInventory);
            }

            // NOTE: PowerCellSlot restore happens in the NEW Elevator's Start(),
            // since this Elevator instance gets destroyed during floor generation.

            OnFloorTransitionComplete?.Invoke(targetFloor);
            _onTransitionComplete?.Invoke();

            _isTransitioning = false;
        }

        /// <summary>
        /// Updates the saved positions of all dropped items before leaving a floor.
        /// Items may have moved due to physics after being dropped.
        /// </summary>
        private void SyncDroppedItemPositions(FloorStateManager floorManager)
        {
            GameObject pickupsContainer = GameObject.Find("--- PICKUPS ---");
            if (pickupsContainer == null) return;

            FloorState currentFloorState = floorManager.GetCurrentFloorState();

            // Scan all pickups in the container and sync positions to the correct list
            Pickup[] pickups = pickupsContainer.GetComponentsInChildren<Pickup>();
            foreach (Pickup pickup in pickups)
            {
                if (pickup == null || string.IsNullOrEmpty(pickup.PickupId)) continue;
                if (!pickup.PickupId.StartsWith("dropped_")) continue;

                Vector3 pos = pickup.transform.position;
                Vector3 rot = pickup.transform.rotation.eulerAngles;

                // Determine which list this pickup belongs to based on its position
                bool isSafeRoom = FloorStateManager.IsPositionInSafeRoom(pos);
                List<DroppedItemData> droppedItems = isSafeRoom
                    ? floorManager.SafeRoomDroppedItems
                    : currentFloorState.droppedItems;

                if (droppedItems == null) continue;

                // Find matching dropped item data and update position
                for (int i = 0; i < droppedItems.Count; i++)
                {
                    if (droppedItems[i].droppedItemId == pickup.PickupId)
                    {
                        droppedItems[i].posX = pos.x;
                        droppedItems[i].posY = pos.y;
                        droppedItems[i].posZ = pos.z;
                        droppedItems[i].rotX = rot.x;
                        droppedItems[i].rotY = rot.y;
                        droppedItems[i].rotZ = rot.z;
                        break;
                    }
                }
            }
        }

        private void HandlePowerStateChanged(bool isPowered)
        {
            // Refresh UI if open
            if (_isUIOpen && _floorUI != null)
            {
                _floorUI.SetPoweredState(isPowered);
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
