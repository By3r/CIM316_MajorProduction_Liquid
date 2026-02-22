using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Static utility for looking up InventoryItemData assets by itemId.
    /// Centralizes the lookup pattern used by debug commands and persistence systems.
    /// </summary>
    public static class ItemDatabase
    {
        private static InventoryItemData[] _cache;

        /// <summary>
        /// Finds an InventoryItemData asset by its itemId field.
        /// </summary>
        /// <param name="itemId">The unique item identifier to search for.</param>
        /// <returns>The matching InventoryItemData, or null if not found.</returns>
        public static InventoryItemData FindByItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (_cache == null)
                RefreshCache();

            foreach (var item in _cache)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }

            return null;
        }

        /// <summary>
        /// Returns all loaded InventoryItemData assets.
        /// </summary>
        public static InventoryItemData[] GetAll()
        {
            if (_cache == null)
                RefreshCache();

            return _cache;
        }

        /// <summary>
        /// Forces a refresh of the item cache. Call when assets may have changed.
        /// </summary>
        public static void RefreshCache()
        {
            _cache = Resources.FindObjectsOfTypeAll<InventoryItemData>();
        }

        /// <summary>
        /// Clears the cache so it will be rebuilt on next access.
        /// </summary>
        public static void ClearCache()
        {
            _cache = null;
        }
    }
}
