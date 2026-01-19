using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Base data for physical inventory items that occupy slots.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Inventory/Item Data", fileName = "NewInventoryItem")]
    public class InventoryItemData : ScriptableObject
    {
        [Header("Item Info")]
        public string itemId;
        public string displayName;
        public PhysicalItemType itemType;

        [Header("Visuals")]
        public Sprite icon;
        public GameObject worldPrefab;

        [Header("Description")]
        [TextArea]
        public string description;

        [Header("Stacking")]
        [Tooltip("Can this item stack in a single slot?")]
        public bool isStackable = false;

        [Tooltip("Maximum stack size if stackable.")]
        public int maxStackSize = 1;
    }
}