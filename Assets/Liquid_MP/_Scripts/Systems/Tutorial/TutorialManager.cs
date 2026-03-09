using System;
using System.Collections.Generic;
using _Scripts.Core.Persistence;
using _Scripts.Core.SceneFlow;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Ordered list entry for a single tutorial step.
    /// Each step plays an optional narrative sequence, then waits until
    /// CompleteCurrentStep() is called before moving on.
    /// </summary>
    [Serializable]
    public sealed class TutorialStep
    {
        public string label;

        [Tooltip("Narrative sequence to play when this step begins. Leave null for steps " +
                 "that only wait for a world action.")]
        public TutorialNarrativeAsset narrative;

        [Tooltip("Fired when this step completes.")]
        public UnityEvent onStepCompleted;
    }

    /// <summary>
    /// The scene-level brain for the tutorial.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        #region Variables

        [Header("Presenter / UI")]
        [SerializeField] private TutorialPresenter presenter;
        [SerializeField] private TutorialUI tutorialUI;

        [Header("Steps")]
        [SerializeField] private List<TutorialStep> steps = new();

        [Header("Lurker")]
        [Tooltip("The Lurker enemy GameObject placed in the tutorial scene. " +
                 "Keep it inactive at start. ForceSpawnLurker() will activate it.")]
        [SerializeField] private GameObject lurkerObject;

        [Header("End Tutorial")]
        [Tooltip("Scene to load when the tutorial is complete. " +
                 "If SceneTransitionManager is present it will be used instead.")]
        [SerializeField] private string nextSceneName = "Game";

        [Tooltip("Fired just before the scene transition — use this to play a final " +
                 "cutscene, save the completion flag, etc.")]
        [SerializeField] private UnityEvent onTutorialEnded;

        [Header("Debug")]
        [Tooltip("Press L in Play mode to immediately complete the current step's requirement.")]
        [SerializeField] private bool enableDebugSkip = true;

        private int _currentStepIndex = -1;
        private int _activeSaveSlot = -1;
        private bool _waitingForStepCompletion;
        private bool _tutorialEnded;

        public int CurrentStepIndex => _currentStepIndex;
        public bool IsTutorialEnded => _tutorialEnded;

        #endregion

        private void Awake()
        {
            _activeSaveSlot = ResolveActiveSaveSlot();

            if (presenter == null)
                presenter = GetComponent<TutorialPresenter>();

            if (tutorialUI == null)
                tutorialUI = GetComponent<TutorialUI>();

            presenter.OnSequenceEnded += HandleSequenceEnded;
        }

        private void Start()
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] No steps defined.");
                return;
            }

            BeginStep(0);
        }

        private void Update()
        {
            if (!enableDebugSkip) return;

            if (Input.GetKeyDown(KeyCode.L))
                DebugSkipCurrentRequirement();
        }

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.OnSequenceEnded -= HandleSequenceEnded;
        }


        #region Public Functions.
        public void CompleteCurrentStep()
        {
            if (_tutorialEnded) return;
            if (_currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

            if (presenter.IsRunning)
                presenter.ForceComplete();

            if (!_waitingForStepCompletion)
                FinishStep();
        }

        public void ForceSpawnLurker()
        {
            if (lurkerObject == null)
            {
                Debug.LogWarning("[TutorialManager] ForceSpawnLurker called but lurkerObject is not assigned.");
                return;
            }

            lurkerObject.SetActive(true);
        }

        public void EndTutorial()
        {
            if (_tutorialEnded) return;

            if (presenter.IsRunning)
                presenter.ForceComplete();

            _tutorialEnded = true;

            if (_activeSaveSlot >= 0)
            {
                GameSaveData save = SaveSystem.LoadGame(_activeSaveSlot);
                if (save != null)
                {
                    save.HasCompletedTutorial = true;
                    save.CurrentStoryStage = StoryStage.MainGame;
                    SaveSystem.SaveGame(save, _activeSaveSlot);
                }
            }

            onTutorialEnded?.Invoke();

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadGameScene();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }

        #endregion

        #region Step Flow.
        private void BeginStep(int index)
        {
            if (index >= steps.Count)
            {
                EndTutorial();
                return;
            }

            _currentStepIndex = index;
            _waitingForStepCompletion = false;

            TutorialStep step = steps[index];

            if (step.narrative != null)
            {
                _waitingForStepCompletion = true;
                presenter.Begin(step.narrative, _activeSaveSlot);
            }
            else
            {
                _waitingForStepCompletion = false;
            }
        }

        private void HandleSequenceEnded()
        {
            if (!_waitingForStepCompletion) return;

            _waitingForStepCompletion = false;

            TutorialStep step = steps[_currentStepIndex];
            bool hasWorldRequirement = step.onStepCompleted.GetPersistentEventCount() > 0;

            if (!hasWorldRequirement)
                FinishStep();
        }

        private void FinishStep()
        {
            TutorialStep step = steps[_currentStepIndex];
            step.onStepCompleted?.Invoke();

            int next = _currentStepIndex + 1;
            if (next >= steps.Count)
                EndTutorial();
            else
                BeginStep(next);
        }
        #endregion

        #region Helpers.
        private void DebugSkipCurrentRequirement()
        {
            if (_tutorialEnded) return;

            Debug.Log($"[TutorialManager] Skipping step {_currentStepIndex} " +
                      $"({(steps.Count > _currentStepIndex && _currentStepIndex >= 0 ? steps[_currentStepIndex].label : "?")})");

            CompleteCurrentStep();
        }

        private static int ResolveActiveSaveSlot()
        {
            int slot = ActiveSaveSlotBridge.ActiveSlot;
            if (slot >= 0) return slot;

            return SaveSystem.GetMostRecentSaveSlotIndex();
        }
        #endregion
    }

    public static class ActiveSaveSlotBridge
    {
        /// <summary>-1 means not set yet.</summary>
        public static int ActiveSlot { get; private set; } = -1;

        public static void Set(int slotIndex) => ActiveSlot = slotIndex;
        public static void Clear() => ActiveSlot = -1;
    }
}