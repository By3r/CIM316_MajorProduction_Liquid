using System.Text.RegularExpressions;
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
        [Tooltip("Unique identifier used for persistence. Auto-generates from displayName if left empty.")]
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

        /// <summary>
        /// Failsafe: auto-generates itemId from displayName if left empty.
        /// Called on asset load to ensure every item always has an ID for persistence.
        /// </summary>
        private void OnEnable()
        {
            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName))
            {
                itemId = GenerateIdFromName(displayName);
                Debug.LogWarning($"[InventoryItemData] '{displayName}' had no itemId — auto-generated: '{itemId}'. Set it manually in the Inspector to avoid this warning.");
            }
        }

        /// <summary>
        /// Converts a display name to a snake_case ID.
        /// "Power Cell" → "power_cell", "AGEU Supply Crate" → "ageu_supply_crate"
        /// </summary>
        private static string GenerateIdFromName(string displayName)
        {
            string id = displayName.Trim().ToLowerInvariant();
            id = Regex.Replace(id, @"[^a-z0-9]+", "_");
            id = id.Trim('_');
            return id;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor validation: warns if itemId is empty when the asset is modified.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarning($"[InventoryItemData] '{displayName}' is missing an itemId! Item persistence will NOT work without one. Auto-generating on play.");
            }
        }
#endif
    }
}