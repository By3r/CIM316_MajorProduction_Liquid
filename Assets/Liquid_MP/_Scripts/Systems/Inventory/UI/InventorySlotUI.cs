using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// UI component for a single physical item slot.
    /// Displays icon, quantity, and empty state.
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
        [SerializeField] private GameObject _emptyStateOverlay;

        [Header("Settings")]
        [SerializeField] private Sprite _emptySlotSprite;
        [SerializeField] private Color _emptySlotColor = new Color(1, 1, 1, 0.3f);
        [SerializeField] private Color _filledSlotColor = Color.white;

        #endregion

        #region Private Fields

        private InventorySlot _currentSlot;
        private int _slotIndex = -1;

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

        private void ShowEmpty()
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = _emptySlotSprite;
                _iconImage.color = _emptySlotColor;
            }

            if (_quantityText != null)
            {
                _quantityText.gameObject.SetActive(false);
            }

            if (_emptyStateOverlay != null)
            {
                _emptyStateOverlay.SetActive(true);
            }
        }

        private void ShowItem(InventoryItemData itemData, int quantity)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = itemData.icon != null ? itemData.icon : _emptySlotSprite;
                _iconImage.color = _filledSlotColor;
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

            if (_emptyStateOverlay != null)
            {
                _emptyStateOverlay.SetActive(false);
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
