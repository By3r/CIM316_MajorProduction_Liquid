using System;
using System.Collections.Generic;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.UI;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.HUD
{
    /// <summary>
    /// Trigger mode for each notification step.
    /// Manual: waits for CompleteCurrentStep() to be called externally (via UnityEvent).
    /// The rest auto subscribe to common gameplay events so you don't need bridges.
    /// </summary>
    public enum VisorStepTrigger
    {
        Manual,
        OnInventoryOpened,
        OnItemEquipped,
        OnInventoryClosed,
        OnComsActivated,
        OnSchematicUnlocked
    }

    [Serializable]
    public class VisorNotificationStep
    {
        [TextArea(2, 4)]
        public string message;

        [Tooltip("How this step gets completed.\n" +
                 "Manual = call CompleteCurrentStep() from a UnityEvent.\n" +
                 "Others auto subscribe to the matching gameplay event.")]
        public VisorStepTrigger trigger = VisorStepTrigger.Manual;

        [Tooltip("How many times CompleteCurrentStep() must be called before this step finishes.\n" +
                 "Use for steps where the player must pick up multiple items.\n" +
                 "Only applies to Manual trigger steps. 1 = single action (default).")]
        [Min(1)]
        public int requiredCount = 1;
    }

    /// <summary>
    /// Generic queued notification sequence for the visor HUD.
    /// Define an ordered list of steps in the Inspector, each with a message and trigger.
    /// Call Begin() to start. Each step shows a task notification, waits for its trigger,
    /// then completes (strikethrough + shake) and advances to the next.
    ///
    /// For Manual trigger steps, wire the source's UnityEvent to call CompleteCurrentStep().
    /// For auto trigger steps, the component subscribes only when that step is active.
    ///
    /// State check failsafes: when a step starts, it checks the current state
    /// (e.g. inventory already open) so it auto completes if the action already happened.
    ///
    /// When all steps finish, OnSequenceCompleted fires.
    /// </summary>
    public class VisorNotificationSequence : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private VisorNotification _notification;

        [Tooltip("Only needed if any step uses OnComsActivated trigger.")]
        [SerializeField] private ComsDeviceController _comsDeviceController;

        [Header("Steps")]
        [SerializeField] private List<VisorNotificationStep> _steps = new();

        #endregion

        #region Events

        /// <summary>Fires when every step in the sequence has been completed.</summary>
        public event Action OnSequenceCompleted;

        #endregion

        #region Private Fields

        private int _currentStepIndex = -1;
        private bool _active;
        private int _manualCompletionCount;
        private int _bankedManualCompletions;

        // Snapshot of schematic count at Begin() for detecting early unlocks.
        private int _schematicCountOnBegin;

        // True while a step is in its completion animation (between FinishCurrentStep and ShowNextStep).
        private bool _isTransitioning;

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            UnsubscribeFromCurrentStep();

            if (_notification != null)
                _notification.OnTaskCompleted -= HandleTaskAnimationDone;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the notification sequence from step 0.
        /// Call from a TutorialStep's onStepCompleted UnityEvent.
        /// </summary>
        public void Begin()
        {
            if (_active)
            {
                Debug.LogWarning("[VisorNotificationSequence] Begin() skipped, already active.");
                return;
            }

            if (_notification == null)
            {
                Debug.LogError("[VisorNotificationSequence] _notification is not assigned!");
                return;
            }

            if (_steps == null || _steps.Count == 0)
            {
                Debug.LogWarning("[VisorNotificationSequence] No steps defined.");
                return;
            }

            _active = true;
            _currentStepIndex = -1;
            _bankedManualCompletions = 0;
            _schematicCountOnBegin = SchematicRegistry.Instance != null ? SchematicRegistry.Instance.Count : 0;
            ShowNextStep();
        }

        /// <summary>
        /// Completes the current step externally.
        /// Use this for Manual trigger steps by wiring a UnityEvent to this method.
        /// If the current step isn't Manual or isn't ready yet, the completion is banked.
        /// </summary>
        public void CompleteCurrentStep()
        {
            if (!_active || _isTransitioning || _currentStepIndex < 0 || _currentStepIndex >= _steps.Count)
            {
                _bankedManualCompletions++;
                Debug.Log($"[VisorNotificationSequence] Banked manual completion (total: {_bankedManualCompletions})");
                return;
            }

            VisorNotificationStep step = _steps[_currentStepIndex];
            if (step.trigger != VisorStepTrigger.Manual)
            {
                _bankedManualCompletions++;
                Debug.Log($"[VisorNotificationSequence] Banked manual completion (total: {_bankedManualCompletions})");
                return;
            }

            _manualCompletionCount++;
            Debug.Log($"[VisorNotificationSequence] Manual completion {_manualCompletionCount}/{step.requiredCount} for step {_currentStepIndex}");

            if (_manualCompletionCount >= step.requiredCount)
                FinishCurrentStep();
        }

        #endregion

        #region Step Flow

        private void ShowNextStep()
        {
            _isTransitioning = false;
            _currentStepIndex++;

            if (_currentStepIndex >= _steps.Count)
            {
                Debug.Log("[VisorNotificationSequence] All steps completed.");
                _active = false;
                OnSequenceCompleted?.Invoke();
                return;
            }

            VisorNotificationStep step = _steps[_currentStepIndex];
            bool isSilentGate = string.IsNullOrEmpty(step.message);

            Debug.Log($"[VisorNotificationSequence] Step {_currentStepIndex}: '{step.message}' (trigger={step.trigger}, silent={isSilentGate})");

            _manualCompletionCount = 0;

            // Check if the action already happened before this step was shown.
            if (IsAlreadySatisfied(step))
            {
                Debug.Log($"[VisorNotificationSequence] Step {_currentStepIndex} already satisfied.");

                if (!isSilentGate)
                {
                    _notification.ShowTask(step.message);
                    _notification.CompleteTask();
                    _isTransitioning = true;
                    _notification.OnTaskCompleted += HandleTaskAnimationDone;
                }
                else
                {
                    ShowNextStep();
                }
                return;
            }

            // Show normally and subscribe to the trigger.
            if (!isSilentGate)
                _notification.ShowTask(step.message);

            SubscribeToCurrentStep();
        }

        private void FinishCurrentStep()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            UnsubscribeFromCurrentStep();

            VisorNotificationStep step = _steps[_currentStepIndex];
            bool isSilentGate = string.IsNullOrEmpty(step.message);

            Debug.Log($"[VisorNotificationSequence] Completing step {_currentStepIndex}: '{step.message}' (silent={isSilentGate})");

            if (isSilentGate)
            {
                ShowNextStep();
                return;
            }

            _isTransitioning = true;
            _notification.CompleteTask();
            _notification.OnTaskCompleted += HandleTaskAnimationDone;
        }

        private void HandleTaskAnimationDone()
        {
            _notification.OnTaskCompleted -= HandleTaskAnimationDone;
            ShowNextStep();
        }

        #endregion

        #region State Check Failsafes

        /// <summary>
        /// Checks if the step's condition is already met right now.
        /// This covers the case where the player did the action before the step was shown.
        /// </summary>
        private bool IsAlreadySatisfied(VisorNotificationStep step)
        {
            switch (step.trigger)
            {
                case VisorStepTrigger.OnInventoryOpened:
                    return InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;

                case VisorStepTrigger.OnInventoryClosed:
                    return InventoryUI.Instance != null && !InventoryUI.Instance.IsOpen;

                case VisorStepTrigger.OnComsActivated:
                    return _comsDeviceController != null && _comsDeviceController.IsActive;

                case VisorStepTrigger.OnSchematicUnlocked:
                    return SchematicRegistry.Instance != null &&
                           SchematicRegistry.Instance.Count > _schematicCountOnBegin;

                case VisorStepTrigger.Manual:
                    if (_bankedManualCompletions >= step.requiredCount)
                    {
                        _bankedManualCompletions -= step.requiredCount;
                        return true;
                    }
                    if (_bankedManualCompletions > 0)
                    {
                        _manualCompletionCount = _bankedManualCompletions;
                        _bankedManualCompletions = 0;
                    }
                    return false;

                default:
                    return false;
            }
        }

        #endregion

        #region Trigger Subscription

        private void SubscribeToCurrentStep()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            VisorNotificationStep step = _steps[_currentStepIndex];

            switch (step.trigger)
            {
                case VisorStepTrigger.OnInventoryOpened:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnInventoryOpened += HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnItemEquipped:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnItemEquipped += HandleItemEquipped;
                    break;

                case VisorStepTrigger.OnInventoryClosed:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnInventoryClosed += HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnComsActivated:
                    if (_comsDeviceController != null)
                        _comsDeviceController.OnComsActivated += HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnSchematicUnlocked:
                    if (SchematicRegistry.Instance != null)
                        SchematicRegistry.Instance.OnSchematicsChanged += HandleAutoTrigger;
                    break;
            }
        }

        private void UnsubscribeFromCurrentStep()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            VisorNotificationStep step = _steps[_currentStepIndex];

            switch (step.trigger)
            {
                case VisorStepTrigger.OnInventoryOpened:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnInventoryOpened -= HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnItemEquipped:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnItemEquipped -= HandleItemEquipped;
                    break;

                case VisorStepTrigger.OnInventoryClosed:
                    if (InventoryUI.Instance != null)
                        InventoryUI.Instance.OnInventoryClosed -= HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnComsActivated:
                    if (_comsDeviceController != null)
                        _comsDeviceController.OnComsActivated -= HandleAutoTrigger;
                    break;

                case VisorStepTrigger.OnSchematicUnlocked:
                    if (SchematicRegistry.Instance != null)
                        SchematicRegistry.Instance.OnSchematicsChanged -= HandleAutoTrigger;
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleAutoTrigger()
        {
            FinishCurrentStep();
        }

        private void HandleItemEquipped(InventoryItemData itemData)
        {
            Debug.Log($"[VisorNotificationSequence] Item equipped: '{itemData?.displayName}'");
            FinishCurrentStep();
        }

        #endregion
    }
}
