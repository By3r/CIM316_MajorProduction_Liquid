using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// UI component for a single physical item slot.
    /// Displays icon and quantity. Empty slots show nothing.
    /// Handles right-click for context menu.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        #region Events

        /// <summary>
        /// Fired when the slot is right-clicked. Passes slot index and screen position.
        /// </summary>
        public event Action<int, Vector2> OnRightClicked;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;

        #endregion

        #region Private Fields

        private InventorySlot _currentSlot;
        private int _slotIndex = -1;
        private static readonly Color Transparent = new Color(1, 1, 1, 0f);

        #endregion

        #region Properties

        public InventorySlot CurrentSlot => _currentSlot;
        public int SlotIndex => _slotIndex;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the slot index for this UI element.
        /// </summary>
        public void SetSlotIndex(int index)
        {
            _slotIndex = index;
        }

        public void UpdateSlot(InventorySlot slot)
        {
            _currentSlot = slot;

            if (slot == null || slot.IsEmpty)
            {
                ShowEmpty();
            }
            else
            {
                ShowItem(slot.ItemData, slot.Quantity);
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

            if (_quantityText != null)
            {
                _quantityText.gameObject.SetActive(false);
            }
        }

        private void ShowItem(InventoryItemData itemData, int quantity)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = itemData.icon;
                _iconImage.color = Color.white;
            }

            if (_quantityText != null)
            {
                if (itemData.isStackable && quantity > 1)
                {
                    _quantityText.text = quantity.ToString();
                    _quantityText.gameObject.SetActive(true);
                }
                else
                {
                    _quantityText.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region IPointerClickHandler

        public void OnPointerClick(PointerEventData eventData)
        {
            // Only respond to right-click on non-empty slots
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_currentSlot != null && !_currentSlot.IsEmpty)
                {
                    OnRightClicked?.Invoke(_slotIndex, eventData.position);
                }
            }
        }

        #endregion
    }
}
