using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// Item data for suit add-ons — equippable gear in the Suit Add-On slot
    /// that gives the player special abilities (e.g. Neutronic Boots for ceiling walking).
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.SuitAddon"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Suit Add-On", fileName = "NewSuitAddon")]
    public class SuitAddonItemData : InventoryItemData
    {
        [Header("Suit Add-On Data")]
        [Tooltip("The MonoBehaviour prefab (or component prefab) that provides the add-on's gameplay behaviour. " +
                 "Instantiated or enabled when equipped, disabled when unequipped.")]
        public GameObject addonBehaviourPrefab;

        [Header("Stat Modifiers")]
        [Tooltip("Movement speed multiplier while equipped (1.0 = no change).")]
        public float moveSpeedMultiplier = 1f;

        [Tooltip("Noise output multiplier while equipped (1.0 = no change, 0.5 = half noise).")]
        public float noiseMultiplier = 1f;

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.SuitAddon;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.SuitAddon;
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
