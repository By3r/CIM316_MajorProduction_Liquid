using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.UI.Interaction
{
    /// <summary>
    /// Deus Ex-style object highlighting system.
    /// Raycasts from the camera center, detects objects on configured layers,
    /// and displays 4 corner brackets that expand from the crosshair to frame the object.
    /// Each bracket is positioned independently in screen space — no frame-parent anchoring tricks.
    /// </summary>
    public class ObjectHighlightingSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Raycast Settings")]
        [SerializeField] private float _raycastDistance = 5f;
        [SerializeField] private Camera _playerCamera;

        [Header("Layer Configurations")]
        [SerializeField] private List<LayerHighlightConfig> _layerConfigs = new List<LayerHighlightConfig>();

        [Header("Animation Settings")]
        [SerializeField] private float _bracketExpandSpeed = 18f;
        [SerializeField] private float _frameTrackingSpeed = 20f;
        [SerializeField] private float _fadeSpeed = 15f;

        [Header("Frame Settings")]
        [SerializeField] private float _minFrameSize = 60f;
        [SerializeField] private float _framePadding = 15f;

        [Header("UI References")]
        [Tooltip("Screen Space - Overlay canvas. Auto-created if not assigned.")]
        [SerializeField] private Canvas _highlightCanvas;

        [Tooltip("The 4 corner bracket Images (TL, TR, BL, BR). Auto-created if not assigned.")]
        [SerializeField] private Image[] _cornerBrackets;

        [Tooltip("Text label shown below the highlight. Auto-created if not assigned.")]
        [SerializeField] private TextMeshProUGUI _highlightText;

        [Header("Crosshair Integration")]
        [SerializeField] private CrosshairManager _crosshairManager;
        [SerializeField] private bool _fadeCrosshairOnHighlight = true;
        [SerializeField] private float _crosshairFadeSpeed = 100f;

        [Header("Bracket Animation")]
        [Tooltip("Brackets expand from screen center (crosshair) to object corners")]
        [SerializeField] private bool _animateBracketsFromCenter = true;

        #endregion

        #region Private Fields

        // Target detection
        private GameObject _currentTargetObject;
        private LayerHighlightConfig _currentConfig;
        private Renderer _targetRenderer;
        private Collider[] _targetColliders;
        private LayerMask _combinedLayerMask;

        // The 4 screen-space corner positions we want brackets to reach
        private Vector2[] _bracketTargetPositions = new Vector2[4];
        // Current interpolated positions
        private Vector2[] _bracketCurrentPositions = new Vector2[4];
        // Cached RectTransforms for the 4 brackets
        private RectTransform[] _bracketRects = new RectTransform[4];
        // Text label RectTransform
        private RectTransform _textRect;

        // Canvas rect for coordinate conversion
        private RectTransform _canvasRect;

        // Fade
        private CanvasGroup _canvasGroup;
        private float _currentAlpha;
        private float _targetAlpha;
        private bool _isHighlightActive;

        // Crosshair
        private float _currentCrosshairAlpha = 1f;
        private float _targetCrosshairAlpha = 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureCanvas();
            EnsureBrackets();
            EnsureText();

            _canvasRect = _highlightCanvas.GetComponent<RectTransform>();

            _canvasGroup = _highlightCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _highlightCanvas.gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Cache bracket RectTransforms
            for (int i = 0; i < 4; i++)
            {
                if (_cornerBrackets[i] != null)
                    _bracketRects[i] = _cornerBrackets[i].GetComponent<RectTransform>();
            }

            if (_highlightText != null)
                _textRect = _highlightText.GetComponent<RectTransform>();

            CalculateCombinedLayerMask();
            SetVisible(false);
        }

        private void Start()
        {
            if (_playerCamera == null)
            {
                var pm = _Scripts.Systems.Player.PlayerManager.Instance;
                if (pm != null && pm.CurrentPlayer != null)
                    _playerCamera = pm.CurrentPlayer.GetComponentInChildren<Camera>();
            }
            if (_playerCamera == null)
                _playerCamera = Camera.main;
        }

        private void Update()
        {
            DetectTarget();

            if (_isHighlightActive && _currentTargetObject != null)
                CalculateTargetPositions();

            Animate();
        }

        #endregion

        #region Target Detection

        private void TryFindCamera()
        {
            var pm = _Scripts.Systems.Player.PlayerManager.Instance;
            if (pm != null && pm.CurrentPlayer != null)
                _playerCamera = pm.CurrentPlayer.GetComponentInChildren<Camera>();
            if (_playerCamera == null)
                _playerCamera = Camera.main;
        }

        private void CalculateCombinedLayerMask()
        {
            _combinedLayerMask = 0;
            foreach (var config in _layerConfigs)
            {
                if (config.enabled)
                    _combinedLayerMask |= (1 << config.layer);
            }
        }

        private void DetectTarget()
        {
            if (_playerCamera == null)
            {
                TryFindCamera();
                if (_playerCamera == null) return;
            }

            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _combinedLayerMask))
            {
                GameObject hitObject = hit.collider.gameObject;
                LayerHighlightConfig config = GetConfigForLayer(hitObject.layer);

                if (config != null && config.enabled)
                {
                    if (hitObject != _currentTargetObject)
                        ActivateHighlight(hitObject, config);
                    return;
                }
            }

            if (_isHighlightActive)
                DeactivateHighlight();
        }

        private LayerHighlightConfig GetConfigForLayer(int layer)
        {
            foreach (var config in _layerConfigs)
            {
                if (config.layer == layer && config.enabled)
                    return config;
            }
            return null;
        }

        #endregion

        #region Highlight Control

        private void ActivateHighlight(GameObject target, LayerHighlightConfig config)
        {
            _currentTargetObject = target;
            _currentConfig = config;

            _targetColliders = target.GetComponentsInChildren<Collider>();
            _targetRenderer = null;
            if (_targetColliders.Length == 0)
            {
                _targetRenderer = target.GetComponent<Renderer>();
                if (_targetRenderer == null)
                    _targetRenderer = target.GetComponentInChildren<Renderer>();
            }

            // Apply visual config to brackets
            ApplyConfigVisuals(config);

            // Text
            if (_highlightText != null)
            {
                if (config.showText)
                {
                    _highlightText.gameObject.SetActive(true);
                    _highlightText.text = string.IsNullOrEmpty(config.displayText)
                        ? target.name
                        : config.displayText;
                    _highlightText.color = config.textColor;
                    _highlightText.fontSize = config.textFontSize;
                }
                else
                {
                    _highlightText.gameObject.SetActive(false);
                }
            }

            _targetAlpha = 1f;
            _isHighlightActive = true;

            // Snap brackets to screen center for the expand-from-crosshair effect
            if (_animateBracketsFromCenter)
            {
                Vector2 screenCenter = ScreenCenterInCanvas();
                for (int i = 0; i < 4; i++)
                    _bracketCurrentPositions[i] = screenCenter;
            }

            // Crosshair fade
            if (_fadeCrosshairOnHighlight && _crosshairManager != null && config.fadeCrosshair)
                _targetCrosshairAlpha = 0f;
        }

        private void DeactivateHighlight()
        {
            _currentTargetObject = null;
            _currentConfig = null;
            _targetRenderer = null;
            _targetColliders = null;
            _targetAlpha = 0f;
            _isHighlightActive = false;

            if (_fadeCrosshairOnHighlight && _crosshairManager != null)
                _targetCrosshairAlpha = 1f;
        }

        private void ApplyConfigVisuals(LayerHighlightConfig config)
        {
            if (_cornerBrackets == null || _cornerBrackets.Length != 4)
                return;

            foreach (var bracket in _cornerBrackets)
            {
                if (bracket == null) continue;
                bracket.gameObject.SetActive(config.showBrackets);
                bracket.color = config.bracketColor;
                if (config.bracketSprite != null)
                    bracket.sprite = config.bracketSprite;

                RectTransform rt = bracket.GetComponent<RectTransform>();
                if (rt != null)
                    rt.sizeDelta = new Vector2(config.bracketSize, config.bracketSize);
            }
        }

        #endregion

        #region UI Setup

        private void EnsureCanvas()
        {
            if (_highlightCanvas != null)
            {
                // Force overlay mode regardless of Inspector setup
                _highlightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            GameObject go = new GameObject("HighlightCanvas");
            go.transform.SetParent(transform);

            _highlightCanvas = go.AddComponent<Canvas>();
            _highlightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _highlightCanvas.sortingOrder = 100;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = go.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }

        private void EnsureBrackets()
        {
            if (_cornerBrackets != null && _cornerBrackets.Length == 4 && _cornerBrackets[0] != null)
            {
                // Force all brackets: anchor center, pivot center
                for (int i = 0; i < 4; i++)
                {
                    if (_cornerBrackets[i] == null) continue;
                    _cornerBrackets[i].raycastTarget = false;
                    RectTransform rt = _cornerBrackets[i].GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                }
                return;
            }

            // Auto-create brackets
            _cornerBrackets = new Image[4];
            string[] names = { "Bracket_TL", "Bracket_TR", "Bracket_BL", "Bracket_BR" };
            // Rotations make the L-shape point inward at each corner
            float[] rotations = { 0f, -90f, 90f, 180f };

            Sprite bracketSprite = CreateBracketSprite();

            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject(names[i]);
                go.transform.SetParent(_highlightCanvas.transform, false);

                Image img = go.AddComponent<Image>();
                img.sprite = bracketSprite;
                img.color = Color.white;
                img.raycastTarget = false;

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(20f, 20f);
                rt.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);

                _cornerBrackets[i] = img;
            }
        }

        private void EnsureText()
        {
            if (_highlightText != null)
            {
                _highlightText.raycastTarget = false;
                // Force anchor to canvas center so we position it absolutely
                RectTransform rt = _highlightText.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(300f, 50f);
                return;
            }

            GameObject go = new GameObject("HighlightText");
            go.transform.SetParent(_highlightCanvas.transform, false);

            _highlightText = go.AddComponent<TextMeshProUGUI>();
            _highlightText.fontSize = 16;
            _highlightText.color = Color.white;
            _highlightText.fontStyle = FontStyles.Bold;
            _highlightText.alignment = TextAlignmentOptions.Center;
            _highlightText.text = "";
            _highlightText.raycastTarget = false;

            RectTransform textRt = go.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.sizeDelta = new Vector2(300f, 50f);
        }

        private Sprite CreateBracketSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            int thickness = 3;

            // Vertical bar (left edge)
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < thickness; x++)
                    pixels[y * 32 + x] = Color.white;

            // Horizontal bar (top edge)
            for (int x = 0; x < 32; x++)
                for (int y = 32 - thickness; y < 32; y++)
                    pixels[y * 32 + x] = Color.white;

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }

        #endregion

        #region Position Calculation

        /// <summary>
        /// Computes the 4 screen-space corner positions from the object's world bounds,
        /// converts them to canvas-local coordinates, and stores them as bracket targets.
        /// Also positions the text label below the bottom edge.
        /// </summary>
        private void CalculateTargetPositions()
        {
            if (_currentTargetObject == null || _playerCamera == null)
                return;

            Bounds bounds = GetObjectBounds();
            if (bounds.size == Vector3.zero)
                return;

            // Project all 8 world-space corners to screen space, find the 2D AABB
            Vector3[] worldCorners = GetBoundsCorners(bounds);
            Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);
            bool hasValid = false;

            foreach (Vector3 corner in worldCorners)
            {
                Vector3 sp = _playerCamera.WorldToScreenPoint(corner);
                if (sp.z <= 0) continue;

                hasValid = true;
                screenMin.x = Mathf.Min(screenMin.x, sp.x);
                screenMin.y = Mathf.Min(screenMin.y, sp.y);
                screenMax.x = Mathf.Max(screenMax.x, sp.x);
                screenMax.y = Mathf.Max(screenMax.y, sp.y);
            }

            if (!hasValid) return;

            // Convert screen corners to canvas-local coordinates
            Vector2 canvasMin = ScreenToCanvas(screenMin);
            Vector2 canvasMax = ScreenToCanvas(screenMax);

            // Apply padding and minimum size
            Vector2 size = canvasMax - canvasMin;
            size.x = Mathf.Max(size.x + _framePadding * 2f, _minFrameSize);
            size.y = Mathf.Max(size.y + _framePadding * 2f, _minFrameSize);

            Vector2 center = (canvasMin + canvasMax) * 0.5f;
            Vector2 halfSize = size * 0.5f;

            // Target positions for each bracket (canvas-local coordinates)
            // 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight
            _bracketTargetPositions[0] = center + new Vector2(-halfSize.x,  halfSize.y);
            _bracketTargetPositions[1] = center + new Vector2( halfSize.x,  halfSize.y);
            _bracketTargetPositions[2] = center + new Vector2(-halfSize.x, -halfSize.y);
            _bracketTargetPositions[3] = center + new Vector2( halfSize.x, -halfSize.y);

            // Text label: centered below the bottom edge
            if (_textRect != null)
                _textRect.anchoredPosition = new Vector2(center.x, center.y - halfSize.y - 10f);
        }

        private Bounds GetObjectBounds()
        {
            if (_targetColliders != null && _targetColliders.Length > 0)
            {
                Bounds combined = _targetColliders[0].bounds;
                for (int i = 1; i < _targetColliders.Length; i++)
                    combined.Encapsulate(_targetColliders[i].bounds);
                return combined;
            }

            if (_targetRenderer != null)
                return _targetRenderer.bounds;

            return new Bounds();
        }

        private Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            return new Vector3[]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x,  e.y,  e.z),
            };
        }

        /// <summary>
        /// Converts a screen-space point to canvas-local coordinates.
        /// Works correctly with any CanvasScaler settings.
        /// </summary>
        private Vector2 ScreenToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        /// <summary>
        /// Returns the canvas-local position of screen center (the crosshair).
        /// </summary>
        private Vector2 ScreenCenterInCanvas()
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return ScreenToCanvas(screenCenter);
        }

        #endregion

        #region Animation

        private void Animate()
        {
            // Fade
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;

            // Crosshair
            if (_crosshairManager != null && _fadeCrosshairOnHighlight)
            {
                _currentCrosshairAlpha = Mathf.MoveTowards(
                    _currentCrosshairAlpha, _targetCrosshairAlpha, _crosshairFadeSpeed * Time.deltaTime);
                _crosshairManager.SetExternalHideRequest(_currentCrosshairAlpha < 0.5f);
            }

            bool visible = _currentAlpha > 0.01f;
            SetVisible(visible);

            if (!visible)
            {
                // Reset bracket positions to screen center so next activation expands from crosshair
                if (_animateBracketsFromCenter)
                {
                    Vector2 center = ScreenCenterInCanvas();
                    for (int i = 0; i < 4; i++)
                        _bracketCurrentPositions[i] = center;
                }
                return;
            }

            // Move each bracket toward its target corner
            float speed = _bracketExpandSpeed * 100f * Time.deltaTime;
            float trackSpeed = _frameTrackingSpeed * 100f * Time.deltaTime;

            for (int i = 0; i < 4; i++)
            {
                if (_bracketRects[i] == null) continue;

                // Use faster expand speed when first appearing, tracking speed when following the object
                float useSpeed = _isHighlightActive ? Mathf.Max(speed, trackSpeed) : trackSpeed;
                _bracketCurrentPositions[i] = Vector2.MoveTowards(
                    _bracketCurrentPositions[i], _bracketTargetPositions[i], useSpeed);

                _bracketRects[i].anchoredPosition = _bracketCurrentPositions[i];
            }
        }

        private void SetVisible(bool visible)
        {
            if (_cornerBrackets == null) return;
            foreach (var bracket in _cornerBrackets)
            {
                if (bracket != null)
                    bracket.gameObject.SetActive(visible);
            }
            if (_highlightText != null)
                _highlightText.gameObject.SetActive(visible && _currentConfig != null && _currentConfig.showText);
        }

        #endregion

        #region Public API

        public void RefreshLayerMask() => CalculateCombinedLayerMask();

        // Diagnostic accessors
        public Camera PlayerCamera => _playerCamera;
        public bool IsHighlightActive => _isHighlightActive;
        public GameObject CurrentTargetObject => _currentTargetObject;
        public float CurrentAlpha => _currentAlpha;
        public float TargetAlpha => _targetAlpha;
        public LayerMask CombinedLayerMask => _combinedLayerMask;
        public List<LayerHighlightConfig> LayerConfigs => _layerConfigs;
        public CanvasGroup HighlightCanvasGroup => _canvasGroup;
        public Image[] CornerBrackets => _cornerBrackets;
        public RectTransform HighlightFrame => _canvasRect;

        #endregion
    }

    #region Data Classes

    [System.Serializable]
    public class LayerHighlightConfig
    {
        [Header("Layer Settings")]
        public string configName = "New Config";
        public bool enabled = true;
        public int layer = 0;

        [Header("Visual Settings")]
        public bool showBrackets = true;
        public bool showText = true;
        public string displayText = "Interact";

        [Header("Bracket Settings")]
        public Color bracketColor = new Color(0.2f, 0.8f, 1f, 1f);
        public float bracketSize = 20f;
        public Sprite bracketSprite;

        [Header("Text Settings")]
        public Color textColor = Color.white;
        public int textFontSize = 16;

        [Header("Crosshair Behavior")]
        [Tooltip("Should the crosshair fade out when highlighting this object type?")]
        public bool fadeCrosshair = true;
    }

    #endregion
}
