using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// What type of resource a container can hold.
    /// </summary>
    public enum ContainerContentType
    {
        AR,             // Aneutronic Rock
        Gasoline,       // Fuel for machines
        // Add more as needed.
    }

    /// <summary>
    /// Item data for containers that hold resources (AR, Gasoline, etc.).
    /// Can be emptied via the context menu.
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.Container"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Container Item", fileName = "NewContainerItem")]
    public class ContainerItemData : InventoryItemData
    {
        [Header("Container Data")]
        [Tooltip("What type of resource this container holds.")]
        public ContainerContentType contentType;

        [Tooltip("Maximum units this container can hold.")]
        public int capacity = 100;

        [Tooltip("If enabled, the container spawns with a random fill amount between the min and max values.")]
        public bool randomizeFill = false;

        [Tooltip("Minimum fill amount when randomized.")]
        [Min(0)]
        public int minFillAmount = 0;

        [Tooltip("Maximum fill amount when randomized (clamped to capacity at runtime).")]
        [Min(0)]
        public int maxFillAmount = 50;

        [Tooltip("Fixed fill amount used when randomizeFill is disabled.")]
        [Min(0)]
        public int defaultFillAmount = 0;

        /// <summary>
        /// Returns the starting fill amount — random if enabled, otherwise the fixed default.
        /// </summary>
        public int GetStartingFill()
        {
            if (randomizeFill)
                return Mathf.Clamp(Random.Range(minFillAmount, maxFillAmount + 1), 0, capacity);

            return Mathf.Clamp(defaultFillAmount, 0, capacity);
        }

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.Container;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.Container;
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
