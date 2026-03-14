using Liquid.Rendering;
using UnityEngine;

namespace _Scripts.Systems.HUD
{
    /// <summary>
    /// Animates the visor LCD pixelation effect when TAB opens/closes the panel.
    /// All visual settings live here. Tweak everything from one Inspector.
    ///
    /// Drives <see cref="VisorPixelationFeature"/> static properties each frame.
    ///
    /// Resolution adaptive: virtual pixel counts are set per resolution breakpoint
    /// (1080p, 1440p, 2160p) and linearly interpolated between them.
    ///
    /// Sequence:
    ///   TAB open  : pixel count starts at screen height (invisible), fades down to target (pixels appear)
    ///   TAB close : pixel count rises back toward screen height (pixels vanish), then off
    /// </summary>
    public class VisorPixelationAnimator : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Pixelation: Per Resolution Targets")]
        [Tooltip("Virtual pixel count at 1080p (1920x1080).\n" +
                 "Lower = blockier. Tune until text is readable at 1080p.")]
        [SerializeField] private float _pixels1080p = 200f;

        [Tooltip("Virtual pixel count at 1440p / 2K (2560x1440).\n" +
                 "Tune until text is readable at 1440p.")]
        [SerializeField] private float _pixels1440p = 280f;

        [Tooltip("Virtual pixel count at 2160p / 4K (3840x2160).\n" +
                 "This was your original tuning target.")]
        [SerializeField] private float _pixels2160p = 324f;

        [Header("Boot up (TAB Open)")]
        [Tooltip("How long (seconds) the pixelation takes to fully appear.\n" +
                 "Effect starts invisible and pixels gradually become visible.")]
        [SerializeField] private float _resolveTime = 1.5f;

        [Header("Shutdown (TAB Close)")]
        [Tooltip("How long (seconds) the pixelation takes to vanish.\n" +
                 "Pixels fade from visible back to invisible.")]
        [SerializeField] private float _shutdownTime = 0.3f;

        [Header("Chromatic Aberration (per channel)")]
        [Tooltip("Red channel offset in virtual pixel units.\n" +
                 "X = horizontal, Y = vertical. (0, 0) = no offset.")]
        [SerializeField] private Vector2 _chromaR = new Vector2(-0.15f, 0f);

        [Tooltip("Green channel offset in virtual pixel units.\n" +
                 "Typically (0, 0). Green stays centered.")]
        [SerializeField] private Vector2 _chromaG = Vector2.zero;

        [Tooltip("Blue channel offset in virtual pixel units.\n" +
                 "X = horizontal, Y = vertical. (0, 0) = no offset.")]
        [SerializeField] private Vector2 _chromaB = new Vector2(0.15f, 0f);

        [Header("Visor Light")]
        [Tooltip("GameObject to enable/disable with the visor (e.g. a point or spot light).")]
        [SerializeField] private GameObject _visorLight;

        [Header("Sub pixel Shape")]
        [Tooltip("Dark gap width between sub pixels and pixel rows.\n" +
                 "0 = no gaps, 0.1 = visible grid, 0.2 = wide grid.")]
        [SerializeField, Range(0f, 0.3f)] private float _gapSize = 0.1f;

        [Tooltip("Sub pixel corner rounding.\n" +
                 "0 = sharp rectangles, 0.15 = slightly rounded, 0.5 = very rounded.")]
        [SerializeField, Range(0f, 1f)] private float _cornerRadius = 0.15f;

        [Tooltip("Brightness multiplier to compensate for sub pixel filtering.\n" +
                 "3.0 = physically correct, higher = brighter.")]
        [SerializeField, Range(1f, 5f)] private float _brightness = 3.0f;

        #endregion

        #region Private Fields

        private enum State { Idle, Boot, Active, Shutdown }

        private State _state = State.Idle;

        /// <summary>
        /// Normalised animation progress.
        /// 0 = screen resolution (invisible, effect matches native pixels).
        /// 1 = target percentage (fully pixelated, pixels clearly visible).
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
                        _progress = 0f;
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
                    if (isOpen) { _state = State.Boot; break; }

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

            // Lerp between per resolution presets based on actual screen height.
            // Below 1080: clamp to 1080 preset. Above 2160: clamp to 2160 preset.
            // Between breakpoints: linear interpolation for smooth transitions.
            float targetPixels;
            if (screenPixels <= 1080f)
            {
                targetPixels = _pixels1080p;
            }
            else if (screenPixels <= 1440f)
            {
                float t = (screenPixels - 1080f) / (1440f - 1080f);
                targetPixels = Mathf.Lerp(_pixels1080p, _pixels1440p, t);
            }
            else if (screenPixels <= 2160f)
            {
                float t = (screenPixels - 1440f) / (2160f - 1440f);
                targetPixels = Mathf.Lerp(_pixels1440p, _pixels2160p, t);
            }
            else
            {
                targetPixels = _pixels2160p;
            }

            // SmoothStep gives a nice ease in out curve.
            float anim = Mathf.SmoothStep(0f, 1f, _progress);

            // Lerp from screen resolution (invisible) to compensated target (visible pixels).
            float pixelCount = Mathf.Lerp(screenPixels, targetPixels, anim);

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
