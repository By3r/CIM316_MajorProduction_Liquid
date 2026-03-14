using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Generic queued notification system for the inventory UI.
    /// Supports two modes:
    ///   - Simple: auto-dismiss after hold duration (e.g. "You equipped WK11 Viper").
    ///   - Task:   stays visible until CompleteTask() is called, then shows
    ///             strikethrough + shake before dismissing (e.g. tutorial objectives).
    ///
    /// Animation matches TerminalNotification: expand → flash → hold → shrink,
    /// with retro FPS simulation for a choppy CRT feel.
    ///
    /// Attach to any GameObject. Assign _panel and _messageText in the Inspector.
    /// The panel UI itself is created by the designer — this script only animates it.
    /// </summary>
    public class InventoryNotification : MonoBehaviour
    {
        #region Types

        private enum NotificationType { Simple, Task }

        private struct QueuedNotification
        {
            public string Message;
            public NotificationType Type;
        }

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _messageText;

        [Header("Timing")]
        [SerializeField] private float _expandDuration = 0.15f;
        [SerializeField] private float _holdDuration = 2f;
        [SerializeField] private float _shrinkDuration = 0.12f;

        [Header("Flash")]
        [Tooltip("Number of alpha pulses after expanding.")]
        [SerializeField] private int _flashCount = 3;
        [Tooltip("Lowest alpha during a flash pulse.")]
        [SerializeField] private float _flashMinAlpha = 0.3f;

        [Header("Task Completion")]
        [Tooltip("Duration of the shake effect after strikethrough.")]
        [SerializeField] private float _shakeDuration = 0.3f;
        [Tooltip("Max pixel offset during shake.")]
        [SerializeField] private float _shakeIntensity = 5f;
        [Tooltip("Pause after strikethrough so the player can read it before shrink.")]
        [SerializeField] private float _postStrikethroughDelay = 0.5f;

        [Header("Animation Curves")]
        [SerializeField] private AnimationCurve _expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Retro Feel")]
        [Tooltip("Simulated framerate for the animation. Lower = choppier.")]
        [SerializeField] private int _fpsSimulation = 15;

        #endregion

        #region Events

        /// <summary>
        /// Fired after a task notification finishes its completion animation
        /// (strikethrough + shake + shrink). Use this to advance the tutorial, etc.
        /// </summary>
        public event Action OnTaskCompleted;

        /// <summary>
        /// Fired after any notification (simple or task) finishes and is dismissed.
        /// </summary>
        public event Action OnNotificationDismissed;

        #endregion

        #region Private Fields

        private CanvasGroup _canvasGroup;
        private Coroutine _activeRoutine;
        private WaitForSecondsRealtime _frameWait;
        private readonly Queue<QueuedNotification> _queue = new();
        private bool _isShowing;
        private bool _isTaskActive;
        private bool _taskCompleted;

        #endregion

        #region Properties

        /// <summary>True when a notification is currently visible on screen.</summary>
        public bool IsShowing => _isShowing;

        /// <summary>True when the active notification is a task waiting for completion.</summary>
        public bool IsTaskActive => _isTaskActive;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Debug.Log($"[InventoryNotification] Awake() — _panel={_panel}, _messageText={_messageText}");

            if (_panel != null)
            {
                _canvasGroup = _panel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _panel.gameObject.AddComponent<CanvasGroup>();

                _panel.localScale = Vector3.zero;
                _canvasGroup.alpha = 0f;
                _panel.gameObject.SetActive(false);
                Debug.Log("[InventoryNotification] Panel initialized (scale=0, alpha=0, inactive).");
            }
            else
            {
                Debug.LogError("[InventoryNotification] _panel is NULL! Assign it in the Inspector.");
            }

            if (_messageText == null)
                Debug.LogError("[InventoryNotification] _messageText is NULL! Assign it in the Inspector.");

            _frameWait = new WaitForSecondsRealtime(1f / Mathf.Max(1, _fpsSimulation));
        }

        private void OnDisable()
        {
            Debug.Log("[InventoryNotification] OnDisable() called!");

            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            _isShowing = false;
            _isTaskActive = false;
            _taskCompleted = false;
            _queue.Clear();

            if (_panel != null)
            {
                _panel.localScale = Vector3.zero;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 0f;
                _panel.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Show a simple notification that auto-dismisses after the hold duration.
        /// If another notification is active, this one queues behind it.
        /// </summary>
        public void ShowNotification(string message)
        {
            _queue.Enqueue(new QueuedNotification
            {
                Message = message,
                Type = NotificationType.Simple
            });
            TryProcessNext();
        }

        /// <summary>
        /// Show a task notification that stays visible until CompleteTask() is called.
        /// On completion it shows strikethrough + shake before dismissing.
        /// If another notification is active, this one queues behind it.
        /// </summary>
        public void ShowTask(string message)
        {
            Debug.Log($"[InventoryNotification] ShowTask('{message}') — GO active={gameObject.activeSelf}, enabled={enabled}, _isShowing={_isShowing}, queueCount={_queue.Count}");
            _queue.Enqueue(new QueuedNotification
            {
                Message = message,
                Type = NotificationType.Task
            });
            TryProcessNext();
        }

        /// <summary>
        /// Marks the current task notification as completed.
        /// Triggers the strikethrough + shake + shrink animation.
        /// No-op if no task notification is currently active.
        /// </summary>
        public void CompleteTask()
        {
            if (!_isTaskActive)
            {
                Debug.LogWarning("[InventoryNotification] CompleteTask() called but no task is active.");
                return;
            }

            _taskCompleted = true;
        }

        #endregion

        #region Private Methods

        private void TryProcessNext()
        {
            Debug.Log($"[InventoryNotification] TryProcessNext() — _isShowing={_isShowing}, queueCount={_queue.Count}, GO active={gameObject.activeSelf}, enabled={enabled}");

            if (_isShowing || _queue.Count == 0)
            {
                Debug.Log($"[InventoryNotification] TryProcessNext() SKIPPED — _isShowing={_isShowing}, queueEmpty={_queue.Count == 0}");
                return;
            }

            QueuedNotification next = _queue.Dequeue();
            _isShowing = true;

            Debug.Log($"[InventoryNotification] Starting {next.Type} notification: '{next.Message}'");
            Debug.Log($"[InventoryNotification] _panel={_panel}, _panel active={(_panel != null ? _panel.gameObject.activeSelf.ToString() : "NULL")}, _messageText={_messageText}");

            _activeRoutine = next.Type switch
            {
                NotificationType.Task => StartCoroutine(AnimateTaskNotification(next.Message)),
                _ => StartCoroutine(AnimateSimpleNotification(next.Message))
            };
        }

        #endregion

        #region Coroutines

        private IEnumerator AnimateSimpleNotification(string message)
        {
            // Setup
            _messageText.text = message;
            _panel.gameObject.SetActive(true);

            // Expand
            yield return Expand();

            // Flash
            yield return Flash();

            // Hold
            yield return new WaitForSecondsRealtime(_holdDuration);

            // Shrink
            yield return Shrink();

            // Cleanup
            Cleanup();
            OnNotificationDismissed?.Invoke();
            TryProcessNext();
        }

        private IEnumerator AnimateTaskNotification(string message)
        {
            Debug.Log($"[InventoryNotification] AnimateTaskNotification STARTED: '{message}'");

            // Setup
            _isTaskActive = true;
            _taskCompleted = false;
            _messageText.text = message;
            _panel.gameObject.SetActive(true);

            Debug.Log($"[InventoryNotification] Panel activated. Scale={_panel.localScale}, Alpha={(_canvasGroup != null ? _canvasGroup.alpha : -1f)}, Panel active={_panel.gameObject.activeSelf}, Panel activeInHierarchy={_panel.gameObject.activeInHierarchy}");

            // Log parent hierarchy to check if anything above is inactive
            Transform t = _panel;
            string hierarchy = "";
            while (t != null)
            {
                hierarchy += $"  {t.name} (active={t.gameObject.activeSelf})\n";
                t = t.parent;
            }
            Debug.Log($"[InventoryNotification] Panel hierarchy:\n{hierarchy}");

            // Expand
            yield return Expand();
            Debug.Log("[InventoryNotification] Expand complete.");

            // Flash
            yield return Flash();
            Debug.Log("[InventoryNotification] Flash complete. Now waiting for CompleteTask()...");

            // Wait for CompleteTask() signal
            while (!_taskCompleted)
                yield return null;

            // Strikethrough
            _messageText.text = $"<s>{message}</s>";

            // Shake
            yield return Shake();

            // Brief pause so the player can read the struck-through text
            yield return new WaitForSecondsRealtime(_postStrikethroughDelay);

            // Shrink
            yield return Shrink();

            // Cleanup
            _isTaskActive = false;
            Cleanup();
            OnTaskCompleted?.Invoke();
            OnNotificationDismissed?.Invoke();
            TryProcessNext();
        }

        #endregion

        #region Animation Phases

        private IEnumerator Expand()
        {
            float step = 1f / Mathf.Max(1, _fpsSimulation);
            float t = 0f;

            while (t < _expandDuration)
            {
                t += step;
                float norm = Mathf.Clamp01(t / _expandDuration);
                float eval = _expandCurve.Evaluate(norm);
                _panel.localScale = Vector3.one * eval;
                _canvasGroup.alpha = eval;
                yield return _frameWait;
            }

            _panel.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
        }

        private IEnumerator Flash()
        {
            for (int i = 0; i < _flashCount; i++)
            {
                _canvasGroup.alpha = _flashMinAlpha;
                yield return _frameWait;
                yield return _frameWait;
                _canvasGroup.alpha = 1f;
                yield return _frameWait;
                yield return _frameWait;
            }
        }

        private IEnumerator Shrink()
        {
            float step = 1f / Mathf.Max(1, _fpsSimulation);
            float t = 0f;

            while (t < _shrinkDuration)
            {
                t += step;
                float norm = Mathf.Clamp01(t / _shrinkDuration);
                float eval = _shrinkCurve.Evaluate(norm);
                _panel.localScale = Vector3.one * eval;
                _canvasGroup.alpha = eval;
                yield return _frameWait;
            }
        }

        private IEnumerator Shake()
        {
            Vector2 originalPos = _panel.anchoredPosition;
            float step = 1f / Mathf.Max(1, _fpsSimulation);
            float t = 0f;

            while (t < _shakeDuration)
            {
                t += step;
                float offsetX = UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity);
                float offsetY = UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity);
                _panel.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
                yield return _frameWait;
            }

            _panel.anchoredPosition = originalPos;
        }

        private void Cleanup()
        {
            _panel.localScale = Vector3.zero;
            _canvasGroup.alpha = 0f;
            _panel.gameObject.SetActive(false);
            _activeRoutine = null;
            _isShowing = false;
        }

        #endregion
    }
}
