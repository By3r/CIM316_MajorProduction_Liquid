using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// Item data for the COMS device — a left-hand communication/scanning tool.
    /// Equippable in the ComsDevice equipment slot. Toggle with key 3 to activate.
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.ComsDevice"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/COMS Device", fileName = "NewComsDevice")]
    public class ComsDeviceItemData : InventoryItemData
    {
        [Header("COMS Device")]
        [Tooltip("The runtime prefab instantiated when equipped. " +
                 "Must have a ComsDevice component with IK target references.")]
        public GameObject comsBehaviourPrefab;

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.ComsDevice;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.ComsDevice;
            base.OnValidate();

            if (isStackable)
            {
                isStackable = false;
                maxStackSize = 1;
            }
        }
#endif
    }
}
