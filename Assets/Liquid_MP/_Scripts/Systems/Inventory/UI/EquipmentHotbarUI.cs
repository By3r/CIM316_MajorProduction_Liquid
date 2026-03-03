using UnityEngine;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Manages the 3 equipment slot UIs on the player's hotbar.
    /// Subscribes to PlayerEquipment events and updates each EquipmentSlotUI.
    /// Handles right-click on equipment slots for unequipping back to inventory.
    /// </summary>
    public class EquipmentHotbarUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Equipment Slot UIs")]
        [Tooltip("The EquipmentSlotUI for the Primary Weapon slot.")]
        [SerializeField] private EquipmentSlotUI _primarySlotUI;

        [Tooltip("The EquipmentSlotUI for the Secondary Weapon slot.")]
        [SerializeField] private EquipmentSlotUI _secondarySlotUI;

        [Tooltip("The EquipmentSlotUI for the Suit Addon slot.")]
        [SerializeField] private EquipmentSlotUI _suitAddonSlotUI;

        #endregion

        #region Private Fields

        private PlayerEquipment _equipment;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _equipment = PlayerEquipment.Instance;

            if (_equipment != null)
            {
                _equipment.OnEquipmentChanged += HandleEquipmentChanged;
            }

            // Subscribe to right-click events on each slot
            if (_primarySlotUI != null) _primarySlotUI.OnRightClicked += HandleSlotRightClicked;
            if (_secondarySlotUI != null) _secondarySlotUI.OnRightClicked += HandleSlotRightClicked;
            if (_suitAddonSlotUI != null) _suitAddonSlotUI.OnRightClicked += HandleSlotRightClicked;

            // Initial refresh
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_equipment != null)
            {
                _equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            }

            if (_primarySlotUI != null) _primarySlotUI.OnRightClicked -= HandleSlotRightClicked;
            if (_secondarySlotUI != null) _secondarySlotUI.OnRightClicked -= HandleSlotRightClicked;
            if (_suitAddonSlotUI != null) _suitAddonSlotUI.OnRightClicked -= HandleSlotRightClicked;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes all 3 equipment slot UIs from current PlayerEquipment state.
        /// </summary>
        public void RefreshAll()
        {
            if (_equipment == null) return;

            UpdateSlotUI(_primarySlotUI, EquipmentSlotType.PrimaryWeapon);
            UpdateSlotUI(_secondarySlotUI, EquipmentSlotType.SecondaryWeapon);
            UpdateSlotUI(_suitAddonSlotUI, EquipmentSlotType.SuitAddon);
        }

        #endregion

        #region Event Handlers

        private void HandleEquipmentChanged(EquipmentSlotType slotType, EquipmentSlot slot)
        {
            EquipmentSlotUI slotUI = GetSlotUI(slotType);
            if (slotUI != null)
            {
                slotUI.UpdateSlot(slot);
            }
        }

        private void HandleSlotRightClicked(EquipmentSlotType slotType, Vector2 screenPosition)
        {
            if (_equipment == null) return;

            EquipmentSlot slot = _equipment.GetSlot(slotType);
            if (slot == null || slot.IsEmpty) return;

            // Unequip with holster animation, then return to inventory
            _equipment.UnequipToInventoryAnimated(slotType);
        }

        #endregion

        #region Private Methods

        private void UpdateSlotUI(EquipmentSlotUI slotUI, EquipmentSlotType slotType)
        {
            if (slotUI == null || _equipment == null) return;
            slotUI.UpdateSlot(_equipment.GetSlot(slotType));
        }

        private EquipmentSlotUI GetSlotUI(EquipmentSlotType type)
        {
            switch (type)
            {
                case EquipmentSlotType.PrimaryWeapon: return _primarySlotUI;
                case EquipmentSlotType.SecondaryWeapon: return _secondarySlotUI;
                case EquipmentSlotType.SuitAddon: return _suitAddonSlotUI;
                default: return null;
            }
        }

        #endregion
    }
}
