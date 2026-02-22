/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate Interactable system
 */
#if false

using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Systems.Interaction
{
    /// <summary>
    /// Base class for all interactable objects in the game.
    /// Provides a unified interface for player interaction.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] protected float _interactionRange = 2.5f;
        [SerializeField] protected string _interactionPrompt = "Interact";
        [SerializeField] protected bool _requiresLookAt = true;

        [Header("Visual Feedback")]
        [SerializeField] protected GameObject _highlightEffect;
        [SerializeField] protected GameObject _promptUI;

        [Header("Events")]
        [SerializeField] protected UnityEvent _onInteracted;

        protected bool _isHighlighted = false;

        public float InteractionRange => _interactionRange;
        public virtual string InteractionPrompt => _interactionPrompt;
        public bool IsHighlighted => _isHighlighted;

        public virtual void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;

            if (_highlightEffect != null)
            {
                _highlightEffect.SetActive(highlighted);
            }
        }

        public virtual bool CanInteract()
        {
            return true;
        }

        public abstract void Interact(GameObject interactor);

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}

#endif
