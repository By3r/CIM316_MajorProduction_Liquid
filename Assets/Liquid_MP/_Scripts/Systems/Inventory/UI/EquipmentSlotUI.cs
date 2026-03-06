using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// UI component for a single equipment slot on the hotbar (Primary, Secondary, or Suit Addon).
    /// Displays the equipped item's icon. Right-click fires an event for unequip.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
    {
        #region Events

        /// <summary>
        /// Fired when this equipment slot is right-clicked.
        /// Passes the slot type and screen position.
        /// </summary>
        public event Action<EquipmentSlotType, Vector2> OnRightClicked;

        #endregion

        #region Serialized Fields

        [Header("Slot Identity")]
        [SerializeField] private EquipmentSlotType _slotType;

        [Header("UI References")]
        [Tooltip("Image that displays the equipped item's icon.")]
        [SerializeField] private Image _iconImage;

        #endregion

        #region Private Fields

        private static readonly Color Transparent = new Color(1, 1, 1, 0f);

        #endregion

        #region Properties

        public EquipmentSlotType SlotType => _slotType;

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the slot visuals from the current equipment slot data.
        /// </summary>
        public void UpdateSlot(EquipmentSlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                ShowEmpty();
            }
            else
            {
                ShowItem(slot.ItemData);
            }
        }

        #endregion

        #region Private Methods

        private void ShowEmpty()
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.color = Transparent;
            }
        }

        private void ShowItem(InventoryItemData itemData)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = itemData.icon;
                _iconImage.color = Color.white;
            }
        }

        #endregion

        #region IPointerClickHandler

        public void OnPointerClick(PointerEventData eventData)
        {
            // Right-click opens unequip context menu
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClicked?.Invoke(_slotType, eventData.position);
            }
        }

        #endregion
    }
}
