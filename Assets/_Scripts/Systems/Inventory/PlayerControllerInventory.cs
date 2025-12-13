using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    public class DebugInventoryController : MonoBehaviour
    {
        #region Variables
        [Header("References")]
        [SerializeField] private RadialInventoryWheel radialInventoryWheel;

        [Header("Debug Items")]
        [Tooltip("Items that will be added when pressing G")]
        [SerializeField] private List<InventoryItemData> debugItems = new List<InventoryItemData>();

        private readonly List<InventoryItemData> _currentItems = new List<InventoryItemData>();
        private int _debugItemIndex;
        #endregion

        private void Start()
        {
            RefreshWheel();
            Cursor.visible = false;
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

            if (_debugItemIndex >= debugItems.Count)
            {
                Debug.Log("Inventory is full or no more test items.");
                return;
            }

            InventoryItemData itemToAdd = debugItems[_debugItemIndex];
            _debugItemIndex++;

            _currentItems.Add(itemToAdd);

            Debug.Log($"Added item: {itemToAdd.displayName}");

            RefreshWheel();
        }

        private void RefreshWheel()
        {
            if (radialInventoryWheel != null)
            {
                radialInventoryWheel.SetItems(_currentItems);
            }
        }
    }
}