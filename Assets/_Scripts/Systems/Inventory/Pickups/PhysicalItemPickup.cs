using UnityEngine;

namespace _Scripts.Systems.Inventory.Pickups
{
    /// <summary>
    /// Pickup for physical inventory items (PowerCells, Grenades, ARContainers, KeyItems).
    /// </summary>
    public class PhysicalItemPickup : Pickup
    {
        [Header("Item Data")]
        [SerializeField] private InventoryItemData _itemData;
        [SerializeField] private int _quantity = 1;

        public InventoryItemData ItemData => _itemData;
        public int Quantity => _quantity;

        public override bool TryPickup(PlayerInventory inventory)
        {
            if (_itemData == null || inventory == null) return false;

            if (inventory.TryAddItem(_itemData, _quantity))
            {
                Debug.Log($"[PhysicalItemPickup] Picked up {_quantity}x {_itemData.displayName}");
                OnPickupSuccess();
                return true;
            }

            Debug.Log($"[PhysicalItemPickup] Inventory full, cannot pick up {_itemData.displayName}");
            return false;
        }
    }
}
