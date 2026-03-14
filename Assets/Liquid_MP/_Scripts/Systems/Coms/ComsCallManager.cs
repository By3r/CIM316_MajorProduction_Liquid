using _Scripts.Systems.Player;
using _Scripts.Tutorial;
using Liquid.Audio;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Liquid_MP._Scripts.Systems.Coms
{
    /// <summary>
    /// Call states for the COMS device.
    /// </summary>
    public enum ComsCallState
    {
        Idle,
        Ringing,
        InCall
    }

    /// <summary>
    /// Singleton that manages the COMS call state machine.
    /// Any system can trigger a call via TriggerCall(callData).
    /// The manager handles ringing, noise emission (enemy attraction),
    /// dialogue progression, and state transitions.
    ///
    /// ComsDeviceController subscribes to events to handle visuals/audio on the device.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ComsCallManager : MonoBehaviour
    {
        #region Singleton

        public static ComsCallManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        #endregion

        #region Serialized Fields

        [Header("Debug")]
        [Tooltip("All available call data assets for debug console discovery. " +
                 "Populate manually or use Resources.LoadAll at runtime.")]
        [SerializeField]
        private CallDataSO[] _callRegistry;

        [Header("Tutorial")]
        [SerializeField]
        private CallDataSO _tutorialCall;

        [SerializeField]
        private TutorialManager _tutorialManager;

        [Header("Noise")]
        [Tooltip("How often (seconds) the device emits noise while ringing after the grace period.")]
        [SerializeField]
        private float _noiseEmitInterval = 2f;

        #endregion

        #region Events

        /// <summary>Fired when an incoming call starts ringing. Passes the call data.</summary>
        public event Action<CallDataSO> OnCallRinging;

        /// <summary>Fired when the call is answered (player pulled out COMS).</summary>
        public event Action OnCallAnswered;

        /// <summary>Fired each time a new dialogue line begins during a call.</summary>
        public event Action<DialogueLine, int> OnDialogueLineChanged;

        /// <summary>Fired when the call ends (all lines finished or manual hangup).</summary>
        public event Action OnCallEnded;

        #endregion

        #region Properties

        /// <summary>Current call state.</summary>
        public ComsCallState CurrentState { get; private set; } = ComsCallState.Idle;

        /// <summary>The active call data (null when Idle).</summary>
        public CallDataSO CurrentCall { get; private set; }

        /// <summary>Current dialogue line index (-1 if not in dialogue).</summary>
        public int CurrentLineIndex { get; private set; } = -1;

        /// <summary>All registered call data assets (for debug console).</summary>
        public CallDataSO[] CallRegistry => _callRegistry;

        [field: SerializeField] public UnityEvent OnAnswerCall { get; private set; }
        #endregion

        #region Private Fields

        private Coroutine _dialogueCoroutine;
        private Coroutine _noiseCoroutine;

        #endregion

        #region Public API

        /// <summary>
        /// Triggers an incoming call. Transitions from Idle to Ringing.
        /// If already in a call or ringing, the new call is rejected.
        /// </summary>
        public void TriggerTutorialCall()
        {
            if (_tutorialCall == null)
            {
                Debug.LogWarning("[ComsCallManager] TriggerTutorialCall: no tutorial call asset assigned.");
                return;
            }
            TriggerCall(_tutorialCall);
        }

        public bool TriggerCall(CallDataSO callData)
        {
            if (callData == null)
            {
                Debug.LogWarning("[ComsCallManager] TriggerCall called with null data.");
                return false;
            }

            if (CurrentState != ComsCallState.Idle)
            {
                Debug.LogWarning($"[ComsCallManager] Cannot trigger call '{callData.callerName}' — " +
                                 $"already in state {CurrentState}.");
                return false;
            }

            CurrentCall = callData;
            CurrentState = ComsCallState.Ringing;
            CurrentLineIndex = -1;

            // Start noise emission after grace period
            _noiseCoroutine = StartCoroutine(NoiseEmissionCoroutine(callData.gracePeriod));

            Debug.Log($"[ComsCallManager] Incoming call from '{callData.callerName}'. Ringing...");
            OnCallRinging?.Invoke(callData);

            return true;
        }

        /// <summary>
        /// Answers the current ringing call. Transitions from Ringing to InCall.
        /// Starts dialogue progression.
        /// </summary>
        public void AnswerCall()
        {
            if (CurrentState != ComsCallState.Ringing)
            {
                Debug.LogWarning($"[ComsCallManager] Cannot answer — state is {CurrentState}, not Ringing.");
                return;
            }

            CurrentState = ComsCallState.InCall;

            // Stop noise emission (call answered, no more ringing noise)
            StopNoiseEmission();

            Debug.Log($"[ComsCallManager] Call answered from '{CurrentCall.callerName}'.");
            OnCallAnswered?.Invoke();

            if (_tutorialManager != null)
                _tutorialManager.CompleteCurrentStep();

            OnAnswerCall?.Invoke();

            if (CurrentCall.lines != null && CurrentCall.lines.Length > 0)
            {
                _dialogueCoroutine = StartCoroutine(DialogueCoroutine());
            }
            else
            {
                // No dialogue lines — end call immediately
                Debug.LogWarning("[ComsCallManager] Call has no dialogue lines. Ending immediately.");
                EndCall();
            }
        }

        /// <summary>
        /// Ends the current call (from any non-Idle state). Transitions to Idle.
        /// Can be called mid-dialogue to hang up early, or when ringing to reject.
        /// </summary>
        public void EndCall()
        {
            if (CurrentState == ComsCallState.Idle)
            {
                return;
            }

            string callerName = CurrentCall != null ? CurrentCall.callerName : "unknown";

            // Stop coroutines
            if (_dialogueCoroutine != null)
            {
                StopCoroutine(_dialogueCoroutine);
                _dialogueCoroutine = null;
            }

            StopNoiseEmission();

            CurrentState = ComsCallState.Idle;
            CurrentCall = null;
            CurrentLineIndex = -1;

            Debug.Log($"[ComsCallManager] Call ended (was from '{callerName}').");
            OnCallEnded?.Invoke();
        }

        #endregion

        #region Coroutines

        /// <summary>
        /// Advances through dialogue lines, waiting for each line's duration.
        /// Fires OnDialogueLineChanged for each line. Calls EndCall when finished.
        /// </summary>
        private IEnumerator DialogueCoroutine()
        {
            DialogueLine[] lines = CurrentCall.lines;

            for (int i = 0; i < lines.Length; i++)
            {
                // Guard: call may have been ended externally
                if (CurrentState != ComsCallState.InCall)
                    yield break;

                CurrentLineIndex = i;
                DialogueLine line = lines[i];

                OnDialogueLineChanged?.Invoke(line, i);

                float duration = line.Duration;
                if (duration <= 0f)
                    duration = 3f; // Safety fallback

                yield return new WaitForSeconds(duration);
            }

            // All lines finished — end call
            _dialogueCoroutine = null;
            EndCall();
        }

        /// <summary>
        /// After the grace period, emits noise at regular intervals to attract enemies.
        /// Noise comes from the player's position (the device is on the player).
        /// </summary>
        private IEnumerator NoiseEmissionCoroutine(float gracePeriod)
        {
            // Wait grace period before noise starts
            yield return new WaitForSeconds(gracePeriod);

            // Emit noise at intervals until stopped
            while (CurrentState == ComsCallState.Ringing)
            {
                EmitDeviceNoise();
                yield return new WaitForSeconds(_noiseEmitInterval);
            }
        }

        #endregion

        #region Private Helpers

        private void EmitDeviceNoise()
        {
            if (NoiseManager.Instance == null) return;

            // Noise comes from the player's position
            Vector3 noisePosition = transform.position;

            // Try to get player position for more accurate placement
            if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            {
                noisePosition = PlayerManager.Instance.CurrentPlayer.transform.position;
            }

            NoiseManager.Instance.EmitNoise(noisePosition, NoiseCategory.CommDevice);
        }

        private void StopNoiseEmission()
        {
            if (_noiseCoroutine != null)
            {
                StopCoroutine(_noiseCoroutine);
                _noiseCoroutine = null;
            }
        }

        #endregion
    }
}