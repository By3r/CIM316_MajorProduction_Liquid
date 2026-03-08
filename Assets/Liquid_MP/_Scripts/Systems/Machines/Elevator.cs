using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.Player;
using _Scripts.Systems.ProceduralGeneration;
using _Scripts.Systems.Terminal;

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

        public event Action<int> OnFloorTransitionStarted;
        public event Action<int> OnFloorTransitionComplete;

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField] private PowerCellSlot _powerCellSlot;
        [SerializeField] private Transform _controlPanel;

        [Header("Floor Settings")]
        [SerializeField] private int _totalFloors = 20;

        [Header("Transition Settings")]
        [SerializeField] private float _transitionDelay = 2f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

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

        // Terminal UI integration (both persist in the permanent safe room)
        private SafeRoomTerminalUI _terminalUI;
        private bool _terminalResolved;

        #endregion

        #region Properties

        public bool IsPowered => _powerCellSlot != null && _powerCellSlot.IsPowered;
        public bool IsTransitioning => _isTransitioning;
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
            // Subscribe to power state changes
            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged += HandlePowerStateChanged;
            }

            // Resolve terminal UI — both persist in the permanent safe room
            TryResolveTerminalUI();
        }

        private void OnDestroy()
        {
            if (_terminalUI != null)
            {
                _terminalUI.OnTravelConfirmed -= HandleFloorSelected;
            }

            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        #endregion

        #region Private Methods

        private void TryResolveTerminalUI()
        {
            _terminalUI = SafeRoomTerminalUI.Instance;
            if (_terminalUI == null) return;

            _terminalResolved = true;
            _terminalUI.OnTravelConfirmed += HandleFloorSelected;
        }

        private void HandleFloorSelected(int floor)
        {
            // Prevent double transitions from rapid clicks or duplicate events
            if (_isTransitioning) return;

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

            StartCoroutine(TransitionCoroutine(floor, isNewFloor));
        }

        private IEnumerator TransitionCoroutine(int targetFloor, bool consumesPower)
        {
            _isTransitioning = true;
            OnFloorTransitionStarted?.Invoke(targetFloor);
            _onTransitionStarted?.Invoke();

            // Fade screen to black (runs concurrently with transition delay)
            ScreenFade.Instance.FadeOut(_fadeOutDuration);

            // Play elevator movement sound
            PlaySound(_elevatorMoveSound);

            // Wait for transition (elevator moving simulation)
            // Fade completes during this delay, so the screen is black before floor gen.
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

                // Save player equipment before floor transition
                if (PlayerEquipment.Instance != null)
                {
                    var eqData = PlayerEquipment.Instance.ToSaveData();
                    floorManager.SavePlayerEquipment(eqData);
                }

                // Save player position so they stay put after floor gen
                // (instead of being teleported to room center)
                var playerManager = PlayerManager.Instance;
                if (playerManager != null && playerManager.CurrentPlayer != null)
                {
                    Transform playerTransform = playerManager.CurrentPlayer.transform;
                    floorManager.SavePlayerPosition(
                        playerTransform.position,
                        playerTransform.rotation.eulerAngles);
                }

                // Sync dropped item positions (physics may have moved them since drop)
                SyncDroppedItemPositions(floorManager);

                // Mark current floor as visited before leaving
                floorManager.MarkCurrentFloorAsVisited();

                // Set new floor
                floorManager.CurrentFloorNumber = targetFloor;
            }

            // Consume power cell if going to new floor
            if (consumesPower && _powerCellSlot != null && _powerCellSlot.IsPowered)
            {
                _powerCellSlot.SetPoweredState(false, null);
            }

            // Publish event for level regeneration (FloorGenerator listens to this).
            // The Elevator now persists (permanent safe room), so the coroutine survives.
            // Post-generation work (fade-in, inventory/equipment restore, player positioning)
            // is handled by PlayerManager.HandleFloorGenerationComplete().
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnFloorTransitionRequested", targetFloor);
            }

            // Wait one frame for floor generation to complete
            yield return null;

            _isTransitioning = false;
            OnFloorTransitionComplete?.Invoke(targetFloor);
            _onTransitionComplete?.Invoke();
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
