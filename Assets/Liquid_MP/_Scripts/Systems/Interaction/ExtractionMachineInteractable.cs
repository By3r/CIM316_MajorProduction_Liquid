/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate ExtractionMachineInteractable
 */
#if false

using UnityEngine;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Machines;

namespace _Scripts.Systems.Interaction
{
    /// <summary>
    /// Interactable wrapper for ExtractionMachine.
    /// </summary>
    [RequireComponent(typeof(ExtractionMachine))]
    public class ExtractionMachineInteractable : Interactable
    {
        private ExtractionMachine _extractionMachine;

        public override string InteractionPrompt => _extractionMachine != null ? _extractionMachine.InteractionPrompt : base.InteractionPrompt;

        private void Awake()
        {
            _extractionMachine = GetComponent<ExtractionMachine>();
        }

        public override bool CanInteract()
        {
            if (_extractionMachine == null) return false;

            // Cannot interact while extracting
            return _extractionMachine.CurrentState != ExtractionState.Extracting &&
                   _extractionMachine.CurrentState != ExtractionState.Depleted;
        }

        public override void Interact(GameObject interactor)
        {
            if (_extractionMachine == null) return;

            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = PlayerInventory.Instance;
            }

            if (inventory != null)
            {
                _extractionMachine.Interact(inventory);
                _onInteracted?.Invoke();
            }
        }
    }
}

#endif
