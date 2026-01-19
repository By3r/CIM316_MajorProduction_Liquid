using System;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Manages the player's inventory: 3 physical item slots and 3 ingredient counters.
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
        public event Action<IngredientType, int> OnIngredientChanged;
        public event Action<int> OnARGramsChanged;

        #endregion

        #region Serialized Fields

        [Header("Physical Item Slots")]
        [Tooltip("Number of physical item slots (default 3).")]
        [SerializeField] private int _slotCount = 3;

        [Header("Ingredient Limits")]
        [SerializeField] private int _ferriteCap = 20;
        [SerializeField] private int _polymerCap = 10;
        [SerializeField] private int _reagentCap = 10;

        [Header("AR Container")]
        [Tooltip("Maximum AR grams the player can carry (upgradeable).")]
        [SerializeField] private int _arGramsCap = 100;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private InventorySlot[] _slots;
        private int _ferriteCount;
        private int _polymerCount;
        private int _reagentCount;
        private int _arGrams;

        #endregion

        #region Properties

        public int SlotCount => _slotCount;
        public int FerriteCap => _ferriteCap;
        public int PolymerCap => _polymerCap;
        public int ReagentCap => _reagentCap;
        public int ARGramsCap => _arGramsCap;

        public int FerriteCount => _ferriteCount;
        public int PolymerCount => _polymerCount;
        public int ReagentCount => _reagentCount;
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

        #region Ingredients

        /// <summary>
        /// Gets the current count of a specific ingredient.
        /// </summary>
        public int GetIngredientCount(IngredientType type)
        {
            return type switch
            {
                IngredientType.Ferrite => _ferriteCount,
                IngredientType.Polymer => _polymerCount,
                IngredientType.Reagent => _reagentCount,
                _ => 0
            };
        }

        /// <summary>
        /// Gets the cap for a specific ingredient.
        /// </summary>
        public int GetIngredientCap(IngredientType type)
        {
            return type switch
            {
                IngredientType.Ferrite => _ferriteCap,
                IngredientType.Polymer => _polymerCap,
                IngredientType.Reagent => _reagentCap,
                _ => 0
            };
        }

        /// <summary>
        /// Attempts to add ingredients of a specific type.
        /// Returns the amount actually added.
        /// </summary>
        public int AddIngredient(IngredientType type, int amount)
        {
            if (amount <= 0) return 0;

            int added = 0;

            switch (type)
            {
                case IngredientType.Ferrite:
                    added = Mathf.Min(amount, _ferriteCap - _ferriteCount);
                    _ferriteCount += added;
                    break;
                case IngredientType.Polymer:
                    added = Mathf.Min(amount, _polymerCap - _polymerCount);
                    _polymerCount += added;
                    break;
                case IngredientType.Reagent:
                    added = Mathf.Min(amount, _reagentCap - _reagentCount);
                    _reagentCount += added;
                    break;
            }

            if (added > 0)
            {
                OnIngredientChanged?.Invoke(type, GetIngredientCount(type));

                if (_showDebugLogs)
                {
                    Debug.Log($"[PlayerInventory] Added {added} {type}. Total: {GetIngredientCount(type)}/{GetIngredientCap(type)}");
                }
            }

            return added;
        }

        /// <summary>
        /// Attempts to remove ingredients of a specific type.
        /// Returns true if successful (had enough).
        /// </summary>
        public bool RemoveIngredient(IngredientType type, int amount)
        {
            if (amount <= 0) return true;

            int current = GetIngredientCount(type);
            if (current < amount) return false;

            switch (type)
            {
                case IngredientType.Ferrite:
                    _ferriteCount -= amount;
                    break;
                case IngredientType.Polymer:
                    _polymerCount -= amount;
                    break;
                case IngredientType.Reagent:
                    _reagentCount -= amount;
                    break;
            }

            OnIngredientChanged?.Invoke(type, GetIngredientCount(type));

            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerInventory] Removed {amount} {type}. Total: {GetIngredientCount(type)}/{GetIngredientCap(type)}");
            }

            return true;
        }

        /// <summary>
        /// Checks if the player has at least the specified amount of an ingredient.
        /// </summary>
        public bool HasIngredient(IngredientType type, int amount)
        {
            return GetIngredientCount(type) >= amount;
        }

        #endregion

        #region AR Grams

        /// <summary>
        /// Adds AR grams to the player's container.
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
                    Debug.Log($"[PlayerInventory] Added {added}g AR. Total: {_arGrams}/{_arGramsCap}g");
                }
            }

            return added;
        }

        /// <summary>
        /// Removes AR grams from the player's container.
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

        #endregion

        #region Upgrades

        /// <summary>
        /// Upgrades ingredient capacity (e.g., from backpack upgrade).
        /// </summary>
        public void UpgradeIngredientCap(IngredientType type, int additionalCap)
        {
            switch (type)
            {
                case IngredientType.Ferrite:
                    _ferriteCap += additionalCap;
                    break;
                case IngredientType.Polymer:
                    _polymerCap += additionalCap;
                    break;
                case IngredientType.Reagent:
                    _reagentCap += additionalCap;
                    break;
            }

            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerInventory] Upgraded {type} cap to {GetIngredientCap(type)}");
            }
        }

        /// <summary>
        /// Upgrades AR container capacity.
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
