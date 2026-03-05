using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _Scripts.Systems.Terminal
{
    /// <summary>
    /// Routine-style terminal interaction. The player's crosshair (screen center)
    /// raycasts into the world. When it hits the terminal's ScreenQuad, the hit UV
    /// is converted to canvas coordinates, a cursor is shown on the screen, and
    /// mouse clicks are forwarded to the UI via GraphicRaycaster.
    ///
    /// No "interaction mode" — just walk up, look, and click.
    ///
    /// Attach this to the ScreenQuad. References are pulled from
    /// SafeRoomTerminalUI.Instance at runtime — no cross-scene serialized fields.
    /// </summary>
    public class TerminalScreenInteraction : MonoBehaviour
    {
        #region Singleton

        public static TerminalScreenInteraction Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Settings")]
        [SerializeField] private float _maxDistance = 2.5f;

        [Header("UV Correction")]
        [Tooltip("Flip the horizontal UV if the cursor moves opposite to your look direction.")]
        [SerializeField] private bool _flipU;
        [Tooltip("Flip the vertical UV if the cursor is inverted vertically.")]
        [SerializeField] private bool _flipV;

        #endregion

        #region Private Fields

        private Camera _mainCamera;
        private MeshCollider _screenCollider;
        private Camera _terminalUICamera;
        private GraphicRaycaster _graphicRaycaster;
        private RectTransform _cursorRect;
        private RectTransform _canvasRect;
        private RenderTexture _renderTexture;

        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _raycastResults = new();

        private GameObject _currentHovered;
        private GameObject _pressedObject;
        private bool _isPointerOnScreen;
        private bool _isInitialized;

        #endregion

        #region Properties

        /// <summary>
        /// True when the player's crosshair is pointing at the terminal screen.
        /// Weapon systems should check this to suppress fire.
        /// </summary>
        public bool IsPointerOnScreen => _isPointerOnScreen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Always let the newest instance win — the old one may be from a
            // previous floor / scene that hasn't been cleaned up yet.
            Instance = this;

            // MeshCollider is on this same GameObject (the ScreenQuad)
            _screenCollider = GetComponent<MeshCollider>();
            if (_screenCollider == null)
                Debug.LogError("[TerminalScreenInteraction] No MeshCollider on this GameObject!");
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            ResolveReferences();
        }

        private void Update()
        {
            // Lazy init — SafeRoomTerminalUI might spawn after us
            if (!_isInitialized)
            {
                ResolveReferences();
                if (!_isInitialized) return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Raycast from crosshair (screen center) forward
            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance)
                && hit.collider == _screenCollider)
            {
                HandleScreenHit(hit.textureCoord);
            }
            else
            {
                HandleScreenExit();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Pulls references from SafeRoomTerminalUI.Instance.
        /// Called in Start and retried each frame until resolved.
        /// </summary>
        private void ResolveReferences()
        {
            var terminal = SafeRoomTerminalUI.Instance;
            if (terminal == null) return;

            _terminalUICamera = terminal.TerminalUICamera;
            _graphicRaycaster = terminal.GraphicRaycaster;
            _cursorRect = terminal.CursorRect;
            _canvasRect = _graphicRaycaster != null
                ? _graphicRaycaster.GetComponent<RectTransform>()
                : null;

            if (_terminalUICamera != null)
                _renderTexture = _terminalUICamera.targetTexture;

            // Hide cursor on init
            if (_cursorRect != null)
                _cursorRect.gameObject.SetActive(false);

            _pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };

            _isInitialized = _screenCollider != null
                          && _terminalUICamera != null
                          && _renderTexture != null
                          && _graphicRaycaster != null;

            if (!_isInitialized)
                Debug.LogWarning("[TerminalScreenInteraction] Missing references from SafeRoomTerminalUI.");
        }

        private void HandleScreenHit(Vector2 uv)
        {
            // Apply UV flipping if needed (depends on quad orientation)
            float u = _flipU ? 1f - uv.x : uv.x;
            float v = _flipV ? 1f - uv.y : uv.y;

            // Convert UV to canvas-space coordinates for cursor positioning.
            // Canvas rect size may differ from RT pixel size if Canvas Scaler
            // uses a different reference resolution.
            Vector2 canvasSize = _canvasRect != null
                ? _canvasRect.rect.size
                : new Vector2(_renderTexture.width, _renderTexture.height);

            Vector2 canvasPos = new Vector2(u * canvasSize.x, v * canvasSize.y);

            // Show cursor and move it
            // Cursor anchors should be at bottom-left (0,0) so anchoredPosition
            // maps directly to position from bottom-left of the canvas.
            if (_cursorRect != null)
            {
                if (!_cursorRect.gameObject.activeSelf)
                    _cursorRect.gameObject.SetActive(true);

                _cursorRect.anchoredPosition = canvasPos;
            }

            _isPointerOnScreen = true;

            // GraphicRaycaster expects coordinates in RT pixel space
            Vector2 pixelPos = new Vector2(
                u * _renderTexture.width,
                v * _renderTexture.height
            );
            _pointerData.position = pixelPos;
            _raycastResults.Clear();
            _graphicRaycaster.Raycast(_pointerData, _raycastResults);

            // Find the topmost hit (skip the cursor itself)
            GameObject newHovered = FindTopHit();

            // Handle hover state changes (PointerEnter / PointerExit)
            if (newHovered != _currentHovered)
            {
                if (_currentHovered != null)
                    ExecuteEvents.Execute(_currentHovered, _pointerData, ExecuteEvents.pointerExitHandler);

                if (newHovered != null)
                    ExecuteEvents.Execute(newHovered, _pointerData, ExecuteEvents.pointerEnterHandler);

                _currentHovered = newHovered;
            }

            // Handle mouse clicks
            HandleMouseInput();
        }

        private void HandleScreenExit()
        {
            if (!_isPointerOnScreen) return;

            _isPointerOnScreen = false;

            // Hide cursor
            if (_cursorRect != null)
                _cursorRect.gameObject.SetActive(false);

            // Send exit to hovered element
            if (_currentHovered != null)
            {
                ExecuteEvents.Execute(_currentHovered, _pointerData, ExecuteEvents.pointerExitHandler);
                _currentHovered = null;
            }

            // If player was holding a press and looked away, release it
            if (_pressedObject != null)
            {
                ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);
                _pressedObject = null;
            }
        }

        private void HandleMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Pointer Down — start press
            if (mouse.leftButton.wasPressedThisFrame && _currentHovered != null)
            {
                _pressedObject = _currentHovered;

                _pointerData.pointerPressRaycast = _raycastResults.Count > 0
                    ? _raycastResults[0]
                    : new RaycastResult();

                ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerDownHandler);
            }

            // Pointer Up — release press
            if (mouse.leftButton.wasReleasedThisFrame && _pressedObject != null)
            {
                ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);

                // Fire click only if released on the same object that was pressed
                if (_pressedObject == _currentHovered)
                {
                    ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerClickHandler);
                }

                _pressedObject = null;
            }
        }

        private GameObject FindTopHit()
        {
            GameObject cursorGO = _cursorRect != null ? _cursorRect.gameObject : null;

            foreach (var result in _raycastResults)
            {
                // Skip the cursor image itself
                if (result.gameObject == cursorGO) continue;

                return result.gameObject;
            }

            return null;
        }

        #endregion
    }
}
