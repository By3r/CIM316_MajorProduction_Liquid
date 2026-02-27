using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// Item data for schematics that unlock crafting recipes when used.
    /// Consumed on use — the referenced SchematicSO is added to the player's unlocked list.
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.Schematic"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Schematic Item", fileName = "NewSchematicItem")]
    public class SchematicItemData : InventoryItemData
    {
        [Header("Schematic Data")]
        [Tooltip("The crafting recipe this schematic unlocks when the player uses it.")]
        public SchematicSO schematicToUnlock;

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.Schematic;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.Schematic;
            base.OnValidate();
        }
#endif
    }
}
