using UnityEngine;
using _Scripts.Systems.Inventory;

namespace _Scripts.Systems.Terminal
{
    /// <summary>
    /// Defines a single crafting recipe for the fabrication terminal.
    /// Lists input ingredients and the output item.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Crafting/Recipe", fileName = "NewRecipe")]
    public class CraftingRecipeSO : ScriptableObject
    {
        [Header("Recipe Info")]
        [Tooltip("Display name shown in the schematics list.")]
        public string recipeName;

        [Tooltip("Subtitle shown in the detail view.")]
        public string subtitle;

        [Header("Ingredients")]
        public Ingredient[] ingredients;

        [Header("Output")]
        public InventoryItemData outputItem;
        public int outputQuantity = 1;

        /// <summary>
        /// Checks whether the given inventory contains all required ingredients.
        /// </summary>
        public bool CanCraft(PlayerInventory inventory)
        {
            if (inventory == null || ingredients == null) return false;

            foreach (var ingredient in ingredients)
            {
                if (ingredient.item == null) continue;

                int count = inventory.CountItem(ingredient.item);
                if (count < ingredient.quantity)
                    return false;
            }

            return true;
        }

        [System.Serializable]
        public struct Ingredient
        {
            public InventoryItemData item;
            public int quantity;
        }
    }
}
