using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Liquid.Rendering;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Plays a "waking up" sequence at the start of the tutorial.
    /// Uses <see cref="WakeUpBlurFeature"/> post process shader to go from
    /// fully black to blurry to clear.
    /// UI (subtitles, dialogue) renders on top of the effect.
    /// </summary>
    public sealed class TutorialWakeUp : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Timing")]
        [Tooltip("Total duration of the wake up sequence.")]
        [SerializeField] private float _wakeDuration = 3f;

        [Tooltip("Normalized time (0 to 1) when the black tint finishes fading out. " +
                 "After this point, only blur remains.")]
        [SerializeField, Range(0.1f, 0.8f)] private float _blackFadeEnd = 0.4f;

        [Tooltip("Maximum blur radius in texels. Higher values create a wider blur.")]
        [SerializeField] private float _blurRadius = 8f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onWakeUpComplete;

        #endregion

        #region Private Fields

        private bool _hasStarted;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            WakeUpBlurFeature.IsActive = true;
            WakeUpBlurFeature.BlackAmount = 1f;
            WakeUpBlurFeature.BlurAmount = 1f;
            WakeUpBlurFeature.BlurRadius = _blurRadius;
        }

        private void OnDestroy()
        {
            WakeUpBlurFeature.IsActive = false;
            WakeUpBlurFeature.BlackAmount = 0f;
            WakeUpBlurFeature.BlurAmount = 0f;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the wake up effect. Call from TutorialManager OnStepCompleted.
        /// </summary>
        public void Begin()
        {
            if (_hasStarted) return;
            _hasStarted = true;

            StartCoroutine(WakeUpRoutine());
        }

        #endregion

        #region Private Methods

        private IEnumerator WakeUpRoutine()
        {
            float elapsed = 0f;

            while (elapsed < _wakeDuration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / _wakeDuration);
                UpdateShaderProperties(raw);
                yield return null;
            }

            WakeUpBlurFeature.BlackAmount = 0f;
            WakeUpBlurFeature.BlurAmount = 0f;
            WakeUpBlurFeature.IsActive = false;

            _onWakeUpComplete?.Invoke();
        }

        /// <summary>
        /// Phase 1 (0 to _blackFadeEnd): black fades out, blur stays full.
        /// Phase 2 (_blackFadeEnd to 1): blur fades out.
        /// </summary>
        private void UpdateShaderProperties(float normalizedTime)
        {
            if (normalizedTime <= _blackFadeEnd)
            {
                float blackT = normalizedTime / _blackFadeEnd;
                WakeUpBlurFeature.BlackAmount = 1f - blackT;
                WakeUpBlurFeature.BlurAmount = 1f;
            }
            else
            {
                WakeUpBlurFeature.BlackAmount = 0f;
                float blurT = (normalizedTime - _blackFadeEnd) / (1f - _blackFadeEnd);
                WakeUpBlurFeature.BlurAmount = 1f - blurT;
            }
        }

        #endregion
    }
}
