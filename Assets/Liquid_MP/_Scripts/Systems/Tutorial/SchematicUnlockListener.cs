using _Scripts.Systems.Inventory;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Bridges SchematicRegistry.OnSchematicsChanged (C# event) to a UnityEvent
    /// so it can be wired in the Inspector (e.g. to complete a TutorialVisorPrompt).
    /// </summary>
    public class SchematicUnlockListener : MonoBehaviour
    {
        [SerializeField] private UnityEvent _onSchematicUnlocked;

        private bool _subscribed;

        private void OnEnable()
        {
            if (SchematicRegistry.Instance != null && !_subscribed)
            {
                SchematicRegistry.Instance.OnSchematicsChanged += HandleSchematicsChanged;
                _subscribed = true;
            }
        }

        private void Start()
        {
            if (SchematicRegistry.Instance != null && !_subscribed)
            {
                SchematicRegistry.Instance.OnSchematicsChanged += HandleSchematicsChanged;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (SchematicRegistry.Instance != null && _subscribed)
            {
                SchematicRegistry.Instance.OnSchematicsChanged -= HandleSchematicsChanged;
                _subscribed = false;
            }
        }

        private void HandleSchematicsChanged()
        {
            _onSchematicUnlocked?.Invoke();
        }
    }
}
