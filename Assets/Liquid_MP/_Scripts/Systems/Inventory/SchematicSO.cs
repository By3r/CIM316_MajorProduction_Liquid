using System;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// A single ingredient entry in a crafting recipe.
    /// </summary>
    [Serializable]
    public struct SchematicIngredient
    {
        [Tooltip("The item required as an ingredient.")]
        public InventoryItemData item;

        [Tooltip("How many of this item are needed.")]
        [Min(1)]
        public int quantity;
    }

    /// <summary>
    /// Defines a crafting recipe (schematic) — what ingredients are consumed
    /// and what item is produced. Unlocked by the player via Schematic items.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Crafting/Schematic", fileName = "NewSchematic")]
    public class SchematicSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique schematic code shown in the terminal UI (e.g. SCHM-0041A).")]
        public string schematicId;

        [Tooltip("Category tag shown in the blueprint detail (e.g. EQUIPMENT, WEAPON).")]
        public string category;

        [Header("Recipe")]
        [Tooltip("Ingredients consumed when crafting.")]
        public SchematicIngredient[] ingredients;

        [Header("Output")]
        [Tooltip("The item produced by this recipe.")]
        public InventoryItemData outputItem;

        [Tooltip("How many of the output item are produced per craft.")]
        [Min(1)]
        public int outputQuantity = 1;
    }
}
