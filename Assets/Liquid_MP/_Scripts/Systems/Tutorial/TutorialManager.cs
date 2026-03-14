using System;
using System.Collections.Generic;
using _Scripts.Core.Persistence;
using _Scripts.Core.SceneFlow;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using _Scripts.Systems.Player;
using _Scripts.Tutorial;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.UI;

namespace _Scripts.Tutorial
{
    [Serializable]
    public sealed class TutorialStep
    {
        [Tooltip("Human-readable label shown in the inspector (no runtime effect).")]
        public string label;

        [Tooltip("Narrative sequence to play when this step begins. Leave null for steps " +
                 "that only wait for a world action (e.g. 'pick up container').")]
        public TutorialNarrativeAsset narrative;

        [Tooltip("If true, this step always waits for CompleteCurrentStep() to be called")]
        public bool waitForExternalTrigger = false;

        [Tooltip("Fired when this step completes — before the next step begins. " +
                 "Use this to enable colliders, spawn enemies, play effects, etc.")]
        public UnityEvent onStepCompleted;
    }

    public sealed class TutorialManager : MonoBehaviour
    {
        #region Variables

        [Header("Presenter / UI")]
        [SerializeField] private TutorialPresenter presenter;
        [SerializeField] private TutorialUI tutorialUI;

        [Header("Player Lock")]
        [Tooltip("The player's MovementController. Disabled while a narrative with lockPlayerDuringPlayback is running.")]
        [SerializeField] private MovementController playerMovement;
        [Tooltip("The player's InteractionController. Disabled alongside movement during locked narratives.")]
        [SerializeField] private InteractionController playerInteraction;

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

        [Header("Tutorial Inventory")]
        [SerializeField] private bool openInventoryOnFirstPickup = false;

        private int _currentStepIndex = -1;
        private int _activeSaveSlot = -1;
        private bool _waitingForStepCompletion;
        private bool _tutorialEnded;
        private bool _playerLocked;

        public int CurrentStepIndex => _currentStepIndex;
        public bool IsTutorialEnded => _tutorialEnded;

        #endregion

        #region Unity Messages

        private void Awake()
        {

            _activeSaveSlot = ResolveActiveSaveSlot();

            if (presenter == null)
                presenter = GetComponent<TutorialPresenter>();

            if (tutorialUI == null)
                tutorialUI = GetComponent<TutorialUI>();

            presenter.OnSequenceEnded += HandleSequenceEnded;

            if (openInventoryOnFirstPickup && PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnSlotChanged += HandleFirstPickup;
        }

        private void Start()
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] No steps defined.");
                return;
            }

            StartCoroutine(BeginStepNextFrame(0));
        }

        private IEnumerator BeginStepNextFrame(int index)
        {
            yield return null;
            BeginStep(index);
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

            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnSlotChanged -= HandleFirstPickup;
        }

        #endregion

        #region Public API

        public void AdvanceNarrative()
        {
            if (presenter != null && presenter.IsRunning)
                presenter.Advance();
        }

        public void CompleteCurrentStep()
        {
            if (_tutorialEnded) return;
            if (_currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

            UnlockPlayer();

            TutorialStep step = steps[_currentStepIndex];

            if (step.waitForExternalTrigger && !_waitingForStepCompletion && !presenter.IsRunning)
            {
                RunStepNarrative(step);
                return;
            }

            if (presenter.IsRunning) presenter.ForceComplete();
            if (!_waitingForStepCompletion) FinishStep();
        }

        public void WireComsDeviceHologram()
        {
            ComsDevice device = FindFirstObjectByType<ComsDevice>();
            if (device == null) return;
            if (tutorialUI != null)
                tutorialUI.SetLieutenantRoot(device.HologramMount.gameObject);
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

        public void LockPlayer()
        {
            if (_playerLocked) return;
            _playerLocked = true;

            if (playerMovement != null) playerMovement.enabled = false;
            if (playerInteraction != null) playerInteraction.enabled = false;
        }

        public void UnlockPlayer()
        {
            if (!_playerLocked) return;
            _playerLocked = false;

            if (playerMovement != null) playerMovement.enabled = true;
            if (playerInteraction != null) playerInteraction.enabled = true;
        }

        public void EndTutorial()
        {
            if (_tutorialEnded) return;

            if (presenter.IsRunning)
                presenter.ForceComplete();

            UnlockPlayer();

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

        #region Step Flow

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

            if (step.waitForExternalTrigger)
                return;

            RunStepNarrative(step);
        }

        private void RunStepNarrative(TutorialStep step)
        {
            if (step.narrative == null)
                return;

            _waitingForStepCompletion = true;

            if (step.narrative.LockPlayerDuringPlayback)
                LockPlayer();

            presenter.Begin(step.narrative, _activeSaveSlot);
        }

        private void HandleSequenceEnded()
        {
            if (!_waitingForStepCompletion) return;

            _waitingForStepCompletion = false;
            UnlockPlayer();
            FinishStep();
        }

        private void HandleFirstPickup(int slotIndex, InventorySlot slot)
        {
            if (slot.IsEmpty) return;

            PlayerInventory.Instance.OnSlotChanged -= HandleFirstPickup;

            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OpenInventory();
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

        #region Helpers

        private void DebugSkipCurrentRequirement()
        {
            if (_tutorialEnded) return;

            Debug.Log($"[TutorialManager] DEBUG: Skipping step {_currentStepIndex} " +
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
        public static int ActiveSlot { get; private set; } = -1;

        public static void Set(int slotIndex) => ActiveSlot = slotIndex;
        public static void Clear() => ActiveSlot = -1;
    }
}