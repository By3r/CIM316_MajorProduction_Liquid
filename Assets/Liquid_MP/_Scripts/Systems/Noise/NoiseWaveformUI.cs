using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Liquid.Audio
{
    /// <summary>
    /// Drives a pulse-style waveform noise meter UI.
    /// Reads LastFinalNoise from NoiseManager each frame fully driven by the
    /// room-multiplied final noise value.
    /// 
    /// SETUP:
    /// 1. Create a UI GameObject with a Horizontal Layout Group.
    ///    Set Child Alignment to Middle Center. Do NOT tick Control Child Size.
    /// 2. Create a bar prefab: UI Image, pivot (0.5, 0.5), fixed width.
    /// 3. Assign barContainer and barPrefab in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseWaveformUI : MonoBehaviour
    {
        #region Variables
        [Header("Bar Setup")]
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private GameObject barPrefab;
        [SerializeField] private int barCount = 32;
        [SerializeField] private float barMaxHeight = 80f;
        [SerializeField] private float barMinHeight = 4f;

        [Header("Transition Settings")]
        [Tooltip("How fast the displayed noise level tracks the NoiseManager value.")]
        [SerializeField] private float noiseSmoothSpeed = 4f;
        [Tooltip("How fast individual bar heights animate.")]
        [SerializeField] private float barSmoothSpeed = 12f;
        [Tooltip("How slowly the color phase transitions � keep low (1-3) for horror feel.")]
        [SerializeField] private float colorSmoothSpeed = 2f;

        [Header("Pulse Animation")]
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float flickerStrength = 0.08f;

        [Header("Colors Per Noise Level")]
        [SerializeField] private Color noneColor = new Color(0.25f, 0.22f, 0.30f, 0.25f);
        [SerializeField] private Color lowColor = new Color(0.55f, 0.45f, 0.65f, 0.55f);
        [SerializeField] private Color mediumColor = new Color(0.85f, 0.55f, 0.20f, 0.85f);
        [SerializeField] private Color highColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        [SerializeField] private Color extremeColor = new Color(1f, 0.05f, 0.05f, 1f);

        [Header("Optional UI")]
        [SerializeField] private Text noiseLevelLabel;
        [SerializeField] private Text roomLabel;
        [SerializeField] private Image progressBarFill;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private float[] _shapeCache;
        private readonly List<RectTransform> _bars = new List<RectTransform>();
        private readonly List<Image> _barImages = new List<Image>();
        private float[] _currentBarHeights;

        private float _displayNoise = 0f;
        private float _targetNoise = 0f;

        private Color _currentColor;
        private Color _targetColor;

        private float _animTime = 0f;

        private bool _debugOverride = false;
        private float _debugValue = 0f;

        #endregion

        private void Awake()
        {
            BuildShapeCache();
            BuildBars();
            _currentColor = noneColor;
            _targetColor = noneColor;
        }

        private void Start()
        {
            ValidateSetup();
        }

        private void Update()
        {
            _animTime += Time.deltaTime * pulseSpeed;

            HandleDebugKeys();

            // Read from NoiseManager
            _targetNoise = _debugOverride ? _debugValue : (NoiseManager.Instance != null ? NoiseManager.Instance.LastFinalNoise : 0f);

            // Smooth noise display value
            _displayNoise = Mathf.Lerp(_displayNoise, _targetNoise, Time.deltaTime * noiseSmoothSpeed);

            // Smooth color transition
            _targetColor = GetColorForNoise(_displayNoise);
            _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * colorSmoothSpeed);

            UpdateBars();
            UpdateLabels();
            UpdateProgressBar();
        }

        #region Shape Cache
        private void BuildShapeCache()
        {
            _shapeCache = new float[barCount];
            for (int i = 0; i < barCount; i++)
            {
                float t = (float)i / (barCount - 1);
                float center = Mathf.Exp(-Mathf.Pow((t - 0.5f) * 6f, 2f));
                float wingL = Mathf.Exp(-Mathf.Pow((t - 0.25f) * 8f, 2f)) * 0.4f;
                float wingR = Mathf.Exp(-Mathf.Pow((t - 0.75f) * 8f, 2f)) * 0.4f;
                _shapeCache[i] = Mathf.Clamp01(center + wingL + wingR);
            }
        }

        #endregion

        #region Bar Construction
        private void BuildBars()
        {
            if (barContainer == null || barPrefab == null)
            {
                Debug.LogError("[NoiseWaveformUI] barContainer or barPrefab not assigned.", this);
                return;
            }

            foreach (Transform child in barContainer)
                Destroy(child.gameObject);

            _bars.Clear();
            _barImages.Clear();
            _currentBarHeights = new float[barCount];

            for (int i = 0; i < barCount; i++)
            {
                GameObject bar = Instantiate(barPrefab, barContainer);
                RectTransform rt = bar.GetComponent<RectTransform>();
                Image img = bar.GetComponent<Image>();

                if (rt == null)
                {
                    Debug.LogError($"[NoiseWaveformUI] Bar prefab missing RectTransform at index {i}.", this);
                    continue;
                }

                rt.sizeDelta = new Vector2(rt.sizeDelta.x, barMinHeight);
                _currentBarHeights[i] = barMinHeight;
                _bars.Add(rt);
                _barImages.Add(img);
            }

            Log($"Built {_bars.Count} bars.");
        }

        #endregion

        #region Bar Updates
        private void UpdateBars()
        {
            if (_bars.Count == 0) return;

            float normalizedNoise = Mathf.Clamp01(_displayNoise / 4f);

            for (int i = 0; i < _bars.Count; i++)
            {
                if (_bars[i] == null) continue;

                float shape = _shapeCache[i];

                float pulseOffset = Mathf.Sin(_animTime + i * 0.4f) * 0.06f * normalizedNoise;

                float flicker = normalizedNoise > 0.5f
                    ? (Random.value - 0.5f) * flickerStrength * (normalizedNoise - 0.5f) * 2f
                    : 0f;

                float barValue = Mathf.Clamp01(shape * normalizedNoise + pulseOffset + flicker);

                float targetHeight = normalizedNoise < 0.01f
                    ? barMinHeight + shape * 6f
                    : Mathf.Lerp(barMinHeight, barMaxHeight, barValue);

                _currentBarHeights[i] = Mathf.Lerp(
                    _currentBarHeights[i], targetHeight, Time.deltaTime * barSmoothSpeed);

                var sd = _bars[i].sizeDelta;
                _bars[i].sizeDelta = new Vector2(sd.x, _currentBarHeights[i]);

                if (_barImages[i] != null)
                    _barImages[i].color = _currentColor;
            }
        }

        private Color GetColorForNoise(float noise)
        {
            if (noise <= 0f)
                return noneColor;
            if (noise <= 1f)
                return Color.Lerp(noneColor, lowColor, noise / 1f);
            if (noise <= 2f)
                return Color.Lerp(lowColor, mediumColor, (noise - 1f) / 1f);
            if (noise <= 3f)
                return Color.Lerp(mediumColor, highColor, (noise - 2f) / 1f);
            return Color.Lerp(highColor, extremeColor, Mathf.Clamp01((noise - 3f) / 1f));
        }

        private void UpdateLabels()
        {
            if (noiseLevelLabel != null)
            {
                NoiseLevel level = LevelFromFloat(_displayNoise);
                noiseLevelLabel.text = level.ToString().ToUpper();
            }

            if (roomLabel != null && NoiseManager.Instance != null)
            {
                var profile = NoiseManager.Instance.LastProfile;
                roomLabel.text = profile != null ? profile.EnvironmentName : "�";
            }
        }

        private void UpdateProgressBar()
        {
            if (progressBarFill != null)
                progressBarFill.fillAmount = Mathf.Clamp01(_displayNoise / 4f);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Instantly spike the waveform � for gunshots, explosions, etc.
        /// Decays naturally via NoiseManager's LastFinalNoise decay.
        /// </summary>
        public void TriggerSpike(float noiseValue)
        {
            _displayNoise = Mathf.Max(_displayNoise, noiseValue);
        }

        #endregion

        #region Debug

        private void HandleDebugKeys()
        {
            if (!showDebugLogs) return;

            if (Input.GetKeyDown(KeyCode.F1)) { _debugOverride = true; _debugValue = 0f; Debug.Log("[NoiseWaveformUI] DEBUG: NONE"); }
            if (Input.GetKeyDown(KeyCode.F2)) { _debugOverride = true; _debugValue = 0.5f; Debug.Log("[NoiseWaveformUI] DEBUG: LOW"); }
            if (Input.GetKeyDown(KeyCode.F3)) { _debugOverride = true; _debugValue = 1.5f; Debug.Log("[NoiseWaveformUI] DEBUG: MEDIUM"); }
            if (Input.GetKeyDown(KeyCode.F4)) { _debugOverride = true; _debugValue = 2.5f; Debug.Log("[NoiseWaveformUI] DEBUG: HIGH"); }
            if (Input.GetKeyDown(KeyCode.F5)) { _debugOverride = true; _debugValue = 4.0f; Debug.Log("[NoiseWaveformUI] DEBUG: EXTREME"); }
            if (Input.GetKeyDown(KeyCode.F6)) { _debugOverride = false; Debug.Log("[NoiseWaveformUI] DEBUG: Live data restored."); }
        }

        private void ValidateSetup()
        {
            if (barContainer == null) Debug.LogError("[NoiseWaveformUI] barContainer not assigned!", this);
            if (barPrefab == null) Debug.LogError("[NoiseWaveformUI] barPrefab not assigned!", this);
            if (NoiseManager.Instance == null) Debug.LogWarning("[NoiseWaveformUI] NoiseManager not found. Debug keys (F1-F5) still work.", this);
            if (_bars.Count == 0) Debug.LogError("[NoiseWaveformUI] No bars created!", this);
            else Log($"Setup is giood {_bars.Count} bars. F1=None F2=Low F3=Medium F4=High F5=Extreme F6=Live");
        }

        private static NoiseLevel LevelFromFloat(float value)
        {
            if (value <= 0f) return NoiseLevel.None;
            if (value <= 1.0f) return NoiseLevel.Low;
            if (value <= 2.0f) return NoiseLevel.Medium;
            if (value <= 3.0f) return NoiseLevel.High;
            return NoiseLevel.Extreme;
        }

        private void Log(string msg)
        {
            if (showDebugLogs) Debug.Log($"[NoiseWaveformUI] {msg}", this);
        }

        #endregion
    }
}