using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// What kind of permanent suit enhancement this co-processor provides.
    /// </summary>
    public enum SuitModifierType
    {
        ExtraInventorySlots,    // Adds inventory capacity
        SilentMovement,         // Reduces footstep noise
        FastCrouch,             // Increases crouched movement speed
        // Add more as needed.
    }

    /// <summary>
    /// Item data for suit co-processors — one-time-use modular enhancements
    /// that permanently upgrade the player's suit. Used via the context menu.
    /// Non-stackable but replaceable (applying a new one of the same type overwrites the old).
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.SuitProcessor"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Suit Processor", fileName = "NewSuitProcessor")]
    public class SuitProcessorItemData : InventoryItemData
    {
        [Header("Suit Co-Processor Data")]
        [Tooltip("What aspect of the suit this processor enhances.")]
        public SuitModifierType modifierType;

        [Tooltip("The value of the enhancement (e.g. 3 extra slots, 0.5x noise multiplier).")]
        public float modifierValue;

        [Header("UI")]
        [Tooltip("Short description of the effect shown in the context menu confirmation.")]
        [TextArea]
        public string effectDescription;

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.SuitProcessor;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.SuitProcessor;
            base.OnValidate();

            if (isStackable)
            {
                isStackable = false;
                maxStackSize = 1;
            }
        }
#endif
    }
}
