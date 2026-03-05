using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// Full-screen fade overlay. Creates its own Screen Space - Overlay canvas
    /// programmatically — no prefab or scene setup required.
    /// Persists across floor transitions via DontDestroyOnLoad.
    ///
    /// Usage: just call ScreenFade.Instance.FadeOut() / FadeIn() from anywhere.
    /// Do NOT place this manually in the scene — it auto-creates on first access.
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
                    var go = new GameObject("[ScreenFade]");
                    _instance = go.AddComponent<ScreenFade>();
                }
                return _instance;
            }
        }

        #endregion

        #region Private Fields

        private Image _fadeImage;
        private Coroutine _activeCoroutine;

        #endregion

        #region Properties

        /// <summary>Current alpha of the fade overlay (0 = clear, 1 = black).</summary>
        public float CurrentAlpha => _fadeImage != null ? _fadeImage.color.a : 0f;

        /// <summary>True while a fade animation is in progress.</summary>
        public bool IsFading => _activeCoroutine != null;

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

            // DontDestroyOnLoad only works on root objects
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Strip any Canvas Group — it overrides individual Image alpha
            // and would block the fade from working.
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) DestroyImmediate(cg);

            SetupFadeImage();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fades the screen to black over the given duration.
        /// Returns the coroutine so callers can yield on it.
        /// </summary>
        public Coroutine FadeOut(float duration = 0.5f)
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeCoroutine(1f, duration));
            return _activeCoroutine;
        }

        /// <summary>
        /// Fades the screen from black to clear over the given duration.
        /// Returns the coroutine so callers can yield on it.
        /// </summary>
        public Coroutine FadeIn(float duration = 0.5f)
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeCoroutine(0f, duration));
            return _activeCoroutine;
        }

        /// <summary>Immediately sets the screen to fully black.</summary>
        public void SetBlack()
        {
            CancelFade();
            if (_fadeImage != null)
                _fadeImage.color = Color.black;
        }

        /// <summary>Immediately clears the fade overlay.</summary>
        public void SetClear()
        {
            CancelFade();
            if (_fadeImage != null)
                _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Uses an existing Image on this object if present,
        /// otherwise creates a full canvas + image from scratch.
        /// </summary>
        private void SetupFadeImage()
        {
            // Check if someone placed an Image on this object already
            _fadeImage = GetComponentInChildren<Image>();

            if (_fadeImage != null)
            {
                // Use existing — just ensure correct starting state
                _fadeImage.color = new Color(0f, 0f, 0f, 0f);
                _fadeImage.raycastTarget = false;

                // Make sure there's an overlay canvas
                var existingCanvas = GetComponentInParent<Canvas>();
                if (existingCanvas == null)
                {
                    var canvas = gameObject.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 999;
                }
            }
            else
            {
                // Create everything from scratch
                CreateFadeCanvas();
            }
        }

        private void CreateFadeCanvas()
        {
            // Canvas — Screen Space Overlay, highest sort order
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            // Full-screen black Image — starts transparent
            var imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(transform, false);

            _fadeImage = imageGO.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = false;

            // Stretch to fill entire screen
            var rect = _fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CancelFade()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        private IEnumerator FadeCoroutine(float targetAlpha, float duration)
        {
            float startAlpha = _fadeImage.color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                _fadeImage.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(0f, 0f, 0f, targetAlpha);
            _activeCoroutine = null;
        }

        #endregion
    }
}
