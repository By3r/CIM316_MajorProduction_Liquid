using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Generic trigger volume that exposes a UnityEvent in the inspector.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TutorialTrigger : MonoBehaviour
    {
        [Header("Filter")]
        [Tooltip("Only fire for GameObjects with this tag. Leave string empty to fire for any collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Tooltip("If true, the trigger fires only once and then disables itself.")]
        [SerializeField] private bool fireOnce = true;

        [Header("Callback")]
        [Tooltip("Drag any script here and select the method to call when triggered.")]
        [SerializeField] private UnityEvent OnTriggered;

        private bool _hasFired;

        private void OnTriggerEnter(Collider other)
        {
            if (_hasFired && fireOnce) return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            _hasFired = true;
            OnTriggered?.Invoke();

            if (fireOnce)
                enabled = false;
        }

        /// <summary>Resets the fired state so the trigger can fire again.</summary>
        public void Reset() => _hasFired = false;
    }
}