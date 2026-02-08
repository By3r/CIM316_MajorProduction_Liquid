using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    public class WeaponWheelController : MonoBehaviour
    {
        #region Variables
        [Header("References")]
        [SerializeField] private WeaponWheelUI radialInventoryWheel;

        [Header("Debug Items")]
        [Tooltip("Items that will be added when pressing G")]
        [SerializeField] private List<InventoryItemData> debugItems = new List<InventoryItemData>();

        [Header("Equip Visuals")]
        [Tooltip("Scene objects to activate when an item is equipped. Only one will be active at a time.")]
        [SerializeField] private List<ItemEquipVisual> equipVisuals = new List<ItemEquipVisual>();

        [System.Serializable]
        public class ItemEquipVisual
        {
            public InventoryItemData itemData;
            public GameObject visualObject;
        }

        private readonly List<InventoryItemData> _currentItems = new List<InventoryItemData>();
        private int _debugItemIndex;
        #endregion

        private void OnEnable()
        {
            if (radialInventoryWheel != null)
            {
                radialInventoryWheel.OnWheelClosedWithSelection += HandleWheelClosedWithSelection;
            }
        }

        private void OnDisable()
        {
            if (radialInventoryWheel != null)
            {
                radialInventoryWheel.OnWheelClosedWithSelection -= HandleWheelClosedWithSelection;
            }
        }

        private void Start()
        {
            RefreshWheel();
            SetEquippedVisual(null);
        }

        private void Update()
        {
            HandleAddItemInput();
        }

        private void HandleAddItemInput()
        {
            if (!Input.GetKeyDown(KeyCode.G))
            {
                return;
            }

            if (debugItems == null || debugItems.Count == 0)
            {
                Debug.LogWarning("No debug items assigned.");
                return;
            }

            if (_debugItemIndex >= debugItems.Count) return;

            InventoryItemData itemToAdd = debugItems[_debugItemIndex];
            _debugItemIndex++;

            _currentItems.Add(itemToAdd);

            RefreshWheel();
        }

        private void RefreshWheel()
        {
            if (radialInventoryWheel != null)
            {
                radialInventoryWheel.SetItems(_currentItems);
            }
        }

        private void HandleWheelClosedWithSelection(int index, InventoryItemData item)
        {
            if (item == null)
            {
                return;
            }

            SetEquippedVisual(item);
        }

        private void SetEquippedVisual(InventoryItemData equippedItem)
        {
            for (int i = 0; i < equipVisuals.Count; i++)
            {
                ItemEquipVisual entry = equipVisuals[i];
                if (entry == null || entry.visualObject == null)
                {
                    continue;
                }

                bool shouldBeActive = (equippedItem != null && entry.itemData == equippedItem);
                entry.visualObject.SetActive(shouldBeActive);
            }
        }
    }
}