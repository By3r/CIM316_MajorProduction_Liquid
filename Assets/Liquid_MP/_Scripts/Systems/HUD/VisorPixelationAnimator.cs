using Liquid.Rendering;
using UnityEngine;

namespace _Scripts.Systems.HUD
{
    /// <summary>
    /// Animates the visor LCD pixelation effect when TAB opens/closes the panel.
    /// All visual settings live here — tweak everything from one Inspector.
    ///
    /// Drives <see cref="VisorPixelationFeature"/> static properties each frame.
    ///
    /// Resolution-independent: the percentage is tuned at a reference resolution
    /// (default 2160p / 4K) and automatically compensated at other resolutions
    /// using sub-linear (sqrt) scaling so the effect looks consistent everywhere.
    ///
    /// Sequence:
    ///   TAB open  → pixel count starts at screen height (invisible) → fades down to target (pixels appear)
    ///   TAB close → pixel count rises back toward screen height (pixels vanish) → off
    /// </summary>
    public class VisorPixelationAnimator : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Pixelation Target")]
        [Tooltip("Percentage of screen-height pixels when fully pixelated.\n" +
                 "Tuned at the reference resolution below, auto-compensated at others.\n" +
                 "Lower = blockier. 0.05 = heavy, 0.15 = medium, 0.50 = subtle.")]
        [SerializeField, Range(0.02f, 1f)] private float _resolvedPixelPercent = 0.15f;

        [Tooltip("The screen height (pixels) where you tuned the percentage above.\n" +
                 "At other resolutions, pixel count is sqrt-compensated so the\n" +
                 "effect looks consistent. 2160 = 4K, 1080 = Full HD.")]
        [SerializeField] private float _referenceHeight = 2160f;

        [Header("Boot-up (TAB Open)")]
        [Tooltip("How long (seconds) the pixelation takes to fully appear.\n" +
                 "Effect starts invisible and pixels gradually become visible.")]
        [SerializeField] private float _resolveTime = 1.5f;

        [Header("Shutdown (TAB Close)")]
        [Tooltip("How long (seconds) the pixelation takes to vanish.\n" +
                 "Pixels fade from visible back to invisible.")]
        [SerializeField] private float _shutdownTime = 0.3f;

        [Header("Chromatic Aberration (per-channel)")]
        [Tooltip("Red channel offset in virtual-pixel units.\n" +
                 "X = horizontal, Y = vertical. (0, 0) = no offset.")]
        [SerializeField] private Vector2 _chromaR = new Vector2(-0.15f, 0f);

        [Tooltip("Green channel offset in virtual-pixel units.\n" +
                 "Typically (0, 0) — green stays centered.")]
        [SerializeField] private Vector2 _chromaG = Vector2.zero;

        [Tooltip("Blue channel offset in virtual-pixel units.\n" +
                 "X = horizontal, Y = vertical. (0, 0) = no offset.")]
        [SerializeField] private Vector2 _chromaB = new Vector2(0.15f, 0f);

        [Header("Visor Light")]
        [Tooltip("GameObject to enable/disable with the visor (e.g. a point or spot light).")]
        [SerializeField] private GameObject _visorLight;

        [Header("Sub-pixel Shape")]
        [Tooltip("Dark gap width between sub-pixels and pixel rows.\n" +
                 "0 = no gaps, 0.1 = visible grid, 0.2 = wide grid.")]
        [SerializeField, Range(0f, 0.3f)] private float _gapSize = 0.1f;

        [Tooltip("Sub-pixel corner rounding.\n" +
                 "0 = sharp rectangles, 0.15 = slightly rounded, 0.5 = very rounded.")]
        [SerializeField, Range(0f, 1f)] private float _cornerRadius = 0.15f;

        [Tooltip("Brightness multiplier to compensate for sub-pixel filtering.\n" +
                 "3.0 = physically correct, higher = brighter.")]
        [SerializeField, Range(1f, 5f)] private float _brightness = 3.0f;

        #endregion

        #region Private Fields

        private enum State { Idle, Boot, Active, Shutdown }

        private State _state = State.Idle;

        /// <summary>
        /// Normalised animation progress.
        /// 0 = screen resolution (invisible — effect matches native pixels).
        /// 1 = target percentage (fully pixelated — pixels clearly visible).
        /// </summary>
        private float _progress;

        #endregion

        #region Unity Lifecycle

        private void OnDisable()
        {
            VisorPixelationFeature.IsActive = false;
            if (_visorLight != null) _visorLight.SetActive(false);
            _state = State.Idle;
        }

        private void Update()
        {
            var vc = VisorController.Instance;
            if (vc == null) return;

            bool isOpen = vc.IsPanelOpen;
            float dt = Time.unscaledDeltaTime;

            switch (_state)
            {
                case State.Idle:
                    if (isOpen)
                    {
                        _progress = 0f;                       // Start invisible.
                        VisorPixelationFeature.IsActive = true;
                        if (_visorLight != null) _visorLight.SetActive(true);
                        _state = State.Boot;
                    }
                    break;

                case State.Boot:
                    if (!isOpen) { _state = State.Shutdown; break; }

                    _progress = Mathf.MoveTowards(
                        _progress, 1f, dt / Mathf.Max(_resolveTime, 0.001f));

                    if (_progress >= 1f)
                    {
                        _progress = 1f;
                        _state = State.Active;
                    }
                    break;

                case State.Active:
                    if (!isOpen) _state = State.Shutdown;
                    break;

                case State.Shutdown:
                    if (isOpen) { _state = State.Boot; break; }   // Reopen — keep progress, reverse direction.

                    _progress = Mathf.MoveTowards(
                        _progress, 0f, dt / Mathf.Max(_shutdownTime, 0.001f));

                    if (_progress <= 0f)
                    {
                        VisorPixelationFeature.IsActive = false;
                        if (_visorLight != null) _visorLight.SetActive(false);
                        _state = State.Idle;
                    }
                    break;
            }

            if (_state != State.Idle)
                PushSettings();
        }

        #endregion

        #region Private Methods

        private void PushSettings()
        {
            float screenPixels = Screen.height;
            float refH = Mathf.Max(_referenceHeight, 480f);

            // Compute target at the reference resolution, then sqrt-compensate
            // for the current resolution. This keeps the effect visually consistent:
            // at lower resolutions the percentage is effectively raised so you get
            // more virtual pixels (less blockiness) while the pixel grid is still visible.
            float referenceTarget = refH * _resolvedPixelPercent;
            float scaleFactor = Mathf.Sqrt(screenPixels / refH);
            float targetPixels = referenceTarget * scaleFactor;

            // SmoothStep gives a nice ease-in-out curve.
            float t = Mathf.SmoothStep(0f, 1f, _progress);

            // Lerp from screen resolution (invisible) → compensated target (visible pixels).
            float pixelCount = Mathf.Lerp(screenPixels, targetPixels, t);

            VisorPixelationFeature.PixelCount  = Mathf.Max(pixelCount, 4f);
            VisorPixelationFeature.ChromaR      = _chromaR;
            VisorPixelationFeature.ChromaG      = _chromaG;
            VisorPixelationFeature.ChromaB      = _chromaB;
            VisorPixelationFeature.GapSize      = _gapSize;
            VisorPixelationFeature.CornerRadius = _cornerRadius;
            VisorPixelationFeature.Brightness   = _brightness;
        }

        #endregion
    }
}
