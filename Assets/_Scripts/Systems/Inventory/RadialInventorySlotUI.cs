using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory
{
    public class RadialInventorySlotUI : MonoBehaviour
    {
        #region Variables
        [Header("Slot References")]
        [Tooltip("Root rect of this slot.")]
        [SerializeField] private RectTransform slotRectTransform;

        [Tooltip("Background image of the slot.")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Icon image to show the item sprite.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Label for the item name.")]
        [SerializeField] private TMP_Text label;

        [Header("Colors")]
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color highlightColour = Color.yellow;

        private InventoryItemData _itemData;

        public RectTransform SlotRectTransform => slotRectTransform;
        public InventoryItemData ItemData => _itemData;
        #endregion

        private void Reset()
        {
            slotRectTransform ??= GetComponent<RectTransform>();
        }

        public void SetItem(InventoryItemData item)
        {
            _itemData = item;

            if (iconImage != null)
            {
                if (item != null && item.icon != null)
                {
                    iconImage.sprite = item.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
            }

            if (label != null)
            {
                label.text = item != null ? item.displayName : string.Empty;
            }
        }

        public void SetHighlight(bool isHighlighted)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isHighlighted ? highlightColour : normalColour;
            }
        }
    }
}