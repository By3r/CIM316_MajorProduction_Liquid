using UnityEngine;

namespace _Scripts.Systems.Inventory.Pickups
{
    /// <summary>
    /// Pickup for ingredient materials (Ferrite, Polymer, Reagent).
    /// These go directly into ingredient counters, not physical slots.
    /// </summary>
    public class IngredientPickup : Pickup
    {
        [Header("Ingredient Data")]
        [SerializeField] private IngredientType _ingredientType;
        [SerializeField] private int _minAmount = 1;
        [SerializeField] private int _maxAmount = 1;

        private int _amount;

        public IngredientType IngredientType => _ingredientType;
        public int Amount => _amount;

        protected override void Awake()
        {
            base.Awake();
            _amount = Random.Range(_minAmount, _maxAmount + 1);
        }

        public override bool TryPickup(PlayerInventory inventory)
        {
            if (inventory == null) return false;

            int added = inventory.AddIngredient(_ingredientType, _amount);

            if (added > 0)
            {
                // If we couldn't add all, keep remaining
                if (added < _amount)
                {
                    _amount -= added;
                    return false;
                }

                OnPickupSuccess();
                return true;
            }

            return false;
        }
    }
}
