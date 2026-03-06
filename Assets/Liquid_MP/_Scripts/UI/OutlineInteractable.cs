using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liquid.UI
{
    /// <summary>
    /// Pointer-driven color transitions for UI elements.
    /// Drives an <see cref="Outline"/> component and/or a <see cref="Graphic"/>'s
    /// material glow through Normal → Hover → Pressed states with smooth lerping.
    ///
    /// Supports two targets (both optional, use either or both):
    ///   • Outline component — drives effectColor
    ///   • Graphic with UIGlow material — drives _GlowColor shader property
    ///
    /// States:
    ///   Normal  — dim border / glow (default idle state)
    ///   Hover   — slightly brighter (pointer enters)
    ///   Pressed — bright (pointer down)
    ///   Active  — stays in pressed color (toggle mode or set via code)
    /// </summary>
    public class OutlineInteractable : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        #region Events

        /// <summary>Fired on left-click.</summary>
        public event Action OnClicked;

        /// <summary>Fired on right-click. Passes screen position for context menus.</summary>
        public event Action<Vector2> OnRightClicked;

        #endregion

        #region Serialized Fields

        [Header("Outline (optional)")]
        [Tooltip("The Outline component to drive. Auto-found on this GameObject if left empty.")]
        [SerializeField] private Outline _outline;

        [Header("Glow Material (optional)")]
        [Tooltip("A Graphic (Image, RawImage) whose material has a _GlowColor property (Liquid/UI/Glow shader). " +
                 "Leave empty to skip glow driving.")]
        [SerializeField] private Graphic _glowGraphic;

        [Header("State Colors")]
        [Tooltip("Idle border color.")]
        [SerializeField] private Color _normalColor = new Color(0.18f, 0.24f, 0.16f, 1f);    // #2e3e28

        [Tooltip("Hovered border color.")]
        [SerializeField] private Color _hoverColor = new Color(0.36f, 0.24f, 0f, 1f);         // #5c3c00

        [Tooltip("Pressed / active border color.")]
        [SerializeField] private Color _pressedColor = new Color(1f, 0.69f, 0f, 1f);           // #ffb000

        [Header("Transition")]
        [Tooltip("How fast the outline color transitions between states.")]
        [SerializeField] private float _transitionSpeed = 12f;

        [Header("Optional")]
        [Tooltip("If true, this button stays in 'active' (pressed color) after clicking until manually deactivated.")]
        [SerializeField] private bool _toggleMode;

        #endregion

        #region Private Fields

        private Color _targetColor;
        private Color _currentGlowColor;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isActive;
        private bool _interactable = true;

        // Material instance per-graphic (so changing one slot doesn't affect all)
        private Material _glowMatInstance;
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

        #endregion

        #region Properties

        /// <summary>Whether this element can be interacted with.</summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                if (!_interactable)
                {
                    _isHovered = false;
                    _isPressed = false;
                    UpdateTargetColor();
                }
            }
        }

        /// <summary>In toggle mode, whether this element is currently in active state.</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                UpdateTargetColor();
            }
        }

        /// <summary>The current normal/idle color. Can be changed at runtime.</summary>
        public Color NormalColor
        {
            get => _normalColor;
            set
            {
                _normalColor = value;
                UpdateTargetColor();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_outline == null)
                _outline = GetComponent<Outline>();

            // Create a material INSTANCE so each element can have independent glow state.
            // Unlike Renderer.material (3D), Graphic.material does NOT auto-instance —
            // it returns the shared material. We must clone explicitly.
            if (_glowGraphic != null && _glowGraphic.material != null)
            {
                _glowMatInstance = new Material(_glowGraphic.material);
                _glowGraphic.material = _glowMatInstance;
            }

            #if UNITY_EDITOR
            ValidateSetup();
            #endif
        }

        private void OnDestroy()
        {
            // Clean up the cloned material instance to avoid memory leaks.
            if (_glowMatInstance != null)
            {
                Destroy(_glowMatInstance);
                _glowMatInstance = null;
            }
        }

        private void OnEnable()
        {
            _targetColor = _isActive ? _pressedColor : _normalColor;

            // Snap to target on enable (no transition from stale state)
            if (_outline != null)
                _outline.effectColor = _targetColor;

            if (_glowMatInstance != null)
            {
                _glowMatInstance.SetColor(GlowColorId, _targetColor);
                _currentGlowColor = _targetColor;
            }
        }

        private void Update()
        {
            float step = _transitionSpeed * Time.unscaledDeltaTime;

            // Smoothly lerp outline toward target color
            if (_outline != null)
            {
                Color current = _outline.effectColor;
                if (current != _targetColor)
                {
                    _outline.effectColor = Color.Lerp(current, _targetColor, step);
                }
            }

            // Smoothly lerp glow material toward target color
            if (_glowMatInstance != null)
            {
                if (_currentGlowColor != _targetColor)
                {
                    _currentGlowColor = Color.Lerp(_currentGlowColor, _targetColor, step);
                    _glowMatInstance.SetColor(GlowColorId, _currentGlowColor);
                }
            }
        }

        #endregion

        #region Pointer Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isHovered = true;
            UpdateTargetColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_interactable) return;
            _isHovered = false;
            _isPressed = false;
            UpdateTargetColor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isPressed = true;
                UpdateTargetColor();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_interactable) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isPressed = false;
                UpdateTargetColor();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_toggleMode)
                {
                    _isActive = !_isActive;
                    UpdateTargetColor();
                }

                OnClicked?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClicked?.Invoke(eventData.position);
            }
        }

        #endregion

        #region Private Methods

        private void UpdateTargetColor()
        {
            if (_isPressed)
                _targetColor = _pressedColor;
            else if (_isActive)
                _targetColor = _pressedColor;
            else if (_isHovered)
                _targetColor = _hoverColor;
            else
                _targetColor = _normalColor;
        }

        #endregion

        #region Editor Validation

        #if UNITY_EDITOR
        /// <summary>
        /// Checks for common setup issues that prevent pointer events from firing.
        /// Only runs in the Editor at Awake time. Logs warnings for each problem found.
        /// </summary>
        private void ValidateSetup()
        {
            string tag = $"[OutlineInteractable] '{name}'";
            bool anyIssue = false;

            // 1. Check for a Graphic with raycastTarget on this GO or any child.
            //    GraphicRaycaster only detects GameObjects that have a Graphic with
            //    raycastTarget = true. Without one, no pointer events can reach us.
            bool hasRaycastTarget = false;
            var graphics = GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (g.raycastTarget)
                {
                    hasRaycastTarget = true;
                    break;
                }
            }
            if (!hasRaycastTarget)
            {
                Debug.LogWarning(
                    $"{tag} — No Graphic with raycastTarget=true found on this GameObject or its children. " +
                    "Add an Image (can be transparent) with Raycast Target enabled, or enable it on an existing Graphic.",
                    this);
                anyIssue = true;
            }

            // 2. Check for a Canvas with a GraphicRaycaster in the parent chain.
            //    World Space Canvases MUST have a GraphicRaycaster for pointer events.
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogWarning(
                    $"{tag} — No parent Canvas found. OutlineInteractable must be inside a Canvas hierarchy.",
                    this);
                anyIssue = true;
            }
            else
            {
                // Walk up to the root canvas (GraphicRaycaster lives on the root canvas)
                Canvas rootCanvas = parentCanvas.rootCanvas;
                if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    Debug.LogWarning(
                        $"{tag} — Root Canvas '{rootCanvas.name}' has no GraphicRaycaster component. " +
                        "Add a GraphicRaycaster to the Canvas GameObject for pointer events to work.",
                        this);
                    anyIssue = true;
                }

                // 3. Check World Space Canvas has an event camera.
                if (rootCanvas.renderMode == RenderMode.WorldSpace && rootCanvas.worldCamera == null)
                {
                    Debug.LogWarning(
                        $"{tag} — World Space Canvas '{rootCanvas.name}' has no worldCamera assigned. " +
                        "The GraphicRaycaster needs a camera to convert screen positions to rays.",
                        this);
                    anyIssue = true;
                }
            }

            // 4. Check parent CanvasGroups for blocksRaycasts / interactable.
            //    A CanvasGroup with blocksRaycasts=false makes all children invisible to raycasts.
            //    NOTE: These may change at runtime (e.g. when inventory panel opens).
            //    This check logs the CURRENT state at Awake time as a heads-up.
            var canvasGroups = GetComponentsInParent<CanvasGroup>(true);
            foreach (var cg in canvasGroups)
            {
                if (!cg.blocksRaycasts)
                {
                    Debug.LogWarning(
                        $"{tag} — Parent CanvasGroup '{cg.name}' has blocksRaycasts=false. " +
                        "Raycasts won't reach this element until blocksRaycasts is enabled. " +
                        "(This may be intentional if the group is toggled at runtime, e.g. when opening a panel.)",
                        this);
                    anyIssue = true;
                }
            }

            // 5. Check for EventSystem in the scene.
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning(
                    $"{tag} — No EventSystem found in the scene. " +
                    "Create an EventSystem (GameObject → UI → Event System) for pointer events to work.",
                    this);
                anyIssue = true;
            }

            if (!anyIssue)
            {
                Debug.Log($"{tag} — Setup looks correct. " +
                          "If hover still doesn't work, make sure the cursor is unlocked (open inventory with TAB).");
            }
        }
        #endif

        #endregion
    }
}
