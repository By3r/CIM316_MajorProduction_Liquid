using System;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Manages the player's inventory: 3 physical item slots and AR grams tracking.
    /// AR grams represent deposited Aneutronic Rock extracted via Specialized Containers.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        #region Singleton

        private static PlayerInventory _instance;
        public static PlayerInventory Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerInventory>();
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        public event Action<int, InventorySlot> OnSlotChanged;
        public event Action<int> OnARGramsChanged;

        #endregion

        #region Serialized Fields

        [Header("Physical Item Slots")]
        [Tooltip("Number of physical item slots (default 3).")]
        [SerializeField] private int _slotCount = 3;

        [Header("AR Deposit")]
        [Tooltip("Maximum AR grams that can be deposited (upgradeable).")]
        [SerializeField] private int _arGramsCap = 100;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private InventorySlot[] _slots;
        private int _arGrams;

        #endregion

        #region Properties

        public int SlotCount => _slotCount;
        public int ARGramsCap => _arGramsCap;
        public int ARGrams => _arGrams;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitializeSlots();
        }

        private void InitializeSlots()
        {
            _slots = new InventorySlot[_slotCount];
            for (int i = 0; i < _slotCount; i++)
            {
                _slots[i] = new InventorySlot();
            }
        }

        #endregion

        #region Physical Item Slots

        /// <summary>
        /// Gets the slot at the specified index.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= _slotCount)
            {
                Debug.LogWarning($"[PlayerInventory] Invalid slot index: {index}");
                return null;
            }
            return _slots[index];
        }

        /// <summary>
        /// Attempts to add an item to the inventory.
        /// Returns true if successful.
        /// </summary>
        public bool TryAddItem(InventoryItemData itemData, int quantity = 1)
        {
            if (itemData == null || quantity <= 0) return false;

            // Try to stack with existing items first
            if (itemData.isStackable)
            {
                for (int i = 0; i < _slotCount; i++)
                {
                    if (_slots[i].ItemData == itemData && _slots[i].Quantity < itemData.maxStackSize)
                    {
                        int spaceLeft = itemData.maxStackSize - _slots[i].Quantity;
                        int toAdd = Mathf.Min(quantity, spaceLeft);
                        _slots[i].Quantity += toAdd;
                        quantity -= toAdd;

                        OnSlotChanged?.Invoke(i, _slots[i]);

                        if (_showDebugLogs)
                        {
                            Debug.Log($"[PlayerInventory] Stacked {toAdd} {itemData.displayName} in slot {i}");
                        }

                        if (quantity <= 0) return true;
                    }
                }
            }

            // Find empty slot for remaining items
            for (int i = 0; i < _slotCount; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int toAdd = itemData.isStackable ? Mathf.Min(quantity, itemData.maxStackSize) : 1;
                    _slots[i].ItemData = itemData;
                    _slots[i].Quantity = toAdd;
                    quantity -= toAdd;

                    OnSlotChanged?.Invoke(i, _slots[i]);

                    if (_showDebugLogs)
                    {
                        Debug.Log($"[PlayerInventory] Added {toAdd} {itemData.displayName} to slot {i}");
                    }

                    if (quantity <= 0 || !itemData.isStackable) return true;
                }
            }

            if (_showDebugLogs && quantity > 0)
            {
                Debug.Log($"[PlayerInventory] Inventory full. Could not add {quantity} {itemData.displayName}");
            }

            return quantity <= 0;
        }

        /// <summary>
        /// Removes an item from a specific slot.
        /// Returns the removed item data, or null if slot was empty.
        /// </summary>
        public InventoryItemData RemoveItemFromSlot(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount) return null;

            InventorySlot slot = _slots[slotIndex];
            if (slot.IsEmpty) return null;

            InventoryItemData removedItem = slot.ItemData;
            slot.Quantity -= quantity;

            if (slot.Quantity <= 0)
            {
                slot.Clear();
            }

            OnSlotChanged?.Invoke(slotIndex, slot);

            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerInventory] Removed {quantity} {removedItem.displayName} from slot {slotIndex}");
            }

            return removedItem;
        }

        /// <summary>
        /// Checks if inventory has room for the specified item.
        /// </summary>
        public bool HasRoomFor(InventoryItemData itemData, int quantity = 1)
        {
            if (itemData == null) return false;

            int remainingQuantity = quantity;

            // Check existing stacks
            if (itemData.isStackable)
            {
                for (int i = 0; i < _slotCount; i++)
                {
                    if (_slots[i].ItemData == itemData)
                    {
                        remainingQuantity -= (itemData.maxStackSize - _slots[i].Quantity);
                        if (remainingQuantity <= 0) return true;
                    }
                }
            }

            // Check empty slots
            for (int i = 0; i < _slotCount; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    remainingQuantity -= (itemData.isStackable ? itemData.maxStackSize : 1);
                    if (remainingQuantity <= 0) return true;
                }
            }

            return remainingQuantity <= 0;
        }

        #endregion

        #region AR Grams

        /// <summary>
        /// Adds AR grams (from depositing a filled Specialized Container).
        /// Returns the amount actually added.
        /// </summary>
        public int AddARGrams(int amount)
        {
            if (amount <= 0) return 0;

            int added = Mathf.Min(amount, _arGramsCap - _arGrams);
            _arGrams += added;

            if (added > 0)
            {
                OnARGramsChanged?.Invoke(_arGrams);

                if (_showDebugLogs)
                {
                    Debug.Log($"[PlayerInventory] Deposited {added}g AR. Total: {_arGrams}/{_arGramsCap}g");
                }
            }

            return added;
        }

        /// <summary>
        /// Removes AR grams.
        /// Returns true if successful.
        /// </summary>
        public bool RemoveARGrams(int amount)
        {
            if (amount <= 0) return true;
            if (_arGrams < amount) return false;

            _arGrams -= amount;
            OnARGramsChanged?.Invoke(_arGrams);

            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerInventory] Removed {amount}g AR. Total: {_arGrams}/{_arGramsCap}g");
            }

            return true;
        }

        /// <summary>
        /// Upgrades AR deposit capacity.
        /// </summary>
        public void UpgradeARGramsCap(int additionalCap)
        {
            _arGramsCap += additionalCap;

            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerInventory] Upgraded AR cap to {_arGramsCap}g");
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a single inventory slot that can hold a physical item.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public InventoryItemData ItemData;
        public int Quantity;

        public bool IsEmpty => ItemData == null || Quantity <= 0;

        public void Clear()
        {
            ItemData = null;
            Quantity = 0;
        }
    }
}
