using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using _Scripts.Core.Managers;
using _Scripts.Systems.HUD;
using _Scripts.Systems.Inventory.ItemTypes;
using _Scripts.Systems.Inventory.Pickups;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Main inventory UI controller.
    /// Opens with TAB key — tells the VisorController to show/hide the visor panel.
    /// Inventory slots and context menu live on the visor's World Space Canvas.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        #region Singleton

        private static InventoryUI _instance;
        public static InventoryUI Instance => _instance;

        #endregion

        #region Serialized Fields

        [Header("Visor")]
        [Tooltip("The VisorController that owns the 3D visor HUD. " +
                 "Inventory slots and context menu live on its World Space Canvas.")]
        [SerializeField] private VisorController _visorController;

        [Header("UI References")]
        [SerializeField] private InventorySlotUI[] _slotUIs;
        // TODO: AR grams UI will be redesigned
        // [SerializeField] private ARGramsCounterUI _arGramsCounterUI;

        [Header("Context Menu & Examination")]
        [SerializeField] private ItemContextMenu _contextMenu;
        [SerializeField] private ItemExaminer _itemExaminer;

        [Header("Drop Settings")]
        [Tooltip("Prefab spawns this far in front of the player when dropping.")]
        [SerializeField] private float _dropDistance = 1.5f;
        [SerializeField] private float _dropForce = 2f;

        [Header("Settings")]
        [SerializeField] private bool _pauseGameWhenOpen = false;

        [Header("Lock")]
        [Tooltip("If true, inventory cannot be opened with TAB until the player picks up the coms device.")]
        [SerializeField] private bool _lockUntilComsPickup = true;

        #endregion

        #region Private Fields

        private bool _isOpen;
        private bool _inventoryLocked;
        private PlayerInventory _playerInventory;
        private InputAction _toggleAction;
        private InputAction _closeAction;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;

        /// <summary>
        /// Fired when the player successfully equips an item from the inventory.
        /// Passes the item data of the equipped item.
        /// </summary>
        public event Action<InventoryItemData> OnItemEquipped;

        /// <summary>Fired when the inventory UI is opened.</summary>
        public event Action OnInventoryOpened;

        /// <summary>Fired when the inventory UI is closed.</summary>
        public event Action OnInventoryClosed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            _playerInventory = PlayerInventory.Instance;

            if (_playerInventory != null)
            {
                _playerInventory.OnSlotChanged += HandleSlotChanged;
                _playerInventory.OnARGramsChanged += HandleARGramsChanged;

                if (_lockUntilComsPickup)
                {
                    _inventoryLocked = true;
                    _playerInventory.OnComsDevicePickedUp.AddListener(HandleComsDevicePickedUp);
                }
            }

            // Find visor controller if not assigned
            if (_visorController == null)
            {
                _visorController = VisorController.Instance;
            }

            // Set up slot indices and subscribe to right-click events
            SetupSlotUIs();

            // Initialize UI with current inventory state
            RefreshUI();

            CloseInventory();
        }

        private void SetupSlotUIs()
        {
            if (_slotUIs == null) return;

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (_slotUIs[i] != null)
                {
                    _slotUIs[i].SetSlotIndex(i);
                    _slotUIs[i].OnRightClicked += HandleSlotRightClicked;
                }
            }
        }

        private void OnEnable()
        {
            _toggleAction = new InputAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
            _toggleAction.performed += OnToggleInventory;
            _toggleAction.Enable();

            // ESC closes inventory (intercepts before pause menu)
            _closeAction = new InputAction("CloseInventory", InputActionType.Button, "<Keyboard>/escape");
            _closeAction.performed += OnCloseInventory;
            _closeAction.Enable();
        }

        private void OnDisable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnToggleInventory;
                _toggleAction.Disable();
                _toggleAction.Dispose();
            }

            if (_closeAction != null)
            {
                _closeAction.performed -= OnCloseInventory;
                _closeAction.Disable();
                _closeAction.Dispose();
            }

            if (_playerInventory != null)
            {
                _playerInventory.OnSlotChanged -= HandleSlotChanged;
                _playerInventory.OnARGramsChanged -= HandleARGramsChanged;
                _playerInventory.OnComsDevicePickedUp.RemoveListener(HandleComsDevicePickedUp);
            }

            // Unsubscribe from slot events
            if (_slotUIs != null)
            {
                foreach (var slotUI in _slotUIs)
                {
                    if (slotUI != null)
                    {
                        slotUI.OnRightClicked -= HandleSlotRightClicked;
                    }
                }
            }

            // Context menu no longer uses persistent event subscriptions —
            // actions are wired dynamically per Show() call.
        }

        #endregion

        #region Input Handling

        private void OnToggleInventory(InputAction.CallbackContext context)
        {
            // Don't open inventory if locked (before coms device pickup)
            if (!_isOpen && _inventoryLocked) return;

            // Don't open inventory if game is paused
            if (!_isOpen && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
                return;

            // Don't open inventory if visor is raised
            if (!_isOpen && _visorController != null && _visorController.IsVisorRaised)
                return;

            ToggleInventory();
        }

        private void OnCloseInventory(InputAction.CallbackContext context)
        {
            if (!_isOpen) return;

            // If examiner is open, close it first — inventory stays open
            if (_itemExaminer != null && _itemExaminer.IsOpen)
            {
                _itemExaminer.Hide();
                return;
            }

            CloseInventory();
        }

        #endregion

        #region Public Methods

        public void ToggleInventory()
        {
            if (_isOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }

        public void OpenInventory()
        {
            _isOpen = true;

            // Show the visor panel (glass + inventory/terminal/vitals)
            if (_visorController != null)
            {
                _visorController.ShowPanel();
            }

            RefreshUI();

            if (_pauseGameWhenOpen)
            {
                Time.timeScale = 0f;
            }

            // Disable player input (movement, camera, etc.)
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }

            Debug.Log("[InventoryUI] OpenInventory() unlocking cursor");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[InventoryUI] OpenInventory() complete. Firing OnInventoryOpened (subscribers: {OnInventoryOpened?.GetInvocationList()?.Length ?? 0})");
            OnInventoryOpened?.Invoke();
        }

        public void CloseInventory()
        {
            _isOpen = false;

            // Close context menu and examiner first
            if (_contextMenu != null)
            {
                _contextMenu.Hide();
            }

            if (_itemExaminer != null && _itemExaminer.IsOpen)
            {
                _itemExaminer.Hide();
            }

            // Hide the visor panel
            if (_visorController != null)
            {
                _visorController.HidePanel();
            }

            if (_pauseGameWhenOpen)
            {
                Time.timeScale = 1f;
            }

            // Re-enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
            }

            Debug.Log("[InventoryUI] CloseInventory() locking cursor");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("[InventoryUI] CloseInventory() complete. Firing OnInventoryClosed.");
            OnInventoryClosed?.Invoke();
        }

        public void RefreshUI()
        {
            if (_playerInventory == null) return;

            // Update slot UIs
            if (_slotUIs != null)
            {
                for (int i = 0; i < _slotUIs.Length && i < _playerInventory.SlotCount; i++)
                {
                    if (_slotUIs[i] != null)
                    {
                        InventorySlot slot = _playerInventory.GetSlot(i);
                        _slotUIs[i].UpdateSlot(slot);
                    }
                }
            }

            // TODO: AR grams UI will be redesigned
        }

        #endregion

        #region Event Handlers

        private void HandleComsDevicePickedUp()
        {
            _inventoryLocked = false;
            if (_playerInventory != null)
                _playerInventory.OnComsDevicePickedUp.RemoveListener(HandleComsDevicePickedUp);
        }

        private void HandleSlotChanged(int slotIndex, InventorySlot slot)
        {
            if (_slotUIs != null && slotIndex < _slotUIs.Length && _slotUIs[slotIndex] != null)
            {
                _slotUIs[slotIndex].UpdateSlot(slot);
            }
        }

        private void HandleARGramsChanged(int newAmount)
        {
            // TODO: AR grams UI will be redesigned
        }

        private void HandleSlotRightClicked(int slotIndex, Vector2 screenPosition)
        {
            if (_playerInventory == null || _contextMenu == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            // Close examiner if open
            if (_itemExaminer != null && _itemExaminer.IsOpen)
                _itemExaminer.Hide();

            var actions = BuildContextActions(slot.ItemData);
            _contextMenu.Show(slotIndex, screenPosition, actions);
        }

        /// <summary>
        /// Builds the context menu action list based on item type.
        /// Order: type-specific action → Examine → Drop.
        /// </summary>
        private List<ContextMenuAction> BuildContextActions(InventoryItemData itemData)
        {
            var actions = new List<ContextMenuAction>();

            // Type-specific actions first
            if (itemData is SchematicItemData)
                actions.Add(new ContextMenuAction { Label = "UPLOAD", Callback = HandleUploadSchematic });

            if (itemData is WeaponItemData || itemData is SuitAddonItemData || itemData is ComsDeviceItemData)
                actions.Add(new ContextMenuAction { Label = "EQUIP", Callback = HandleEquipRequested });

            // Universal actions
            actions.Add(new ContextMenuAction { Label = "EXAMINE", Callback = HandleExamineRequested });

            // Drop — everything except key items and coms device
            if (itemData.itemType != PhysicalItemType.KeyItem && itemData.itemType != PhysicalItemType.ComsDevice)
                actions.Add(new ContextMenuAction { Label = "DROP", Callback = HandleDropRequested });

            return actions;
        }

        private void HandleExamineRequested(int slotIndex)
        {
            if (_playerInventory == null || _itemExaminer == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            // Inventory stays open — examiner overlays on top
            _itemExaminer.Show(slot.ItemData);
        }

        private void HandleEquipRequested(int slotIndex)
        {
            if (_playerInventory == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            PlayerEquipment equipment = PlayerEquipment.Instance;
            if (equipment == null)
            {
                Debug.LogWarning("[InventoryUI] PlayerEquipment not found.");
                return;
            }

            InventoryItemData itemData = slot.ItemData;
            EquipmentSlotType targetSlot = DetermineEquipSlot(itemData, equipment);

            if (!equipment.CanEquip(itemData, targetSlot))
            {
                Debug.LogWarning($"[InventoryUI] Cannot equip '{itemData.displayName}' to {targetSlot}.");
                return;
            }

            if (equipment.TryEquip(itemData, targetSlot))
            {
                // Remove from inventory — TryEquip handles returning any swapped item
                _playerInventory.RemoveItemFromSlot(slotIndex, 1);
                Debug.Log($"[InventoryUI] Item equipped: '{itemData.displayName}'. Firing OnItemEquipped (subscribers: {OnItemEquipped?.GetInvocationList()?.Length ?? 0})");
                OnItemEquipped?.Invoke(itemData);
            }
        }

        private void HandleUploadSchematic(int slotIndex)
        {
            if (_playerInventory == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            if (slot.ItemData is not SchematicItemData schematicItem) return;

            SchematicSO schematic = schematicItem.schematicToUnlock;
            if (schematic == null)
            {
                Debug.LogWarning($"[InventoryUI] Schematic item '{schematicItem.displayName}' has no schematicToUnlock assigned.");
                return;
            }

            // Check if already unlocked
            if (SchematicRegistry.Instance != null && SchematicRegistry.Instance.IsUnlocked(schematic))
            {
                Debug.Log($"[InventoryUI] Schematic '{schematic.schematicId}' already uploaded.");
                return;
            }

            // Unlock the schematic and consume the item
            if (SchematicRegistry.Instance != null)
            {
                SchematicRegistry.Instance.UnlockSchematic(schematic);
                _playerInventory.RemoveItemFromSlot(slotIndex, 1);
                Debug.Log($"[InventoryUI] Uploaded schematic '{schematic.schematicId}' — recipe unlocked.");
            }
            else
            {
                Debug.LogWarning("[InventoryUI] SchematicRegistry not found. Cannot upload schematic.");
            }
        }

        /// <summary>
        /// Determines the best equipment slot for an item.
        /// Weapons: prefers empty slot matching their type, falls back to the other if compatible.
        /// Suit addons: always go to SuitAddon slot.
        /// </summary>
        private EquipmentSlotType DetermineEquipSlot(InventoryItemData itemData, PlayerEquipment equipment)
        {
            if (itemData is SuitAddonItemData)
                return EquipmentSlotType.SuitAddon;

            if (itemData is ComsDeviceItemData)
                return EquipmentSlotType.ComsDevice;

            if (itemData is WeaponItemData weaponData)
            {
                switch (weaponData.weaponSlot)
                {
                    case WeaponSlotType.PrimaryOnly:
                        return EquipmentSlotType.PrimaryWeapon;

                    case WeaponSlotType.SecondaryOnly:
                        return EquipmentSlotType.SecondaryWeapon;

                    case WeaponSlotType.PrimaryOrSecondary:
                        // Prefer the empty slot; if both empty prefer primary; if both full prefer primary (swap)
                        EquipmentSlot primary = equipment.GetSlot(EquipmentSlotType.PrimaryWeapon);
                        EquipmentSlot secondary = equipment.GetSlot(EquipmentSlotType.SecondaryWeapon);

                        if (primary.IsEmpty && !secondary.IsEmpty) return EquipmentSlotType.PrimaryWeapon;
                        if (!primary.IsEmpty && secondary.IsEmpty) return EquipmentSlotType.SecondaryWeapon;
                        return EquipmentSlotType.PrimaryWeapon; // both empty or both full → primary
                }
            }

            // Fallback (should never happen for equippable items)
            return EquipmentSlotType.PrimaryWeapon;
        }

        private void HandleDropRequested(int slotIndex)
        {
            if (_playerInventory == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            InventoryItemData itemData = slot.ItemData;
            int quantity = slot.Quantity;

            // Remove all items from the slot
            _playerInventory.RemoveItemFromSlot(slotIndex, quantity);

            // Spawn the item in the world with tracking
            SpawnDroppedItem(itemData, quantity);
        }

        private void SpawnDroppedItem(InventoryItemData itemData, int quantity = 1)
        {
            if (itemData.worldPrefab == null)
            {
                Debug.LogWarning($"[InventoryUI] Cannot drop {itemData.displayName} - no worldPrefab assigned");
                return;
            }

            // Find player camera for drop direction
            Camera playerCamera = Camera.main;
            if (playerCamera == null) return;

            Vector3 spawnPosition = playerCamera.transform.position +
                                    playerCamera.transform.forward * _dropDistance;

            // Spawn the prefab
            GameObject droppedItem = Instantiate(itemData.worldPrefab, spawnPosition, Quaternion.identity);

            // Parent to pickups container
            GameObject pickupsContainer = GameObject.Find("--- PICKUPS ---");
            if (pickupsContainer == null)
            {
                pickupsContainer = new GameObject("--- PICKUPS ---");
            }
            droppedItem.transform.SetParent(pickupsContainer.transform);

            // Generate a unique dropped item ID
            string droppedItemId = $"dropped_{itemData.itemId}_{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Set the pickup ID on the spawned pickup component
            Pickup pickup = droppedItem.GetComponent<Pickup>();
            if (pickup != null)
            {
                pickup.SetPickupId(droppedItemId);
            }
            else
            {
                Debug.LogWarning($"[ItemPersistence] WARNING: worldPrefab for '{itemData.itemId}' has no Pickup component! It won't be re-pickable.");
            }

            // Create dropped item tracking data
            DroppedItemData droppedData = new DroppedItemData
            {
                droppedItemId = droppedItemId,
                itemId = itemData.itemId,
                quantity = quantity,
                posX = spawnPosition.x,
                posY = spawnPosition.y,
                posZ = spawnPosition.z,
                rotX = 0f,
                rotY = 0f,
                rotZ = 0f
            };

            // Add to the correct persistence scope
            var floorManager = FloorStateManager.Instance;
            if (floorManager != null && floorManager.IsInitialized)
            {
                bool isSafeRoom = FloorStateManager.IsPositionInSafeRoom(spawnPosition);

                if (isSafeRoom)
                {
                    floorManager.SafeRoomDroppedItems.Add(droppedData);
                }
                else
                {
                    FloorState floorState = floorManager.GetCurrentFloorState();
                    floorState.droppedItems.Add(droppedData);
                }
            }
            else
            {
                Debug.LogWarning($"[ItemPersistence] DROPPED ITEM NOT TRACKED! FloorStateManager null: {floorManager == null}, initialized: {floorManager?.IsInitialized}");
            }

            // Apply some force to make it feel natural
            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(playerCamera.transform.forward * _dropForce, ForceMode.Impulse);
            }
        }

        #endregion
    }
}
