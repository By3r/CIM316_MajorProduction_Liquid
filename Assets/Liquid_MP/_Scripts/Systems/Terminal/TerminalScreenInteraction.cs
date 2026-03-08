using System.Collections.Generic;
using _Scripts.Systems.Inventory;
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
    /// mouse clicks are forwarded to the UI via ExecuteEvents.
    ///
    /// No "interaction mode" — just walk up, look, and click.
    ///
    /// Attach this to the ScreenQuad. References are pulled from
    /// SafeRoomTerminalUI.Instance at runtime — no cross-scene serialized fields.
    ///
    /// Hit detection uses manual canvas-space containment checks instead of
    /// GraphicRaycaster, which doesn't work reliably with Render Texture cameras.
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

        [Header("Cursor Sprites")]
        [Tooltip("Default cursor sprite (arrow).")]
        [SerializeField] private Sprite _defaultCursorSprite;
        [Tooltip("Pointer cursor sprite shown when hovering a clickable element.")]
        [SerializeField] private Sprite _pointerCursorSprite;

        #endregion

        #region Private Fields

        private Camera _mainCamera;
        private MeshCollider _screenCollider;
        private RectTransform _cursorRect;
        private Image _cursorImage;
        private RectTransform _canvasRect;

        private PointerEventData _pointerData;

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
            // If the player was looking at the terminal when it was destroyed
            // (e.g. during floor transition), exit terminal mode so weapons
            // aren't permanently blocked.
            if (_isPointerOnScreen)
            {
                _isPointerOnScreen = false;

                var equipment = PlayerEquipment.Instance;
                if (equipment != null)
                    equipment.ExitTerminalMode();
            }

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

            _cursorRect = terminal.CursorRect;
            _cursorImage = _cursorRect != null ? _cursorRect.GetComponent<Image>() : null;

            // Get the canvas RectTransform from the terminal's GraphicRaycaster or canvas
            var raycaster = terminal.GraphicRaycaster;
            _canvasRect = raycaster != null
                ? raycaster.GetComponent<RectTransform>()
                : null;

            // Hide cursor on init
            if (_cursorRect != null)
                _cursorRect.gameObject.SetActive(false);

            _pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };

            _isInitialized = _screenCollider != null && _canvasRect != null;

            if (!_isInitialized)
                Debug.LogWarning("[TerminalScreenInteraction] Missing references from SafeRoomTerminalUI.");
        }

        private void HandleScreenHit(Vector2 uv)
        {
            // Apply UV flipping if needed (depends on quad orientation)
            float u = _flipU ? 1f - uv.x : uv.x;
            float v = _flipV ? 1f - uv.y : uv.y;

            // Convert UV to canvas-space coordinates for cursor positioning.
            Vector2 canvasSize = _canvasRect.rect.size;
            Vector2 canvasPos = new Vector2(u * canvasSize.x, v * canvasSize.y);

            // Show cursor and move it.
            // Pivot is (0,0) = bottom-left. Shift down by cursor height so the
            // top-left of the image (the pointer tip) aligns with canvasPos.
            if (_cursorRect != null)
            {
                if (!_cursorRect.gameObject.activeSelf)
                    _cursorRect.gameObject.SetActive(true);

                _cursorRect.anchoredPosition = new Vector2(
                    canvasPos.x,
                    canvasPos.y - _cursorRect.rect.height);
            }

            // Detect entering the screen — holster weapon, block weapon input
            if (!_isPointerOnScreen)
            {
                _isPointerOnScreen = true;

                var equipment = PlayerEquipment.Instance;
                if (equipment != null)
                    equipment.EnterTerminalMode();
            }

            // Find the topmost UI element at this canvas position
            // Uses manual canvas-space containment (bypasses GraphicRaycaster
            // which doesn't work reliably with RT cameras).
            GameObject rawHit = FindHitAtCanvasPosition(canvasPos);

            // Bubble up to find the actual event handler (e.g. Button parent of Text child)
            GameObject newHandler = rawHit != null
                ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(rawHit)
                : null;

            // Handle hover state changes (PointerEnter / PointerExit)
            if (newHandler != _currentHovered)
            {
                if (_currentHovered != null)
                    ExecuteEvents.ExecuteHierarchy(_currentHovered, _pointerData, ExecuteEvents.pointerExitHandler);

                if (newHandler != null)
                    ExecuteEvents.ExecuteHierarchy(newHandler, _pointerData, ExecuteEvents.pointerEnterHandler);

                _currentHovered = newHandler;
                UpdateCursorSprite();
            }

            // Handle mouse clicks
            HandleMouseInput();
        }

        private void HandleScreenExit()
        {
            if (!_isPointerOnScreen) return;

            _isPointerOnScreen = false;

            // Exit terminal mode — unblock weapon input, draw weapon if appropriate
            var equipment = PlayerEquipment.Instance;
            if (equipment != null)
                equipment.ExitTerminalMode();

            // Hide cursor and reset sprite
            if (_cursorRect != null)
                _cursorRect.gameObject.SetActive(false);
            SetCursorSprite(_defaultCursorSprite);

            // Send exit to hovered element
            if (_currentHovered != null)
            {
                ExecuteEvents.ExecuteHierarchy(_currentHovered, _pointerData, ExecuteEvents.pointerExitHandler);
                _currentHovered = null;
            }

            // If player was holding a press and looked away, release it
            if (_pressedObject != null)
            {
                ExecuteEvents.ExecuteHierarchy(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);
                _pointerData.pointerPress = null;
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
                _pointerData.pointerPress = _pressedObject;

                ExecuteEvents.ExecuteHierarchy(_pressedObject, _pointerData, ExecuteEvents.pointerDownHandler);
            }

            // Pointer Up — release press
            if (mouse.leftButton.wasReleasedThisFrame && _pressedObject != null)
            {
                ExecuteEvents.ExecuteHierarchy(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);

                // Fire click only if released on the same object that was pressed
                if (_pressedObject == _currentHovered)
                {
                    ExecuteEvents.ExecuteHierarchy(_pressedObject, _pointerData, ExecuteEvents.pointerClickHandler);
                }

                _pointerData.pointerPress = null;
                _pressedObject = null;
            }

            // Scroll — forward to any ScrollRect under the cursor
            Vector2 scrollDelta = mouse.scroll.ReadValue();
            if (scrollDelta.y != 0f && _currentHovered != null)
            {
                _pointerData.scrollDelta = scrollDelta;
                ExecuteEvents.ExecuteHierarchy(_currentHovered, _pointerData, ExecuteEvents.scrollHandler);
            }
        }

        /// <summary>
        /// Finds the topmost UI Graphic at the given canvas position.
        /// Converts everything to canvas local space using GetWorldCorners,
        /// bypassing GraphicRaycaster which doesn't work with RT cameras.
        /// </summary>
        /// <param name="canvasPos">Position in canvas coordinates (bottom-left origin).</param>
        private GameObject FindHitAtCanvasPosition(Vector2 canvasPos)
        {
            // Convert canvasPos (bottom-left origin) to canvas local space (centered at pivot).
            Rect canvasRect = _canvasRect.rect;
            Vector2 testPoint = new Vector2(
                canvasRect.x + canvasPos.x,
                canvasRect.y + canvasPos.y);

            GameObject cursorGO = _cursorRect != null ? _cursorRect.gameObject : null;
            Graphic bestHit = null;
            int bestDepth = int.MinValue;

            var graphics = _canvasRect.GetComponentsInChildren<Graphic>();
            Vector3[] corners = new Vector3[4];

            foreach (var graphic in graphics)
            {
                if (!graphic.raycastTarget) continue;
                if (!graphic.gameObject.activeInHierarchy) continue;
                if (graphic.canvasRenderer.cull) continue;
                if (graphic.depth <= bestDepth) continue;
                if (graphic.gameObject == cursorGO) continue;

                // Get world corners and convert to canvas local space.
                // This avoids any camera projection — pure transform math.
                graphic.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                    corners[i] = _canvasRect.InverseTransformPoint(corners[i]);

                // corners[0]=bottom-left, [2]=top-right
                float minX = corners[0].x;
                float minY = corners[0].y;
                float maxX = corners[2].x;
                float maxY = corners[2].y;

                if (testPoint.x >= minX && testPoint.x <= maxX &&
                    testPoint.y >= minY && testPoint.y <= maxY)
                {
                    bestDepth = graphic.depth;
                    bestHit = graphic;
                }
            }

            return bestHit != null ? bestHit.gameObject : null;
        }

        private void UpdateCursorSprite()
        {
            bool isClickable = _currentHovered != null
                               && _currentHovered.GetComponent<Selectable>() != null
                               && _currentHovered.GetComponent<Selectable>().interactable;

            SetCursorSprite(isClickable ? _pointerCursorSprite : _defaultCursorSprite);
        }

        private void SetCursorSprite(Sprite sprite)
        {
            if (_cursorImage != null && sprite != null)
                _cursorImage.sprite = sprite;
        }

        #endregion
    }
}
