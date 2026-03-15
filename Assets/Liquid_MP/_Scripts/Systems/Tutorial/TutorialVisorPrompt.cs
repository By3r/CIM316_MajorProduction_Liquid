using _Scripts.Systems.HUD;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Simple, reusable component for single message visor task prompts.
    /// Place one instance per notification in the scene.
    /// Wire Show() and Complete() from UnityEvents in the Inspector.
    /// Chain prompts via OnPromptCompleted.
    /// </summary>
    public class TutorialVisorPrompt : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisorNotification _notification;

        [Header("Message")]
        [SerializeField, TextArea(2, 4)]
        private string _message;

        [Header("Events")]
        [Tooltip("Fired after the completion animation finishes (strikethrough + shake + shrink). " +
                 "Wire this to the next prompt's Show(), TutorialManager.CompleteCurrentStep(), etc.")]
        [SerializeField] private UnityEvent _onPromptCompleted;

        private bool _isActive;

        /// <summary>
        /// Shows the task notification on the visor.
        /// Call from UnityEvents (TutorialStep.onStepCompleted, trigger volumes, etc.).
        /// </summary>
        public void Show()
        {
            if (_notification == null)
            {
                Debug.LogError($"[TutorialVisorPrompt] '{gameObject.name}': _notification is not assigned.");
                return;
            }

            if (_isActive)
            {
                Debug.LogWarning($"[TutorialVisorPrompt] '{gameObject.name}': Show() called while already active.");
                return;
            }

            _isActive = true;
            _notification.ShowTask(_message);
        }

        /// <summary>
        /// Marks the current task as completed (triggers strikethrough + shake).
        /// Call from UnityEvents (pickup events, trigger volumes, PowerCellSlot.onPowerInsert, etc.).
        /// </summary>
        public void Complete()
        {
            if (!_isActive)
            {
                Debug.LogWarning($"[TutorialVisorPrompt] '{gameObject.name}': Complete() called but prompt is not active.");
                return;
            }

            _notification.CompleteTask();
            _notification.OnTaskCompleted += HandleTaskCompleted;
        }

        private void HandleTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleTaskCompleted;
            _isActive = false;
            _onPromptCompleted?.Invoke();
        }

        private void OnDestroy()
        {
            if (_notification != null)
                _notification.OnTaskCompleted -= HandleTaskCompleted;
        }
    }
}
