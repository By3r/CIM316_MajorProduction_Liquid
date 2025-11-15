using _Scripts.Core;
using _Scripts.Core.Managers;
using _Scripts.Systems.Player;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Manages crosshair appearance and behavior.
    /// Shows a normal crosshair by default, and switches to an interaction crosshair when looking at interactables.
    /// Integrates with InteractionController to detect when the player is looking at something interactable.
    /// Supports hiding the normal crosshair entirely for a more immersive experience.
    /// </summary>
    public class CrosshairManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Crosshair Sprites")]
        [Tooltip("Default crosshair sprite (simple dot or crosshair).")]
        [SerializeField] private Sprite _normalCrosshair;

        [Tooltip("Crosshair sprite when looking at an interactable object.")]
        [SerializeField] private Sprite _interactionCrosshair;

        [Header("UI References")]
        [Tooltip("The UI Image component that displays the crosshair.")]
        [SerializeField] private Image _crosshairImage;

        [Tooltip("Canvas group for fading crosshair in/out (optional).")]
        [SerializeField] private CanvasGroup _crosshairCanvasGroup;

        [Header("Visibility Settings")]
        [Tooltip("Hide the normal crosshair when not looking at interactables? (for immersion)")]
        [SerializeField] private bool _hideNormalCrosshair = false;

        [Tooltip("Always show the crosshair regardless of interaction state.")]
        [SerializeField] private bool _alwaysShowCrosshair = true;

        [Header("Animation Settings")]
        [Tooltip("How fast the crosshair changes between states.")]
        [SerializeField] private float _transitionSpeed = 10f;

        [Tooltip("Scale multiplier when showing the interaction crosshair.")]
        [SerializeField] private float _interactionScale = 1.2f;

        [Tooltip("Should the crosshair pulse when looking at interactables?")]
        [SerializeField] private bool _enablePulseEffect = true;

        [Tooltip("Speed of the pulse effect.")]
        [SerializeField] private float _pulseSpeed = 3f;

        [Tooltip("Intensity of the pulse effect (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float _pulseIntensity = 0.3f;

        [Header("Color Settings (Optional)")]
        [Tooltip("Should the crosshair change color when looking at interactables?")]
        [SerializeField] private bool _useColorChange = false;

        [Tooltip("Color when looking at interactables.")]
        [SerializeField] private Color _interactionColor = Color.white;

        [Tooltip("Normal crosshair color.")]
        [SerializeField] private Color _normalColor = Color.white;

        [Header("Interaction Rotation Animation")]
        [Tooltip("Play a rotation animation when the player interacts with something?")]
        [SerializeField] private bool _enableInteractionRotation = true;

        [Tooltip("Rotation angles on each axis when interacting (in degrees).")]
        [SerializeField] private Vector3 _interactionRotationAngles = new Vector3(10f, 0f, 30f);

        [Tooltip("How long the rotation animation takes (in seconds).")]
        [SerializeField] private float _interactionRotationDuration = 0.2f;

        [Tooltip("Animation curve for the rotation (makes it feel more dynamic).")]
        [SerializeField] private AnimationCurve _interactionRotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Private Fields

        private InteractionController _interactionController;
        private Vector3 _baseScale;
        private float _pulseTimer;
        private Color _currentColor;
        private Color _targetColor;
        private bool _isShowingInteraction;
        private Coroutine _rotationCoroutine;
        private bool _wasLookingAtInteractable;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether the crosshair is currently showing the interaction state.
        /// </summary>
        public bool IsShowingInteraction => _isShowingInteraction;

        /// <summary>
        /// Gets or sets whether the normal crosshair should be hidden (immersion mode).
        /// </summary>
        public bool HideNormalCrosshair
        {
            get => _hideNormalCrosshair;
            set => _hideNormalCrosshair = value;
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            ValidateComponents();
        }

        private void Start()
        {
            InitializeCrosshair();
            FindInteractionController();
        }

        private void ValidateComponents()
        {
            if (_crosshairImage == null)
            {
                _crosshairImage = GetComponent<Image>();
                if (_crosshairImage == null)
                {
                    Debug.LogError("[CrosshairManager] No Image component found! Please assign a crosshair Image.");
                }
            }

            if (_crosshairCanvasGroup == null)
            {
                _crosshairCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (_normalCrosshair == null)
            {
                Debug.LogWarning("[CrosshairManager] Normal crosshair sprite is not assigned!");
            }

            if (_interactionCrosshair == null)
            {
                Debug.LogWarning("[CrosshairManager] Interaction crosshair sprite is not assigned!");
            }
        }

        private void InitializeCrosshair()
        {
            if (_crosshairImage != null)
            {
                _baseScale = _crosshairImage.transform.localScale;
                _crosshairImage.sprite = _normalCrosshair;
                _currentColor = _normalColor;
                _targetColor = _normalColor;
                _crosshairImage.color = _currentColor;
                _crosshairImage.enabled = true;
            }

            if (_crosshairCanvasGroup != null)
            {
                _crosshairCanvasGroup.alpha = 1f;
            }
        }

        private void FindInteractionController()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _interactionController = player.GetComponent<InteractionController>();
            }

            if (_interactionController == null)
            {
                _interactionController = FindFirstObjectByType<InteractionController>();
            }

            if (_interactionController == null)
            {
                Debug.LogWarning("[CrosshairManager] Could not find InteractionController in scene!");
            }
        }

        #endregion

        #region Update Loop

        private void LateUpdate()
        {
            UpdateCrosshairState();
            UpdateCrosshairSprite();
            UpdateCrosshairColor();
            UpdateCrosshairScale();
            UpdatePulseEffect();
            UpdateCrosshairVisibility();
            CheckForInteraction();
        }

        #endregion

        #region State Management

        private void UpdateCrosshairState()
        {
            if (_interactionController == null) return;

            bool shouldShowInteraction = _interactionController.IsLookingAtDoor;

            if (shouldShowInteraction != _isShowingInteraction)
            {
                _isShowingInteraction = shouldShowInteraction;
                _pulseTimer = 0f;
            }
        }

        private void UpdateCrosshairSprite()
        {
            if (_crosshairImage == null) return;

            Sprite targetSprite = _isShowingInteraction ? _interactionCrosshair : _normalCrosshair;
            
            if (_crosshairImage.sprite != targetSprite)
            {
                _crosshairImage.sprite = targetSprite;
            }
        }

        private void UpdateCrosshairColor()
        {
            if (_crosshairImage == null || !_useColorChange) return;

            _targetColor = _isShowingInteraction ? _interactionColor : _normalColor;

            _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * _transitionSpeed);
            
            var finalColor = _currentColor;
            finalColor.a = _crosshairImage.color.a;
            _crosshairImage.color = finalColor;
        }

        private void UpdateCrosshairScale()
        {
            if (_crosshairImage == null) return;

            Vector3 targetScale = _isShowingInteraction ? _baseScale * _interactionScale : _baseScale;

            _crosshairImage.transform.localScale = Vector3.Lerp(
                _crosshairImage.transform.localScale,
                targetScale,
                Time.deltaTime * _transitionSpeed
            );
        }

        private void UpdatePulseEffect()
        {
            if (!_enablePulseEffect || _crosshairImage == null) return;

            if (_isShowingInteraction)
            {
                _pulseTimer += Time.deltaTime * _pulseSpeed;
                var pulseValue = 1f + Mathf.Sin(_pulseTimer) * _pulseIntensity;

                var color = _crosshairImage.color;
                color.a = pulseValue;
                _crosshairImage.color = color;
            }
            else
            {
                var color = _crosshairImage.color;
                color.a = Mathf.Lerp(color.a, 1f, Time.deltaTime * _transitionSpeed);
                _crosshairImage.color = color;
                _pulseTimer = 0f;
            }
        }

        private void UpdateCrosshairVisibility()
        {
            if (_crosshairImage == null) return;

            bool shouldBeVisible = _alwaysShowCrosshair || _isShowingInteraction;

            if (_hideNormalCrosshair && !_isShowingInteraction)
            {
                shouldBeVisible = false;
            }

            if (_crosshairCanvasGroup != null)
            {
                float targetAlpha = shouldBeVisible ? 1f : 0f;
                _crosshairCanvasGroup.alpha = Mathf.Lerp(
                    _crosshairCanvasGroup.alpha,
                    targetAlpha,
                    Time.deltaTime * _transitionSpeed
                );
            }
            else
            {
                _crosshairImage.enabled = shouldBeVisible;
            }
        }

        private void CheckForInteraction()
        {
            if (_interactionController == null || !_enableInteractionRotation) return;

            bool isLookingAtInteractable = _interactionController.IsLookingAtDoor;

            if (isLookingAtInteractable && 
                InputManager.Instance != null && 
                InputManager.Instance.InteractPressed)
            {
                PlayInteractionRotation();
            }

            if (_wasLookingAtInteractable && !isLookingAtInteractable)
            {
                ResetRotation();
            }

            _wasLookingAtInteractable = isLookingAtInteractable;
        }

        #endregion

        #region Rotation Animation

        private void PlayInteractionRotation()
        {
            if (_crosshairImage == null) return;

            if (_rotationCoroutine != null)
            {
                StopCoroutine(_rotationCoroutine);
            }

            _rotationCoroutine = StartCoroutine(RotationAnimationCoroutine());
        }

        private System.Collections.IEnumerator RotationAnimationCoroutine()
        {
            float elapsed = 0f;
            Quaternion startRotation = _crosshairImage.transform.localRotation;
            Quaternion targetRotation = Quaternion.Euler(_interactionRotationAngles);

            while (elapsed < _interactionRotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = _interactionRotationCurve.Evaluate(elapsed / _interactionRotationDuration);
                _crosshairImage.transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, t);
                yield return null;
            }

            _crosshairImage.transform.localRotation = targetRotation;

            yield return new WaitForSeconds(0.05f);

            elapsed = 0f;
            startRotation = _crosshairImage.transform.localRotation;
            targetRotation = Quaternion.identity;

            while (elapsed < _interactionRotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = _interactionRotationCurve.Evaluate(elapsed / _interactionRotationDuration);
                _crosshairImage.transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, t);
                yield return null;
            }

            _crosshairImage.transform.localRotation = Quaternion.identity;
            _rotationCoroutine = null;
        }

        private void ResetRotation()
        {
            if (_crosshairImage == null) return;

            if (_rotationCoroutine != null)
            {
                StopCoroutine(_rotationCoroutine);
                _rotationCoroutine = null;
            }

            _crosshairImage.transform.localRotation = Quaternion.identity;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the interaction controller manually.
        /// Useful if the player spawns dynamically after CrosshairManager initialization.
        /// </summary>
        /// <param name="controller">The InteractionController to monitor.</param>
        public void SetInteractionController(InteractionController controller)
        {
            _interactionController = controller;
        }

        /// <summary>
        /// Sets whether the crosshair should be visible or hidden.
        /// </summary>
        /// <param name="visible">True to show, false to hide.</param>
        public void SetCrosshairVisible(bool visible)
        {
            if (_crosshairCanvasGroup != null)
            {
                _crosshairCanvasGroup.alpha = visible ? 1f : 0f;
            }
            else if (_crosshairImage != null)
            {
                _crosshairImage.enabled = visible;
            }
        }

        /// <summary>
        /// Gets whether the crosshair is currently visible.
        /// </summary>
        /// <returns>True if visible, false if hidden.</returns>
        public bool IsCrosshairVisible()
        {
            if (_crosshairCanvasGroup != null)
            {
                return _crosshairCanvasGroup.alpha > 0.01f;
            }
            else if (_crosshairImage != null)
            {
                return _crosshairImage.enabled;
            }
            return false;
        }

        /// <summary>
        /// Toggles the "hide normal crosshair" setting at runtime.
        /// Useful for a settings menu option.
        /// </summary>
        /// <param name="hide">True to hide normal crosshair (immersion mode), false to always show.</param>
        public void SetHideNormalCrosshair(bool hide)
        {
            _hideNormalCrosshair = hide;
        }

        /// <summary>
        /// Gets debug information about the current crosshair state.
        /// </summary>
        /// <returns>String containing debug information.</returns>
        public string GetDebugInfo()
        {
            return $"Interaction: {_isShowingInteraction}, " +
                   $"Visible: {IsCrosshairVisible()}, " +
                   $"HideNormal: {_hideNormalCrosshair}, " +
                   $"Rotating: {(_rotationCoroutine != null)}, " +
                   $"InteractionController: {(_interactionController != null ? "Found" : "Missing")}";
        }

        #endregion

        #region Debug

        private void OnValidate()
        {
            _transitionSpeed = Mathf.Max(0.1f, _transitionSpeed);
            _interactionScale = Mathf.Max(0.1f, _interactionScale);
            _pulseSpeed = Mathf.Max(0.1f, _pulseSpeed);
            _pulseIntensity = Mathf.Clamp01(_pulseIntensity);
            _interactionRotationDuration = Mathf.Max(0.01f, _interactionRotationDuration);
        }

        #endregion
    }
}