using System;
using System.Collections;
using _Scripts.Systems.Inventory.ItemTypes;
using _Scripts.Systems.Player;
using _Scripts.Systems.Weapon;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Manages the player's 4 equipment slots (Primary Weapon, Secondary Weapon, Suit Addon, COMS Device).
    /// Bridges inventory items to runtime gameplay systems (Kinemation weapons, suit addon behaviours, COMS device).
    /// Lives on the same GameObject as TacticalShooterPlayer and PlayerInventory.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        #region Singleton

        private static PlayerEquipment _instance;
        public static PlayerEquipment Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<PlayerEquipment>();
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>Fired when any equipment slot changes (equip or unequip).</summary>
        public event Action<EquipmentSlotType, EquipmentSlot> OnEquipmentChanged;

        /// <summary>Fired when the active weapon slot switches (0 = primary, 1 = secondary).</summary>
        public event Action<int> OnActiveWeaponSlotChanged;

        #endregion

        #region Private Fields

        private const int SlotCount = 4;

        private EquipmentSlot[] _slots;
        private int _activeWeaponSlot; // 0 = primary, 1 = secondary

        private TacticalShooterPlayer _tacticalPlayer;
        private WeaponHitDetector _hitDetector;
        private MovementController _movementController;
        private ComsDeviceController _comsController;

        // Input
        private InputAction _selectWeapon1;
        private InputAction _selectWeapon2;
        private InputAction _scrollWeapon;
        private InputAction _quickDrawSecondary;
        private InputAction _comsToggle;

        private bool _isSwitching;

        // Holster state — weapon is equipped but not drawn
        private bool _isHolstered;

        // Terminal interaction mode — blocks all weapon actions
        private bool _isInTerminalMode;

        #endregion

        #region Properties

        public int ActiveWeaponSlot => _activeWeaponSlot;

        public EquipmentSlot GetSlot(EquipmentSlotType type)
        {
            int index = (int)type;
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        public bool HasWeaponEquipped =>
            !_slots[(int)EquipmentSlotType.PrimaryWeapon].IsEmpty ||
            !_slots[(int)EquipmentSlotType.SecondaryWeapon].IsEmpty;

        /// <summary>Whether the active weapon is holstered (equipped but not drawn).</summary>
        public bool IsHolstered => _isHolstered;

        /// <summary>Whether the player is in terminal interaction mode (weapons blocked).</summary>
        public bool IsInTerminalMode => _isInTerminalMode;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            _slots = new EquipmentSlot[SlotCount];
            _slots[0] = new EquipmentSlot { SlotType = EquipmentSlotType.PrimaryWeapon };
            _slots[1] = new EquipmentSlot { SlotType = EquipmentSlotType.SecondaryWeapon };
            _slots[2] = new EquipmentSlot { SlotType = EquipmentSlotType.SuitAddon };
            _slots[3] = new EquipmentSlot { SlotType = EquipmentSlotType.ComsDevice };
        }

        private void Start()
        {
            _tacticalPlayer = GetComponent<TacticalShooterPlayer>();
            _hitDetector = GetComponent<WeaponHitDetector>();
            _movementController = GetComponent<MovementController>();
            _comsController = GetComponent<ComsDeviceController>();

            // Tell TacticalShooterPlayer to skip its hardcoded weapon spawn
            if (_tacticalPlayer != null)
            {
                _tacticalPlayer.UseEquipmentSystem = true;
            }
        }

        private void OnEnable()
        {
            // Key 1 → primary weapon
            _selectWeapon1 = new InputAction("SelectWeapon1", InputActionType.Button, "<Keyboard>/1");
            _selectWeapon1.performed += _ => SwitchActiveWeapon(0);
            _selectWeapon1.Enable();

            // Key 2 → secondary weapon
            _selectWeapon2 = new InputAction("SelectWeapon2", InputActionType.Button, "<Keyboard>/2");
            _selectWeapon2.performed += _ => SwitchActiveWeapon(1);
            _selectWeapon2.Enable();

            // Scroll wheel → toggle between primary/secondary
            _scrollWeapon = new InputAction("ScrollWeapon", InputActionType.Value, "<Mouse>/scroll/y");
            _scrollWeapon.performed += ctx =>
            {
                float val = ctx.ReadValue<float>();
                if (val > 0f) SwitchToNextWeapon();
                else if (val < 0f) SwitchToPreviousWeapon();
            };
            _scrollWeapon.Enable();

            // X key → quick draw secondary weapon in off-hand
            _quickDrawSecondary = new InputAction("QuickDrawSecondary", InputActionType.Button, "<Keyboard>/x");
            _quickDrawSecondary.performed += _ => OnQuickDrawSecondary();
            _quickDrawSecondary.Enable();

            // Key 3 → toggle COMS device
            _comsToggle = new InputAction("ComsToggle", InputActionType.Button, "<Keyboard>/3");
            _comsToggle.performed += _ => HandleComsToggle();
            _comsToggle.Enable();
        }

        private void OnDisable()
        {
            _selectWeapon1?.Disable();
            _selectWeapon1?.Dispose();

            _selectWeapon2?.Disable();
            _selectWeapon2?.Dispose();

            _scrollWeapon?.Disable();
            _scrollWeapon?.Dispose();

            _quickDrawSecondary?.Disable();
            _quickDrawSecondary?.Dispose();

            _comsToggle?.Disable();
            _comsToggle?.Dispose();
        }

        #endregion

        #region Public Methods — Equip / Unequip

        /// <summary>
        /// Checks whether the given item can be equipped in the target slot.
        /// </summary>
        public bool CanEquip(InventoryItemData itemData, EquipmentSlotType targetSlot)
        {
            if (itemData == null) return false;

            switch (targetSlot)
            {
                case EquipmentSlotType.PrimaryWeapon:
                    if (itemData is WeaponItemData wpnPrimary)
                        return wpnPrimary.weaponSlot == WeaponSlotType.PrimaryOnly ||
                               wpnPrimary.weaponSlot == WeaponSlotType.PrimaryOrSecondary;
                    return false;

                case EquipmentSlotType.SecondaryWeapon:
                    if (itemData is WeaponItemData wpnSecondary)
                        return wpnSecondary.weaponSlot == WeaponSlotType.SecondaryOnly ||
                               wpnSecondary.weaponSlot == WeaponSlotType.PrimaryOrSecondary;
                    return false;

                case EquipmentSlotType.SuitAddon:
                    return itemData is SuitAddonItemData;

                case EquipmentSlotType.ComsDevice:
                    return itemData is ComsDeviceItemData;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Equips an item into the target slot. If the slot is occupied, the old item
        /// is returned to PlayerInventory. Returns true on success.
        /// The item must be removed from inventory by the caller AFTER this succeeds.
        /// </summary>
        public bool TryEquip(InventoryItemData itemData, EquipmentSlotType targetSlot)
        {
            if (!CanEquip(itemData, targetSlot)) return false;

            EquipmentSlot slot = _slots[(int)targetSlot];

            // If slot is occupied, unequip the old item back to inventory
            if (!slot.IsEmpty)
            {
                InventoryItemData oldItem = slot.ItemData;

                // Check if inventory has room before we swap
                if (!PlayerInventory.Instance.HasRoomFor(oldItem))
                {
                    return false;
                }

                // Destroy the runtime instance
                DestroyEquipment(targetSlot);

                // Return old item to inventory
                PlayerInventory.Instance.TryAddItem(oldItem);
            }

            // Assign new item
            slot.ItemData = itemData;

            // Instantiate the runtime equipment
            InstantiateEquipment(targetSlot);

            OnEquipmentChanged?.Invoke(targetSlot, slot);

            // If this is a weapon slot and it's the only weapon, make it active
            if (targetSlot == EquipmentSlotType.PrimaryWeapon || targetSlot == EquipmentSlotType.SecondaryWeapon)
            {
                AutoActivateIfNeeded((int)targetSlot);
            }

            return true;
        }

        /// <summary>
        /// Unequips the item from the given slot. Returns the item data
        /// (caller is responsible for adding it to inventory or dropping it).
        /// </summary>
        public InventoryItemData Unequip(EquipmentSlotType slotType)
        {
            EquipmentSlot slot = _slots[(int)slotType];
            if (slot.IsEmpty) return null;

            InventoryItemData removedItem = slot.ItemData;

            DestroyEquipment(slotType);
            slot.Clear();

            OnEquipmentChanged?.Invoke(slotType, slot);

            // If we unequipped the active weapon, try to switch to the other slot
            if ((int)slotType == _activeWeaponSlot)
            {
                int otherSlot = _activeWeaponSlot == 0 ? 1 : 0;
                if (!_slots[otherSlot].IsEmpty)
                {
                    ActivateWeaponSlot(otherSlot);
                }
                else
                {
                    // No weapons left — enter unarmed state
                    _isHolstered = true;
                    if (_tacticalPlayer != null && !_tacticalPlayer.IsUnarmed)
                        _tacticalPlayer.EnterUnarmedState();
                }
            }

            return removedItem;
        }

        /// <summary>
        /// Unequips a weapon with holster animation, then returns it to inventory.
        /// For suit addons, unequips immediately (no animation).
        /// Handles the quick-draw edge case: if the secondary is being held in the
        /// off-hand via quick draw, exits quick draw first, then unequips.
        /// </summary>
        public void UnequipToInventoryAnimated(EquipmentSlotType slotType)
        {
            EquipmentSlot slot = _slots[(int)slotType];
            if (slot.IsEmpty) return;
            if (_isSwitching) return;

            bool isWeaponSlot = slotType == EquipmentSlotType.PrimaryWeapon ||
                                slotType == EquipmentSlotType.SecondaryWeapon;
            bool isActiveWeapon = isWeaponSlot && (int)slotType == _activeWeaponSlot;

            // Edge case: unequipping the secondary while it's quick-drawn in the off-hand
            bool isQuickDrawnSecondary = slotType == EquipmentSlotType.SecondaryWeapon &&
                                         _tacticalPlayer != null &&
                                         _tacticalPlayer.IsQuickDrawActive;

            if ((isActiveWeapon || isQuickDrawnSecondary) && _tacticalPlayer != null && !_isHolstered)
            {
                StartCoroutine(AnimatedUnequipCoroutine(slotType, isQuickDrawnSecondary));
            }
            else
            {
                // Non-active weapon or suit addon — unequip immediately
                FinishUnequipToInventory(slotType);
            }
        }

        private IEnumerator AnimatedUnequipCoroutine(EquipmentSlotType slotType, bool exitQuickDrawFirst)
        {
            _isSwitching = true;

            if (exitQuickDrawFirst)
            {
                // Exit quick draw mode — holsters the off-hand pistol back, restores primary
                _tacticalPlayer.QuickDrawByReference(null);

                // Quick draw holster is fast (~0.35s based on Holster(false, 0.35f) in ExecuteQuickDrawToggle)
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                // Play holster animation on the active weapon
                float holsterDelay = _tacticalPlayer.HolsterActiveWeapon();
                if (holsterDelay > 0f)
                    yield return new WaitForSeconds(holsterDelay);
            }

            FinishUnequipToInventory(slotType);

            _isSwitching = false;
        }

        private void FinishUnequipToInventory(EquipmentSlotType slotType)
        {
            InventoryItemData removedItem = Unequip(slotType);
            if (removedItem == null) return;

            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                if (!inventory.TryAddItem(removedItem))
                {
                    Debug.LogWarning($"[PlayerEquipment] Inventory full — could not return '{removedItem.displayName}'.");
                    // Re-equip since we can't put it back
                    TryEquip(removedItem, slotType);
                }
            }
        }

        #endregion

        #region Public Methods — Weapon Switching

        /// <summary>
        /// Switches to the weapon in the given slot (0 = primary, 1 = secondary).
        /// Pressing the same slot key toggles holster/draw.
        /// </summary>
        public void SwitchActiveWeapon(int slotIndex)
        {
            if (_isInTerminalMode) return;
            if (slotIndex < 0 || slotIndex > 1) return;
            if (_slots[slotIndex].IsEmpty) return;
            if (_isSwitching) return;

            // Block switching while quick draw is active — player must holster pistol first (X)
            if (_tacticalPlayer != null && _tacticalPlayer.IsQuickDrawActive) return;

            // If COMS is active and switching to primary → deactivate COMS first
            if (_comsController != null && _comsController.IsActive && slotIndex == 0)
            {
                _comsController.ToggleComs();
            }

            // Pressing the active slot's key while drawn → holster
            if (slotIndex == _activeWeaponSlot && !_isHolstered)
            {
                HolsterWeapon();
                return;
            }

            // Any key press while holstered → draw that weapon
            if (_isHolstered)
            {
                DrawWeapon(slotIndex);
                return;
            }

            // Normal switch between different slots
            StartCoroutine(SwitchWeaponCoroutine(slotIndex));
        }

        public void SwitchToNextWeapon()
        {
            // Scroll while holstered → draw the current weapon
            if (_isHolstered)
            {
                if (!_slots[_activeWeaponSlot].IsEmpty)
                    DrawWeapon(_activeWeaponSlot);
                return;
            }

            int next = _activeWeaponSlot == 0 ? 1 : 0;
            SwitchActiveWeapon(next);
        }

        public void SwitchToPreviousWeapon()
        {
            // Scroll while holstered → draw the current weapon
            if (_isHolstered)
            {
                if (!_slots[_activeWeaponSlot].IsEmpty)
                    DrawWeapon(_activeWeaponSlot);
                return;
            }

            int prev = _activeWeaponSlot == 0 ? 1 : 0;
            SwitchActiveWeapon(prev);
        }

        /// <summary>
        /// Toggles the secondary weapon in the off-hand (quick draw / holster).
        /// Requires a primary weapon to be active and a secondary weapon equipped.
        /// </summary>
        public void OnQuickDrawSecondary()
        {
            if (_isInTerminalMode) return;
            if (_tacticalPlayer == null) return;
            if (_isSwitching) return;
            if (_isHolstered) return; // Can't quick draw while holstered

            // If already in quick draw, always allow toggling off
            if (_tacticalPlayer.IsQuickDrawActive)
            {
                _tacticalPlayer.QuickDrawByReference(null); // weapon arg ignored during holster
                return;
            }

            // Need a primary weapon active and a secondary weapon equipped
            if (_slots[(int)EquipmentSlotType.PrimaryWeapon].IsEmpty) return;
            if (_slots[(int)EquipmentSlotType.SecondaryWeapon].IsEmpty) return;
            if (_activeWeaponSlot != 0) return; // must be on primary slot

            var secondaryWeapon = GetWeaponFromSlot(1);
            if (secondaryWeapon == null) return;

            _tacticalPlayer.QuickDrawByReference(secondaryWeapon);

        }

        /// <summary>
        /// Holsters the active weapon — plays holster animation, then enters unarmed state.
        /// The weapon stays equipped in its slot but is not drawn.
        /// </summary>
        public void HolsterWeapon()
        {
            if (_isHolstered) return;
            if (_isSwitching) return;
            if (_tacticalPlayer == null) return;
            if (!HasWeaponEquipped) return;

            StartCoroutine(HolsterCoroutine());
        }

        /// <summary>
        /// Draws the weapon in the given slot from holstered/unarmed state.
        /// </summary>
        public void DrawWeapon(int slotIndex)
        {
            if (!_isHolstered) return;
            if (_isSwitching) return;
            if (slotIndex < 0 || slotIndex > 1) return;
            if (_slots[slotIndex].IsEmpty) return;
            if (_tacticalPlayer == null) return;

            var weapon = GetWeaponFromSlot(slotIndex);
            if (weapon == null) return;

            _activeWeaponSlot = slotIndex;
            _isHolstered = false;

            _tacticalPlayer.EnterArmedState(weapon);

            OnActiveWeaponSlotChanged?.Invoke(_activeWeaponSlot);

        }

        private IEnumerator HolsterCoroutine()
        {
            _isSwitching = true;

            float delay = _tacticalPlayer.HolsterActiveWeapon();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            _tacticalPlayer.EnterUnarmedState();
            _isHolstered = true;
            _isSwitching = false;

        }

        #endregion

        #region COMS Device

        /// <summary>
        /// Handles key 3 press — toggles the COMS device on/off.
        /// If the primary weapon is active, holsters it first.
        /// </summary>
        private void HandleComsToggle()
        {
            if (_isInTerminalMode) return;
            if (_comsController == null || !_comsController.IsEquipped) return;
            if (_isSwitching) return;

            if (_comsController.IsActive)
            {
                // Deactivate COMS
                _comsController.ToggleComs();
            }
            else
            {
                // Block if quick draw is active
                if (_tacticalPlayer != null && _tacticalPlayer.IsQuickDrawActive) return;

                // Primary weapon active → holster first, then activate COMS
                if (_activeWeaponSlot == 0 && !_isHolstered && !_slots[0].IsEmpty)
                {
                    StartCoroutine(HolsterThenActivateComs());
                }
                else
                {
                    // Pistol active or unarmed → activate directly
                    _comsController.ToggleComs();
                }
            }
        }

        private IEnumerator HolsterThenActivateComs()
        {
            _isSwitching = true;

            float holsterDelay = _tacticalPlayer.HolsterActiveWeapon();
            if (holsterDelay > 0f)
                yield return new WaitForSeconds(holsterDelay);

            _tacticalPlayer.EnterUnarmedState();
            _isHolstered = true;
            _isSwitching = false;

            _comsController.ToggleComs();
        }

        /// <summary>Whether the COMS device is currently active (toggled on).</summary>
        public bool IsComsActive => _comsController != null && _comsController.IsActive;

        #endregion

        #region Terminal Mode

        /// <summary>
        /// Enters terminal interaction mode — holsters the active weapon (if drawn),
        /// blocks all weapon input (fire, aim, reload, switch, etc.).
        /// Called by TerminalScreenInteraction when the player looks at the screen.
        /// </summary>
        public void EnterTerminalMode()
        {
            if (_isInTerminalMode) return;

            _isInTerminalMode = true;

            // Deactivate COMS if active
            if (_comsController != null && _comsController.IsActive)
                _comsController.ToggleComs();

            // Block weapon input on TacticalShooterPlayer
            if (_tacticalPlayer != null)
                _tacticalPlayer.BlockWeaponInput = true;

            // Holster weapon if currently drawn
            if (!_isHolstered && HasWeaponEquipped)
                HolsterWeapon();
        }

        /// <summary>
        /// Exits terminal interaction mode — unblocks weapon input so the player
        /// can draw their weapon manually (1/2/scroll). Does NOT auto-draw to
        /// avoid animation spam when the crosshair drifts off screen edges.
        /// </summary>
        public void ExitTerminalMode()
        {
            if (!_isInTerminalMode) return;

            _isInTerminalMode = false;

            // Unblock weapon input — player draws manually when ready
            if (_tacticalPlayer != null)
                _tacticalPlayer.BlockWeaponInput = false;
        }

        #endregion

        #region Save / Restore

        public EquipmentSaveData ToSaveData()
        {
            var data = new EquipmentSaveData
            {
                activeWeaponSlot = _activeWeaponSlot,
                slots = new EquipmentSlotSaveData[SlotCount]
            };

            for (int i = 0; i < SlotCount; i++)
            {
                data.slots[i] = new EquipmentSlotSaveData
                {
                    itemId = _slots[i].IsEmpty ? "" : _slots[i].ItemData.itemId,
                    slotType = _slots[i].SlotType
                };
            }

            return data;
        }

        public void RestoreFromSaveData(EquipmentSaveData data)
        {
            if (data == null) return;

            for (int i = 0; i < SlotCount && i < data.slots.Length; i++)
            {
                if (string.IsNullOrEmpty(data.slots[i].itemId)) continue;

                InventoryItemData itemData = ItemDatabase.FindByItemId(data.slots[i].itemId);
                if (itemData != null)
                {
                    TryEquip(itemData, data.slots[i].slotType);
                }
                else
                {
                    Debug.LogWarning($"[PlayerEquipment] Could not find item '{data.slots[i].itemId}' during restore.");
                }
            }

            // Activate the saved weapon slot
            _activeWeaponSlot = data.activeWeaponSlot;
            EquipmentSlot activeSlot = _slots[_activeWeaponSlot];
            if (!activeSlot.IsEmpty && activeSlot.RuntimeInstance != null)
            {
                var weapon = activeSlot.RuntimeInstance.GetComponentInChildren<TacticalShooterWeapon>();
                if (weapon != null && _tacticalPlayer != null)
                {
                    _isHolstered = false;
                    _tacticalPlayer.EnterArmedState(weapon);
                }
            }

        }

        #endregion

        #region Private Methods — Instantiation

        private void InstantiateEquipment(EquipmentSlotType slotType)
        {
            EquipmentSlot slot = _slots[(int)slotType];

            if (slotType == EquipmentSlotType.PrimaryWeapon || slotType == EquipmentSlotType.SecondaryWeapon)
            {
                InstantiateWeapon(slot);
            }
            else if (slotType == EquipmentSlotType.SuitAddon)
            {
                InstantiateSuitAddon(slot);
            }
            else if (slotType == EquipmentSlotType.ComsDevice)
            {
                InstantiateComsDevice(slot);
            }
        }

        private void InstantiateWeapon(EquipmentSlot slot)
        {
            if (_tacticalPlayer == null) return;

            var weaponData = slot.ItemData as WeaponItemData;
            if (weaponData == null || weaponData.weaponPrefab == null)
            {
                Debug.LogWarning($"[PlayerEquipment] WeaponItemData '{slot.ItemData?.displayName}' has no weaponPrefab.");
                return;
            }

            TacticalShooterWeapon weapon = _tacticalPlayer.AddWeapon(weaponData.weaponPrefab);

            if (_hitDetector != null)
                _hitDetector.SubscribeToWeapon(weapon);

            slot.RuntimeInstance = weapon.GetWeaponRoot().gameObject;

        }

        private void InstantiateSuitAddon(EquipmentSlot slot)
        {
            var addonData = slot.ItemData as SuitAddonItemData;
            if (addonData == null) return;

            if (addonData.addonBehaviourPrefab != null)
            {
                slot.RuntimeInstance = Instantiate(addonData.addonBehaviourPrefab, transform);
            }

            // Apply stat modifiers
            if (_movementController != null)
            {
                _movementController.SetEquipmentSpeedMultiplier(addonData.moveSpeedMultiplier);
            }

            // TODO: Apply noise multiplier when NoiseManager supports it

        }

        private void DestroyEquipment(EquipmentSlotType slotType)
        {
            EquipmentSlot slot = _slots[(int)slotType];

            if (slotType == EquipmentSlotType.PrimaryWeapon || slotType == EquipmentSlotType.SecondaryWeapon)
            {
                DestroyWeapon(slot);
            }
            else if (slotType == EquipmentSlotType.SuitAddon)
            {
                DestroySuitAddon(slot);
            }
            else if (slotType == EquipmentSlotType.ComsDevice)
            {
                DestroyComsDevice(slot);
            }
        }

        private void DestroyWeapon(EquipmentSlot slot)
        {
            if (slot.RuntimeInstance == null) return;

            var weapon = slot.RuntimeInstance.GetComponentInChildren<TacticalShooterWeapon>();
            if (weapon != null)
            {
                if (_hitDetector != null)
                    _hitDetector.UnsubscribeFromWeapon(weapon);

                if (_tacticalPlayer != null)
                    _tacticalPlayer.RemoveWeapon(weapon);
            }

            slot.RuntimeInstance = null;
        }

        private void DestroySuitAddon(EquipmentSlot slot)
        {
            if (slot.RuntimeInstance != null)
            {
                Destroy(slot.RuntimeInstance);
            }

            // Revert stat modifiers
            if (_movementController != null)
            {
                _movementController.ClearEquipmentSpeedMultiplier();
            }

            slot.RuntimeInstance = null;
        }

        private void InstantiateComsDevice(EquipmentSlot slot)
        {
            var comsData = slot.ItemData as ComsDeviceItemData;
            if (comsData == null || comsData.comsBehaviourPrefab == null)
            {
                Debug.LogWarning($"[PlayerEquipment] ComsDeviceItemData '{slot.ItemData?.displayName}' has no comsBehaviourPrefab.");
                return;
            }

            if (_comsController != null)
                _comsController.EquipDevice(comsData);
        }

        private void DestroyComsDevice(EquipmentSlot slot)
        {
            if (_comsController != null)
                _comsController.UnequipDevice();

            slot.RuntimeInstance = null;
        }

        #endregion

        #region Private Methods — Switching

        private IEnumerator SwitchWeaponCoroutine(int targetSlot)
        {
            _isSwitching = true;

            // Holster current weapon (plays holster animation)
            float holsterDelay = 0f;
            EquipmentSlot currentSlot = _slots[_activeWeaponSlot];
            if (!currentSlot.IsEmpty && currentSlot.RuntimeInstance != null)
            {
                holsterDelay = _tacticalPlayer.HolsterActiveWeapon();
            }

            if (holsterDelay > 0f)
                yield return new WaitForSeconds(holsterDelay);

            // Switch to target weapon — ActivateWeaponByReference handles:
            //   1. Hiding the old weapon
            //   2. Setting the new weapon as active
            //   3. Playing the draw animation
            _activeWeaponSlot = targetSlot;
            var targetWeapon = GetWeaponFromSlot(targetSlot);
            if (targetWeapon != null && _tacticalPlayer != null)
            {
                _tacticalPlayer.ActivateWeaponByReference(targetWeapon);
            }

            _isSwitching = false;

            OnActiveWeaponSlotChanged?.Invoke(_activeWeaponSlot);

        }

        private void ActivateWeaponSlot(int slotIndex)
        {
            var weapon = GetWeaponFromSlot(slotIndex);
            if (weapon == null) return;

            _activeWeaponSlot = slotIndex;
            _isHolstered = false;

            if (_tacticalPlayer != null)
                _tacticalPlayer.EnterArmedState(weapon);

            OnActiveWeaponSlotChanged?.Invoke(_activeWeaponSlot);
        }

        private void AutoActivateIfNeeded(int equippedSlotIndex)
        {
            // If this is the first weapon equipped, activate it automatically
            int otherSlot = equippedSlotIndex == 0 ? 1 : 0;
            if (_slots[otherSlot].IsEmpty || _activeWeaponSlot == equippedSlotIndex)
            {
                ActivateWeaponSlot(equippedSlotIndex);
            }
        }

        private TacticalShooterWeapon GetWeaponFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex > 1) return null;
            EquipmentSlot slot = _slots[slotIndex];
            if (slot.IsEmpty || slot.RuntimeInstance == null) return null;
            return slot.RuntimeInstance.GetComponentInChildren<TacticalShooterWeapon>();
        }

        #endregion
    }
}
