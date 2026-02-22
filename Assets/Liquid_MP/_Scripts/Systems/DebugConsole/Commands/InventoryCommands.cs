using System.Linq;
using System.Text;
using _Scripts.Systems.Inventory;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for inventory management and item spawning.
    /// </summary>
    public static class InventoryCommands
    {
        #region Item Lookup

        private static InventoryItemData[] GetAllItems()
        {
            return ItemDatabase.GetAll();
        }

        private static InventoryItemData FindItem(string query)
        {
            var items = GetAllItems();
            string lowerQuery = query.ToLower();

            // Exact match on itemId (use ItemDatabase for direct lookup first)
            var match = ItemDatabase.FindByItemId(query);
            if (match != null) return match;

            // Exact match on displayName
            match = items.FirstOrDefault(i => i.displayName != null && i.displayName.ToLower() == lowerQuery);
            if (match != null) return match;

            // Partial match on displayName
            match = items.FirstOrDefault(i => i.displayName != null && i.displayName.ToLower().Contains(lowerQuery));
            return match;
        }

        #endregion

        #region Commands

        [DebugCommand("list items", "Lists all available inventory items.", "list items")]
        public static string ListItems(string[] args)
        {
            var items = GetAllItems();

            if (items.Length == 0)
                return "<color=red>No InventoryItemData assets found.</color>";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Available Items ({items.Length}) ===");

            var sorted = items.OrderBy(i => i.displayName ?? i.itemId).ToList();
            foreach (var item in sorted)
            {
                string stackInfo = item.isStackable ? $" (stackable, max {item.maxStackSize})" : "";
                sb.AppendLine($"  {item.displayName ?? "???"} [{item.itemId}]{stackInfo}");
            }

            return sb.ToString();
        }

        [DebugCommand("give item", "Adds an item to the player's inventory.", "give item <name> [quantity]")]
        public static string GiveItem(string[] args)
        {
            if (args.Length == 0)
                return "Usage: give item <name> [quantity]";

            // Parse quantity from last arg if it's a number
            int quantity = 1;
            string[] nameArgs = args;

            if (args.Length >= 2 && int.TryParse(args[^1], out int parsedQty))
            {
                quantity = Mathf.Max(1, parsedQty);
                nameArgs = args.Take(args.Length - 1).ToArray();
            }

            string itemName = string.Join(" ", nameArgs);
            var itemData = FindItem(itemName);

            if (itemData == null)
                return $"<color=red>Item not found: '{itemName}'. Use 'list items' to see available items.</color>";

            if (PlayerInventory.Instance == null)
                return "<color=red>PlayerInventory not found.</color>";

            bool success = PlayerInventory.Instance.TryAddItem(itemData, quantity);

            if (success)
                return $"<color=green>Added {quantity}x {itemData.displayName} to inventory.</color>";
            else
                return $"<color=red>Could not add {quantity}x {itemData.displayName}. Inventory may be full.</color>";
        }

        [DebugCommand("spawn item", "Spawns an item's world prefab in front of the player.", "spawn item <name>")]
        public static string SpawnItem(string[] args)
        {
            if (args.Length == 0)
                return "Usage: spawn item <name>";

            string itemName = string.Join(" ", args);
            var itemData = FindItem(itemName);

            if (itemData == null)
                return $"<color=red>Item not found: '{itemName}'. Use 'list items' to see available items.</color>";

            if (itemData.worldPrefab == null)
                return $"<color=red>{itemData.displayName} has no world prefab assigned.</color>";

            // Find spawn position in front of the player
            Camera playerCamera = Camera.main;
            if (playerCamera == null)
                return "<color=red>No main camera found.</color>";

            Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward * 2f;
            var spawned = Object.Instantiate(itemData.worldPrefab, spawnPos, Quaternion.identity);

            // Apply a small forward force if it has a rigidbody
            Rigidbody rb = spawned.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(playerCamera.transform.forward * 2f, ForceMode.Impulse);
            }

            return $"<color=green>Spawned {itemData.displayName} in front of player.</color>";
        }

        #endregion
    }
}
