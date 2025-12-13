using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory
{
    public class RadialInventorySlotUI : MonoBehaviour
    {
        #region Variables
        [Header("Slot References")]
        [Tooltip("Root rect of this slot (usually the Segment RectTransform).")]
        [SerializeField] private RectTransform slotRectTransform;

        [Tooltip("Background image of the slot (the wedge Image).")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Icon image to show the item sprite.")]
        [SerializeField] private Image iconImage;

        [Header("Icon Upright")]
        [Tooltip("Assign a child RectTransform (example: Icon or Content) that should stay upright.")]
        [SerializeField] private RectTransform iconUprightRoot;

        [Tooltip("If true, the iconUprightRoot will stay upright even if the segment rotates.")]
        [SerializeField] private bool keepIconUpright = true;

        [Header("Visibility")]
        [Tooltip("If true, only the highlighted segment shows its icon.")]
        [SerializeField] private bool showIconOnlyWhenHighlighted = true;

        [Header("Colors")]
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color highlightColour = Color.yellow;

        [Header("Filled State Colour")]
        [Tooltip("If true, segments with an item use filledColour when not highlighted.")] // TODO: Replace with actual item text labels.
        [SerializeField] private bool useFilledColour = true;

        [Tooltip("Colour used when this segment has an item (and is not highlighted).")]
        [SerializeField] private Color filledColour = new Color(1f, 0.55f, 0f, 1f);

        private InventoryItemData _itemData;
        private bool _isHighlighted;

        public RectTransform SlotRectTransform => slotRectTransform;
        public InventoryItemData ItemData => _itemData;
        #endregion

        private void Reset()
        {
            slotRectTransform ??= GetComponent<RectTransform>();

            if (iconUprightRoot == null && iconImage != null)
            {
                iconUprightRoot = iconImage.rectTransform;
            }
        }

        private void LateUpdate()
        {
            if (!keepIconUpright || iconUprightRoot == null)
            {
                return;
            }

            RectTransform root = slotRectTransform != null ? slotRectTransform : transform as RectTransform;
            if (root == null)
            {
                return;
            }

            // Cancel out the segment rotation so the icon stays upright.
            iconUprightRoot.localRotation = Quaternion.Inverse(root.localRotation);
        }

        public void SetItem(InventoryItemData item)
        {
            _itemData = item;

            if (iconImage != null)
            {
                if (item != null && item.icon != null)
                {
                    iconImage.sprite = item.icon;
                }
                else
                {
                    iconImage.sprite = null;
                }
            }

            ApplyIconVisibility();
            ApplyBackgroundColour();
        }

        public void SetHighlight(bool isHighlighted)
        {
            _isHighlighted = isHighlighted;

            ApplyIconVisibility();
            ApplyBackgroundColour();
        }

        private void ApplyIconVisibility()
        {
            if (iconImage == null)
            {
                return;
            }

            bool hasIcon = _itemData != null && _itemData.icon != null;
            bool shouldShow = hasIcon && (!showIconOnlyWhenHighlighted || _isHighlighted);

            iconImage.enabled = shouldShow;
        }

        private void ApplyBackgroundColour()
        {
            if (backgroundImage == null)
            {
                return;
            }

            if (_isHighlighted)
            {
                backgroundImage.color = highlightColour;
                return;
            }

            bool hasItem = _itemData != null;
            if (useFilledColour && hasItem)
            {
                backgroundImage.color = filledColour;
            }
            else
            {
                backgroundImage.color = normalColour;
            }
        }
    }
}