using System;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Represents a single equipment slot on the player's hotbar.
    /// Holds the item data and a runtime reference to the instantiated
    /// GameObject (Kinemation weapon or suit addon behaviour).
    /// </summary>
    [Serializable]
    public class EquipmentSlot
    {
        public EquipmentSlotType SlotType;
        public InventoryItemData ItemData;

        /// <summary>
        /// The instantiated runtime GameObject (weapon prefab instance or addon behaviour).
        /// Not serialized — rebuilt on restore.
        /// </summary>
        [NonSerialized] public GameObject RuntimeInstance;

        public bool IsEmpty => ItemData == null;

        public void Clear()
        {
            ItemData = null;
            RuntimeInstance = null;
        }
    }
}
