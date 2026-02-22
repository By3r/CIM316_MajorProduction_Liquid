/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate PowerCellSlotInteractable
 */
#if false

using UnityEngine;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Machines;

namespace _Scripts.Systems.Interaction
{
    /// <summary>
    /// Interactable wrapper for PowerCellSlot.
    /// </summary>
    [RequireComponent(typeof(PowerCellSlot))]
    public class PowerCellSlotInteractable : Interactable
    {
        private PowerCellSlot _powerCellSlot;

        public override string InteractionPrompt => _powerCellSlot != null ? _powerCellSlot.CurrentPrompt : base.InteractionPrompt;

        private void Awake()
        {
            _powerCellSlot = GetComponent<PowerCellSlot>();
        }

        public override void Interact(GameObject interactor)
        {
            if (_powerCellSlot == null) return;

            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = PlayerInventory.Instance;
            }

            if (inventory != null)
            {
                _powerCellSlot.TogglePowerCell(inventory);
                _onInteracted?.Invoke();
            }
        }
    }
}

#endif
