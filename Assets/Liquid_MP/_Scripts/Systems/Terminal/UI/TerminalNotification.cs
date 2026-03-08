using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Centered notification popup on the terminal canvas.
    /// Expands from center, flashes, holds, then shrinks away.
    /// IMPORTANT: _panel can be the same GO as this script — we activate before
    /// starting the coroutine and only deactivate as the very last step.
    /// </summary>
    public class TerminalNotification : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private RawImage _iconImage;

        [Header("3D Item Preview")]
        [SerializeField] private NotificationItemPreview _itemPreview;

        [Header("Timing")]
        [SerializeField] private float _expandDuration = 0.15f;
        [SerializeField] private float _holdDuration = 2f;
        [SerializeField] private float _shrinkDuration = 0.12f;

        [Header("Flash")]
        [Tooltip("Number of alpha pulses during the hold phase.")]
        [SerializeField] private int _flashCount = 3;
        [Tooltip("Duration of each flash pulse (fade out + fade in).")]
        [SerializeField] private float _flashPulseDuration = 0.15f;
        [Tooltip("Lowest alpha during a flash pulse.")]
        [SerializeField] private float _flashMinAlpha = 0.3f;

        [Header("Animation Curves")]
        [SerializeField] private AnimationCurve _expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [FormerlySerializedAs("_fps")]
        [FormerlySerializedAs("_retroFps")]
        [Header("Retro Feel")]
        [Tooltip("Simulated framerate for the animation. Lower = choppier.")]
        [SerializeField] private int _fpsSimulation = 15;

        private CanvasGroup _canvasGroup;
        private Coroutine _activeRoutine;
        private WaitForSecondsRealtime _frameWait;

        private void Awake()
        {
            if (_panel != null)
            {
                _canvasGroup = _panel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _panel.gameObject.AddComponent<CanvasGroup>();

                _panel.localScale = Vector3.zero;
                _canvasGroup.alpha = 0f;
                _panel.gameObject.SetActive(false);
            }

            _frameWait = new WaitForSecondsRealtime(1f / Mathf.Max(1, _fpsSimulation));
        }

        /// <summary>
        /// Show a notification with a message and a 3D item prefab rotating in the preview.
        /// </summary>
        public void Show(string message, GameObject worldPrefab)
        {
            if (_panel == null || _messageText == null) return;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _messageText.text = message;

            if (_itemPreview != null && worldPrefab != null)
            {
                _itemPreview.Show(worldPrefab);
                if (_iconImage != null)
                {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.texture = _itemPreview.RenderTexture;
                }
            }
            else if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(false);
            }

            // Activate BEFORE starting coroutine
            _panel.gameObject.SetActive(true);
            _activeRoutine = StartCoroutine(AnimateNotification());
        }

        /// <summary>
        /// Show a notification with a message and a static icon texture (no 3D preview).
        /// </summary>
        public void Show(string message, Texture icon = null)
        {
            if (_panel == null || _messageText == null) return;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _messageText.text = message;

            if (_itemPreview != null)
                _itemPreview.Hide();

            if (_iconImage != null)
            {
                bool hasIcon = icon != null;
                _iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                    _iconImage.texture = icon;
            }

            _panel.gameObject.SetActive(true);
            _activeRoutine = StartCoroutine(AnimateNotification());
        }

        /// <summary>
        /// Overload that accepts a Sprite (extracts its texture).
        /// </summary>
        public void Show(string message, Sprite icon)
        {
            Show(message, icon != null ? icon.texture : null);
        }

        private IEnumerator AnimateNotification()
        {
            float step = 1f / Mathf.Max(1, _fpsSimulation);

            // === EXPAND: scale 0→1, alpha 0→1 ===
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

            // === FLASH: hard on/off alpha pulses (retro blink) ===
            for (int i = 0; i < _flashCount; i++)
            {
                _canvasGroup.alpha = _flashMinAlpha;
                yield return _frameWait;
                yield return _frameWait;
                _canvasGroup.alpha = 1f;
                yield return _frameWait;
                yield return _frameWait;
            }

            // === HOLD: stay visible ===
            yield return new WaitForSecondsRealtime(_holdDuration);

            // === SHRINK: scale 1→0, alpha 1→0 ===
            t = 0f;
            while (t < _shrinkDuration)
            {
                t += step;
                float norm = Mathf.Clamp01(t / _shrinkDuration);
                float eval = _shrinkCurve.Evaluate(norm);
                _panel.localScale = Vector3.one * eval;
                _canvasGroup.alpha = eval;
                yield return _frameWait;
            }

            // Cleanup — deactivate as the LAST step
            _panel.localScale = Vector3.zero;
            _canvasGroup.alpha = 0f;
            if (_itemPreview != null)
                _itemPreview.Hide();
            _panel.gameObject.SetActive(false);
            _activeRoutine = null;
        }
    }
}
