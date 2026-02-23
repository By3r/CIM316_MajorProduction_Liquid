using UnityEngine;
using UnityEngine.EventSystems;

namespace _Scripts.Systems.DebugConsole
{
    /// <summary>
    /// Drag handle for resizing the debug console panel.
    /// Attach this to a thin UI element positioned at the bottom edge of the console panel.
    /// Dragging it vertically resizes the panel height.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ConsoleResizeHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Serialized Fields

        [Header("Target")]
        [Tooltip("The console panel RectTransform to resize. If null, uses parent.")]
        [SerializeField] private RectTransform _targetPanel;

        [Header("Constraints")]
        [SerializeField] private float _minHeight = 150f;
        [SerializeField] private float _maxHeight = 900f;

        [Header("Cursor Feedback")]
        [Tooltip("Optional cursor texture for resize feedback. Leave null for default.")]
        [SerializeField] private Texture2D _resizeCursor;

        #endregion

        #region Private Fields

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private bool _isDragging;
        private float _dragStartY;
        private float _panelStartHeight;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (_targetPanel == null && transform.parent != null)
                _targetPanel = transform.parent.GetComponent<RectTransform>();

            _parentCanvas = GetComponentInParent<Canvas>();
        }

        #endregion

        #region Pointer Events (Cursor Feedback)

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_resizeCursor != null)
                Cursor.SetCursor(_resizeCursor, new Vector2(16, 16), CursorMode.Auto);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isDragging)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        #endregion

        #region Drag Events

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_targetPanel == null) return;

            _isDragging = true;
            _dragStartY = GetPointerWorldY(eventData);
            _panelStartHeight = _targetPanel.sizeDelta.y;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _targetPanel == null) return;

            float currentY = GetPointerWorldY(eventData);
            float delta = currentY - _dragStartY;

            // Console anchored at top — dragging the bottom edge DOWN increases height
            // (pointer moves down = negative delta = we INCREASE height)
            float newHeight = Mathf.Clamp(_panelStartHeight - delta, _minHeight, _maxHeight);

            Vector2 sizeDelta = _targetPanel.sizeDelta;
            sizeDelta.y = newHeight;
            _targetPanel.sizeDelta = sizeDelta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        #endregion

        #region Helpers

        private float GetPointerWorldY(PointerEventData eventData)
        {
            if (_parentCanvas == null) return eventData.position.y;

            // Handle both Screen Space - Overlay and Screen Space - Camera canvases
            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return eventData.position.y;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            return localPoint.y;
        }

        #endregion
    }
}
