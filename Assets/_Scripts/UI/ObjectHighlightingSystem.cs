using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.UI.Interaction
{
    /// <summary>
    /// Layer-based object highlighting system with configurable settings per object type.
    /// Uses raycasting to detect objects on configured layers and displays customizable highlights.
    /// Supports different visual styles (brackets, text, colors) per layer.
    /// Integrates with CrosshairManager for morphing crosshair effect.
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
        [SerializeField] private float _animationSpeed = 12f;
        [SerializeField] private float _fadeSpeed = 15f;
        
        [Header("Frame Settings")]
        [SerializeField] private float _minFrameSize = 60f;
        [SerializeField] private float _framePadding = 15f;
        
        [Header("UI References (Auto-created if not assigned)")]
        [SerializeField] private Canvas _highlightCanvas;
        [SerializeField] private RectTransform _highlightFrame;
        [SerializeField] private TextMeshProUGUI _highlightText;
        [SerializeField] private Image[] _cornerBrackets;
        
        [Header("Crosshair Integration")]
        [Tooltip("Reference to CrosshairManager for morphing effect")]
        [SerializeField] private CrosshairManager _crosshairManager;
        
        [Tooltip("Global toggle: Fade out crosshair when highlighting objects")]
        [SerializeField] private bool _fadeCrosshairOnHighlight = true;
        
        [Tooltip("How fast the crosshair fades (higher = faster, 100+ recommended for instant feel)")]
        [SerializeField] private float _crosshairFadeSpeed = 100f;

        [Tooltip("Animate brackets from screen center (crosshair position) to corners")]
        [SerializeField] private bool _animateBracketsFromCenter = true;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private GameObject _currentTargetObject;
        private LayerHighlightConfig _currentConfig;
        private Renderer _targetRenderer;
        private Collider _targetCollider;
        
        private Vector2 _targetFramePosition;
        private Vector2 _targetFrameSize;
        private Vector2 _currentFramePosition;
        private Vector2 _currentFrameSize;
        private float _currentAlpha = 0f;
        private float _targetAlpha = 0f;
        
        private CanvasGroup _highlightCanvasGroup;
        private bool _isHighlightActive = false;
        
        private LayerMask _combinedLayerMask;
        
        // Bracket animation from center
        private Vector2[] _bracketStartPositions;
        private Vector2[] _bracketTargetPositions;
        private bool _justActivated = false;
        
        // Crosshair fade tracking
        private float _targetCrosshairAlpha = 1f;
        private float _currentCrosshairAlpha = 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupUIComponents();
            CalculateCombinedLayerMask();
            
            _bracketStartPositions = new Vector2[4];
            _bracketTargetPositions = new Vector2[4];
        }
        
        private void Start()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            if (_playerCamera == null)
            {
                Debug.LogError("[ObjectHighlightingSystem] No camera found!");
                enabled = false;
                return;
            }
            
            SetHighlightVisibility(false);
            
            if (_showDebugLogs)
                Debug.Log("[ObjectHighlightingSystem] Initialized with " + _layerConfigs.Count + " layer configs");
        }
        
        private void Update()
        {
            CheckForHighlightTarget();
            
            if (_isHighlightActive && _currentTargetObject != null)
            {
                UpdateHighlightPosition();
            }
            
            AnimateHighlight();
        }

        #endregion

        #region Target Detection

        private void CalculateCombinedLayerMask()
        {
            _combinedLayerMask = 0;
            foreach (var config in _layerConfigs)
            {
                if (config.enabled)
                {
                    _combinedLayerMask |= (1 << config.layer);
                }
            }
        }

        private void CheckForHighlightTarget()
        {
            if (_playerCamera == null)
                return;

            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, _raycastDistance, _combinedLayerMask))
            {
                GameObject hitObject = hit.collider.gameObject;
                
                LayerHighlightConfig config = GetConfigForLayer(hitObject.layer);
                
                if (config != null && config.enabled)
                {
                    if (hitObject != _currentTargetObject)
                    {
                        ShowHighlight(hitObject, config);
                    }
                    return;
                }
            }

            if (_isHighlightActive)
            {
                HideHighlight();
            }
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

        private void ShowHighlight(GameObject targetObject, LayerHighlightConfig config)
        {
            if (targetObject == null || config == null)
                return;

            _currentTargetObject = targetObject;
            _currentConfig = config;
            _targetRenderer = targetObject.GetComponent<Renderer>();
            _targetCollider = targetObject.GetComponent<Collider>();
            
            if (_targetRenderer == null)
                _targetRenderer = targetObject.GetComponentInChildren<Renderer>();
            if (_targetCollider == null)
                _targetCollider = targetObject.GetComponentInChildren<Collider>();
            
            ApplyConfigVisuals(config);
            
            if (_highlightText != null && config.showText)
            {
                _highlightText.gameObject.SetActive(true);
                _highlightText.text = string.IsNullOrEmpty(config.displayText) ? targetObject.name : config.displayText;
                _highlightText.color = config.textColor;
                _highlightText.fontSize = config.textFontSize;
            }
            else if (_highlightText != null)
            {
                _highlightText.gameObject.SetActive(false);
            }
            
            _targetAlpha = 1f;
            _isHighlightActive = true;
            _justActivated = true;
            
            // Per-layer crosshair fade control
            if (_fadeCrosshairOnHighlight && _crosshairManager != null && config.fadeCrosshair)
            {
                _targetCrosshairAlpha = 0f;
            }
            
            if (_showDebugLogs)
                Debug.Log($"[ObjectHighlightingSystem] Highlighting '{targetObject.name}' on layer '{LayerMask.LayerToName(config.layer)}'");
        }
        
        private void HideHighlight()
        {
            _currentTargetObject = null;
            _currentConfig = null;
            _targetRenderer = null;
            _targetCollider = null;
            _targetAlpha = 0f;
            _isHighlightActive = false;
            
            // Fade crosshair back in
            if (_fadeCrosshairOnHighlight && _crosshairManager != null)
            {
                _targetCrosshairAlpha = 1f;
            }
        }

        private void ApplyConfigVisuals(LayerHighlightConfig config)
        {
            if (_cornerBrackets == null || _cornerBrackets.Length != 4)
                return;

            foreach (var bracket in _cornerBrackets)
            {
                if (bracket != null)
                {
                    bracket.gameObject.SetActive(config.showBrackets);
                    bracket.color = config.bracketColor;
                    
                    if (config.bracketSprite != null)
                    {
                        bracket.sprite = config.bracketSprite;
                    }
                    
                    RectTransform rectTransform = bracket.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.sizeDelta = new Vector2(config.bracketSize, config.bracketSize);
                    }
                }
            }
        }

        #endregion

        #region UI Setup

        private void SetupUIComponents()
        {
            if (_highlightCanvas == null)
            {
                CreateHighlightCanvas();
            }
            
            if (_highlightFrame == null)
            {
                CreateHighlightFrame();
            }
            
            _highlightCanvasGroup = _highlightFrame.GetComponent<CanvasGroup>();
            if (_highlightCanvasGroup == null)
            {
                _highlightCanvasGroup = _highlightFrame.gameObject.AddComponent<CanvasGroup>();
            }
            
            _highlightCanvasGroup.alpha = 0f;
            _highlightCanvasGroup.blocksRaycasts = false;
            _highlightCanvasGroup.interactable = false;
            
            SetupCornerBrackets();
            
            if (_highlightText == null)
            {
                CreateHighlightText();
            }
            
            DisableAllRaycastTargets();
        }
        
        private void CreateHighlightCanvas()
        {
            GameObject canvasGO = new GameObject("HighlightCanvas");
            canvasGO.transform.SetParent(transform);
            
            _highlightCanvas = canvasGO.AddComponent<Canvas>();
            _highlightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _highlightCanvas.sortingOrder = 100;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }
        
        private void CreateHighlightFrame()
        {
            GameObject frameGO = new GameObject("HighlightFrame");
            frameGO.transform.SetParent(_highlightCanvas.transform);
            
            _highlightFrame = frameGO.AddComponent<RectTransform>();
            _highlightFrame.anchorMin = new Vector2(0.5f, 0.5f);
            _highlightFrame.anchorMax = new Vector2(0.5f, 0.5f);
            _highlightFrame.pivot = new Vector2(0.5f, 0.5f);
        }
        
        private void CreateHighlightText()
        {
            GameObject textGO = new GameObject("HighlightText");
            textGO.transform.SetParent(_highlightFrame);
            
            _highlightText = textGO.AddComponent<TextMeshProUGUI>();
            _highlightText.fontSize = 16;
            _highlightText.color = Color.white;
            _highlightText.fontStyle = FontStyles.Bold;
            _highlightText.alignment = TextAlignmentOptions.Center;
            _highlightText.text = "";
            _highlightText.raycastTarget = false;
            
            RectTransform textRect = _highlightText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -10f);
            textRect.sizeDelta = new Vector2(300f, 50f);
        }
        
        private void SetupCornerBrackets()
        {
            if (_cornerBrackets == null || _cornerBrackets.Length != 4)
            {
                _cornerBrackets = new Image[4];
            }
            
            string[] bracketNames = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
            Vector2[] anchors = {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };
            float[] rotations = { 0f, -90f, 90f, 180f };
            
            for (int i = 0; i < 4; i++)
            {
                if (_cornerBrackets[i] == null)
                {
                    GameObject bracketGO = new GameObject($"Bracket_{bracketNames[i]}");
                    bracketGO.transform.SetParent(_highlightFrame);
                    
                    _cornerBrackets[i] = bracketGO.AddComponent<Image>();
                    _cornerBrackets[i].color = Color.white;
                    _cornerBrackets[i].raycastTarget = false;
                    _cornerBrackets[i].sprite = CreateBracketSprite();
                    
                    RectTransform bracketRect = bracketGO.GetComponent<RectTransform>();
                    bracketRect.anchorMin = anchors[i];
                    bracketRect.anchorMax = anchors[i];
                    bracketRect.pivot = new Vector2(0f, 1f);
                    bracketRect.sizeDelta = new Vector2(20f, 20f);
                    bracketRect.anchoredPosition = Vector2.zero;
                    bracketRect.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
                }
            }
        }
        
        private Sprite CreateBracketSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            
            int thickness = 3;
            
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < thickness; x++)
                {
                    pixels[y * 32 + x] = Color.white;
                }
            }
            
            for (int x = 0; x < 32; x++)
            {
                for (int y = 32 - thickness; y < 32; y++)
                {
                    pixels[y * 32 + x] = Color.white;
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0f, 1f));
        }
        
        private void DisableAllRaycastTargets()
        {
            if (_highlightFrame != null)
            {
                Graphic[] allGraphics = _highlightFrame.GetComponentsInChildren<Graphic>(true);
                foreach (Graphic graphic in allGraphics)
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        #endregion

        #region Position & Animation

        private void UpdateHighlightPosition()
        {
            if (_currentTargetObject == null || _playerCamera == null)
                return;
            
            Bounds bounds = GetObjectBounds();
            if (bounds.size == Vector3.zero)
                return;
            
            Vector2 screenMin = Vector2.positiveInfinity;
            Vector2 screenMax = Vector2.negativeInfinity;
            
            Vector3[] boundsCorners = GetBoundsCorners(bounds);
            bool hasValidPoints = false;
            
            foreach (Vector3 corner in boundsCorners)
            {
                Vector3 screenPoint = _playerCamera.WorldToScreenPoint(corner);
                
                if (screenPoint.z <= 0)
                    continue;
                
                hasValidPoints = true;
                screenMin.x = Mathf.Min(screenMin.x, screenPoint.x);
                screenMin.y = Mathf.Min(screenMin.y, screenPoint.y);
                screenMax.x = Mathf.Max(screenMax.x, screenPoint.x);
                screenMax.y = Mathf.Max(screenMax.y, screenPoint.y);
            }
            
            if (!hasValidPoints)
                return;
            
            RectTransform canvasRect = _highlightCanvas.GetComponent<RectTransform>();
            Vector2 canvasMin, canvasMax;
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenMin, _highlightCanvas.worldCamera, out canvasMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenMax, _highlightCanvas.worldCamera, out canvasMax);
            
            Vector2 frameSize = new Vector2(
                Mathf.Max(canvasMax.x - canvasMin.x + _framePadding * 2, _minFrameSize),
                Mathf.Max(canvasMax.y - canvasMin.y + _framePadding * 2, _minFrameSize)
            );
            
            Vector2 frameCenter = (canvasMin + canvasMax) * 0.5f;
            
            _targetFramePosition = frameCenter;
            _targetFrameSize = frameSize;
        }
        
        private Bounds GetObjectBounds()
        {
            if (_targetRenderer != null)
                return _targetRenderer.bounds;
            else if (_targetCollider != null)
                return _targetCollider.bounds;
            
            return new Bounds();
        }
        
        private Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            
            return new Vector3[]
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(+extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, +extents.y, -extents.z),
                center + new Vector3(+extents.x, +extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, +extents.z),
                center + new Vector3(+extents.x, -extents.y, +extents.z),
                center + new Vector3(-extents.x, +extents.y, +extents.z),
                center + new Vector3(+extents.x, +extents.y, +extents.z),
            };
        }
        
        private void AnimateHighlight()
        {
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            
            if (_highlightCanvasGroup != null)
            {
                _highlightCanvasGroup.alpha = _currentAlpha;
            }
            
            // Update crosshair with per-layer control
            if (_crosshairManager != null && _fadeCrosshairOnHighlight)
            {
                _currentCrosshairAlpha = Mathf.MoveTowards(_currentCrosshairAlpha, _targetCrosshairAlpha, _crosshairFadeSpeed * Time.deltaTime);
                
                bool shouldHideCrosshair = _currentCrosshairAlpha < 0.5f;
                _crosshairManager.SetExternalHideRequest(shouldHideCrosshair);
            }
            
            bool shouldShow = _currentAlpha > 0.01f;
            SetHighlightVisibility(shouldShow);
            
            // FIX: Return brackets to center when fading out completely
            if (_currentAlpha < 0.01f && _animateBracketsFromCenter)
            {
                ResetBracketsToCenter();
                return;
            }
            
            if (!shouldShow)
                return;
            
            _currentFramePosition = Vector2.MoveTowards(_currentFramePosition, _targetFramePosition, _animationSpeed * 100f * Time.deltaTime);
            _currentFrameSize = Vector2.MoveTowards(_currentFrameSize, _targetFrameSize, _animationSpeed * 100f * Time.deltaTime);
            
            if (_highlightFrame != null)
            {
                _highlightFrame.anchoredPosition = _currentFramePosition;
                _highlightFrame.sizeDelta = _currentFrameSize;
            }
            
            if (_animateBracketsFromCenter && _cornerBrackets != null && _cornerBrackets.Length == 4)
            {
                AnimateBracketsFromCenter();
            }
        }
        
        private void AnimateBracketsFromCenter()
        {
            if (_justActivated)
            {
                Vector2 screenCenter = Vector2.zero;
                
                for (int i = 0; i < 4; i++)
                {
                    if (_cornerBrackets[i] != null)
                    {
                        RectTransform bracketRect = _cornerBrackets[i].GetComponent<RectTransform>();
                        if (bracketRect != null)
                        {
                            Vector2 targetPos = GetBracketTargetPosition(i);
                            _bracketTargetPositions[i] = targetPos;
                            
                            _bracketStartPositions[i] = screenCenter;
                            bracketRect.anchoredPosition = screenCenter;
                        }
                    }
                }
                
                _justActivated = false;
            }
            
            for (int i = 0; i < 4; i++)
            {
                if (_cornerBrackets[i] != null)
                {
                    RectTransform bracketRect = _cornerBrackets[i].GetComponent<RectTransform>();
                    if (bracketRect != null)
                    {
                        Vector2 currentPos = bracketRect.anchoredPosition;
                        Vector2 targetPos = _bracketTargetPositions[i];
                        
                        Vector2 newPos = Vector2.MoveTowards(currentPos, targetPos, _animationSpeed * 150f * Time.deltaTime);
                        bracketRect.anchoredPosition = newPos;
                    }
                }
            }
        }
        
        /// <summary>
        /// Resets brackets to screen center when highlight fully fades out.
        /// This ensures they expand from center on next activation.
        /// </summary>
        private void ResetBracketsToCenter()
        {
            if (_cornerBrackets == null || _cornerBrackets.Length != 4)
                return;
                
            Vector2 screenCenter = Vector2.zero;
            
            for (int i = 0; i < 4; i++)
            {
                if (_cornerBrackets[i] != null)
                {
                    RectTransform bracketRect = _cornerBrackets[i].GetComponent<RectTransform>();
                    if (bracketRect != null)
                    {
                        bracketRect.anchoredPosition = screenCenter;
                    }
                }
            }
        }
        
        private Vector2 GetBracketTargetPosition(int bracketIndex)
        {
            return Vector2.zero;
        }
        
        private void SetHighlightVisibility(bool visible)
        {
            // Visibility controlled by CanvasGroup alpha
        }

        #endregion

        #region Public API

        /// <summary>
        /// Recalculates the combined layer mask from all enabled layer configs.
        /// Call this if you modify layer configs at runtime.
        /// </summary>
        public void RefreshLayerMask()
        {
            CalculateCombinedLayerMask();
        }

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