using System.Text.RegularExpressions;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Base data for all physical inventory items.
    /// Items with no extra fields (Miscellaneous, PowerCell, KeyItem) use this class directly.
    /// Items that need specialised data (weapons, schematics, containers, etc.) derive from this.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Basic Item", fileName = "NewItem")]
    public class InventoryItemData : ScriptableObject
    {
        [Header("Item Info")]
        [Tooltip("Unique identifier used for persistence. Auto-generates from displayName if left empty.")]
        public string itemId;
        public string displayName;

        [HideInInspector] public PhysicalItemType itemType;

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
        protected virtual void OnEnable()
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
        protected static string GenerateIdFromName(string displayName)
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
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarning($"[InventoryItemData] '{displayName}' is missing an itemId! Item persistence will NOT work without one. Auto-generating on play.");
            }
        }
#endif
    }
}