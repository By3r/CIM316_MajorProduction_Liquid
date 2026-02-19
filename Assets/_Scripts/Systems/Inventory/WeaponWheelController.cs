using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Weapon wheel controller — PARKED for Phase 3 reimplementation.
    /// Previously routed weapon selection to WeaponManager (now deleted).
    /// Will be reconnected to TacticalShooterPlayer's weapon system.
    /// </summary>
    public class WeaponWheelController : MonoBehaviour
    {
        #region Variables
        [Header("References")]
        [SerializeField] private WeaponWheelUI radialInventoryWheel;

        // TODO Phase 3: Replace WeaponManager reference with TacticalShooterPlayer weapon routing.
        // [SerializeField] private WeaponManager _weaponManager;

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

            // TODO Phase 3: Sync weapon order with TacticalShooterPlayer
        }

        private void HandleWheelClosedWithSelection(int index, InventoryItemData item)
        {
            if (item == null)
            {
                return;
            }

            SetEquippedVisual(item);

            // TODO Phase 3: Route weapon selection to TacticalShooterPlayer
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
