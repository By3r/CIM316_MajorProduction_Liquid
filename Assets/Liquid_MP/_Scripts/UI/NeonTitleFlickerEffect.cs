using TMPro;
using UnityEngine;

namespace _Scripts.UI.MainMenu
{
    /// <summary>
    /// Simple neon-style flicker that drives the emission color of a material so it feels like a failing neon sign.
    /// </summary>

    public class NeonTitleFlicker : MonoBehaviour
    {
        #region Variables
        [Header("Target")]
        [Tooltip("Renderer for mesh / 3D text.")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private TMP_Text tmpText;

        [Header("Emission / Colour Settings")]
        [SerializeField] private Color baseEmissionColor = new Color(0.5f, 0.9f, 1.5f, 1f);

        [Tooltip("Base intensity multiplier for the flicker.")]
        [SerializeField] private float baseIntensity = 2f;

        [Tooltip("Extra intensity added and removed during normal flicker.")]
        [SerializeField] private float flickerAmplitude = 1.5f;

        [Tooltip("Speed of the regular flicker animation.")]
        [SerializeField] private float flickerSpeed = 4f;

        [Header("Glitch Settings")]
        [Tooltip("Chance per second to trigger a short glitch (0 = never, 1 = very often).")]
        [SerializeField] private float glitchChancePerSecond = 0.5f;

        [Tooltip("Minimum and maximum duration of a glitch in seconds.")]
        [SerializeField] private Vector2 glitchDurationRange = new Vector2(0.04f, 0.25f);

        [Tooltip("How much to dim the sign during a glitch (0 = fully off, 1 = no dim).")]
        [SerializeField] private float glitchDimMultiplier = 0.1f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Material _materialInstance;
        private Color _originalEmissionColor;
        private Color _originalTextColor;

        private float _glitchTimer;
        private bool _useEmission;
        #endregion

        private void Reset()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (tmpText == null)
            {
                tmpText = GetComponentInChildren<TMP_Text>();
            }
        }

        private void Awake()
        {
            if (targetRenderer == null && tmpText == null)
            {
                Debug.LogWarning("No Renderer or TMP_Text assigned.", this);
                enabled = false;
                return;
            }

            if (targetRenderer != null)
            {
                _materialInstance = targetRenderer.material;
            }
            else if (tmpText != null)
            {
                tmpText.fontMaterial = new Material(tmpText.fontMaterial);
                _materialInstance = tmpText.fontMaterial;
            }

            if (_materialInstance != null && _materialInstance.HasProperty(EmissionColorId))
            {
                _useEmission = true;
                _originalEmissionColor = _materialInstance.GetColor(EmissionColorId);
                _materialInstance.EnableKeyword("_EMISSION");
            }
            else
            {
                _useEmission = false;

                if (tmpText == null)
                {
                    Debug.LogWarning("Target material has no _EmissionColor and no TMP_Text to fall back to.", this);
                    enabled = false;
                    return;
                }

                _originalTextColor = tmpText.color;
            }
        }

        private void Update()
        {
            float time = Time.unscaledTime;

            float sine = Mathf.Sin(time * flickerSpeed);
            float noise = Mathf.PerlinNoise(0f, time * (flickerSpeed * 0.7f)) * 2f - 1f;

            float flicker = baseIntensity + (sine + noise) * 0.5f * flickerAmplitude;
            flicker = Mathf.Max(0f, flicker);

            if (_glitchTimer > 0f)
            {
                _glitchTimer -= Time.unscaledDeltaTime;
                flicker *= glitchDimMultiplier;
            }
            else
            {
                if (glitchChancePerSecond > 0f)
                {
                    float chanceThisFrame = glitchChancePerSecond * Time.unscaledDeltaTime;
                    if (Random.value < chanceThisFrame)
                    {
                        _glitchTimer = Random.Range(glitchDurationRange.x, glitchDurationRange.y);
                    }
                }
            }

            if (_useEmission && _materialInstance != null)
            {
                Color finalColour = baseEmissionColor * flicker;
                _materialInstance.SetColor(EmissionColorId, finalColour);
            }
            else if (tmpText != null)
            {
                float brightness = Mathf.Clamp01((baseIntensity + flicker) * 0.25f);
                Color finalColour = _originalTextColor * brightness;

                finalColour.a = _originalTextColor.a;

                tmpText.color = finalColour;
            }
        }
    }
}