using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    [CreateAssetMenu(menuName = "Liquid/Inventory/Item Data", fileName = "NewInventoryItem")]
    public class InventoryItemData : ScriptableObject
    {
        [Header("Item Info")]
        public string itemId;
        public string displayName;

        [Header("Visuals")]
        public Sprite icon;

        [TextArea]
        public string description;
    }
}