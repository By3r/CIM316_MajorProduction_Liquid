using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Simple screen fade utility for transitions.
    /// Creates and manages a full-screen UI Image for fade effects.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        #region Singleton

        private static ScreenFade _instance;
        public static ScreenFade Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to find existing instance
                    _instance = FindObjectOfType<ScreenFade>();

                    // Create if not found
                    if (_instance == null)
                    {
                        GameObject fadeObj = new GameObject("ScreenFade");
                        _instance = fadeObj.AddComponent<ScreenFade>();
                        // Note: DontDestroyOnLoad and CreateFadeCanvas are called in Awake
                    }
                }

                // Ensure canvas is created (in case accessed before Awake ran)
                if (_instance != null && _instance._fadeImage == null)
                {
                    _instance.CreateFadeCanvas();
                }

                return _instance;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Fade Settings")]
        [SerializeField] private Color _fadeColor = Color.black;
        [SerializeField] private float _defaultFadeDuration = 0.5f;

        #endregion

        #region Private Fields

        private Canvas _fadeCanvas;
        private Image _fadeImage;
        private Coroutine _currentFade;

        #endregion

        #region Properties

        public bool IsFading => _currentFade != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadeCanvas();
        }

        #endregion

        #region Setup

        private void CreateFadeCanvas()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("FadeCanvas");
            canvasObj.transform.SetParent(transform);

            _fadeCanvas = canvasObj.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999; // Always on top

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create fade image
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(canvasObj.transform);

            _fadeImage = imageObj.AddComponent<Image>();
            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 0f);
            _fadeImage.raycastTarget = false;

            // Make it fill the screen
            RectTransform rect = _fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Start invisible
            _fadeImage.gameObject.SetActive(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fades the screen to the fade color (default black).
        /// </summary>
        public void FadeOut(float duration = -1f, Action onComplete = null)
        {
            if (duration < 0) duration = _defaultFadeDuration;
            StartFade(0f, 1f, duration, onComplete);
        }

        /// <summary>
        /// Fades the screen from the fade color back to clear.
        /// </summary>
        public void FadeIn(float duration = -1f, Action onComplete = null)
        {
            if (duration < 0) duration = _defaultFadeDuration;
            StartFade(1f, 0f, duration, onComplete);
        }

        /// <summary>
        /// Performs a full fade out then fade in sequence.
        /// Calls midAction at the peak of the fade (fully black).
        /// </summary>
        public void FadeOutIn(float fadeOutDuration = -1f, float fadeInDuration = -1f,
            Action midAction = null, Action onComplete = null)
        {
            if (fadeOutDuration < 0) fadeOutDuration = _defaultFadeDuration;
            if (fadeInDuration < 0) fadeInDuration = _defaultFadeDuration;

            if (_currentFade != null) StopCoroutine(_currentFade);
            _currentFade = StartCoroutine(FadeOutInCoroutine(fadeOutDuration, fadeInDuration, midAction, onComplete));
        }

        /// <summary>
        /// Immediately sets the screen to fully faded (black).
        /// </summary>
        public void SetFaded()
        {
            if (_currentFade != null) StopCoroutine(_currentFade);
            _fadeImage.gameObject.SetActive(true);
            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 1f);
        }

        /// <summary>
        /// Immediately clears the fade (transparent).
        /// </summary>
        public void SetClear()
        {
            if (_currentFade != null) StopCoroutine(_currentFade);
            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 0f);
            _fadeImage.gameObject.SetActive(false);
        }

        #endregion

        #region Private Methods

        private void StartFade(float startAlpha, float endAlpha, float duration, Action onComplete)
        {
            // Ensure canvas exists
            if (_fadeImage == null)
            {
                CreateFadeCanvas();
            }

            if (_currentFade != null) StopCoroutine(_currentFade);
            _currentFade = StartCoroutine(FadeCoroutine(startAlpha, endAlpha, duration, onComplete));
        }

        private IEnumerator FadeCoroutine(float startAlpha, float endAlpha, float duration, Action onComplete)
        {
            _fadeImage.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, endAlpha);

            // Hide image if fully transparent
            if (endAlpha <= 0f)
            {
                _fadeImage.gameObject.SetActive(false);
            }

            _currentFade = null;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutInCoroutine(float fadeOutDuration, float fadeInDuration,
            Action midAction, Action onComplete)
        {
            // Fade out
            _fadeImage.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                float alpha = Mathf.Lerp(0f, 1f, t);
                _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 1f);

            // Execute mid action
            midAction?.Invoke();

            // Small pause at peak
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade in
            elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                float alpha = Mathf.Lerp(1f, 0f, t);
                _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 0f);
            _fadeImage.gameObject.SetActive(false);

            _currentFade = null;
            onComplete?.Invoke();
        }

        #endregion
    }
}
