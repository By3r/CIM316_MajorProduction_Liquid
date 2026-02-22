/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate ElevatorInteractable
 */
#if false

using UnityEngine;
using _Scripts.Systems.Machines;

namespace _Scripts.Systems.Interaction
{
    /// <summary>
    /// Interactable wrapper for Elevator.
    /// </summary>
    [RequireComponent(typeof(Elevator))]
    public class ElevatorInteractable : Interactable
    {
        private Elevator _elevator;

        public override string InteractionPrompt => _elevator != null ? _elevator.InteractionPrompt : base.InteractionPrompt;

        private void Awake()
        {
            _elevator = GetComponent<Elevator>();
            _interactionRange = _elevator != null ? _elevator.InteractionRange : _interactionRange;
        }

        public override bool CanInteract()
        {
            return _elevator != null && _elevator.CanOperate;
        }

        public override void Interact(GameObject interactor)
        {
            if (_elevator == null) return;

            if (_elevator.TryUseElevator())
            {
                _onInteracted?.Invoke();
            }
        }
    }
}

#endif
