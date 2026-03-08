using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Dynamic right-click context menu for inventory items.
    /// Buttons are spawned at runtime from a prefab based on the action list
    /// provided by InventoryUI (which decides actions per item type).
    /// </summary>
    public class ItemContextMenu : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private RectTransform _menuPanel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Dynamic Buttons")]
        [Tooltip("Prefab with a Button + TMP child. Spawned once per action.")]
        [SerializeField] private GameObject _buttonPrefab;
        [Tooltip("Parent transform with a VerticalLayoutGroup for spawned buttons.")]
        [SerializeField] private Transform _buttonContainer;

        #endregion

        #region Private Fields

        private int _currentSlotIndex = -1;
        private bool _isOpen;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = _menuPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _menuPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Hide();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Close menu when clicking outside
            if (Input.GetMouseButtonDown(0))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        _menuPanel, Input.mousePosition, _parentCanvas.worldCamera))
                {
                    Hide();
                }
            }

            // Close menu on escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the context menu with the given actions at the specified screen position.
        /// Each action becomes a button in the menu.
        /// </summary>
        public void Show(int slotIndex, Vector2 screenPosition, List<ContextMenuAction> actions)
        {
            _currentSlotIndex = slotIndex;

            // Activate before spawning so layout can calculate
            _menuPanel.gameObject.SetActive(true);

            ClearButtons();
            SpawnButtons(actions);

            // Force layout rebuild so ContentSizeFitter updates the panel size
            // before we read it for clamping
            LayoutRebuilder.ForceRebuildLayoutImmediate(_menuPanel);

            // Pivot top-left so the menu opens downward-right from the cursor (Windows-style)
            _menuPanel.pivot = new Vector2(0f, 1f);

            PositionMenu(screenPosition);

            _isOpen = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
        }

        /// <summary>
        /// Hides the context menu and clears spawned buttons.
        /// </summary>
        public void Hide()
        {
            _isOpen = false;
            _currentSlotIndex = -1;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            _menuPanel.gameObject.SetActive(false);
            ClearButtons();
        }

        #endregion

        #region Private Methods

        private void SpawnButtons(List<ContextMenuAction> actions)
        {
            if (_buttonPrefab == null || _buttonContainer == null || actions == null) return;

            foreach (var action in actions)
            {
                GameObject go = Instantiate(_buttonPrefab, _buttonContainer);
                go.SetActive(true);

                // Set label text
                var label = go.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = action.Label;

                // Wire click → callback + hide
                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    var callback = action.Callback;
                    int slot = _currentSlotIndex;
                    button.onClick.AddListener(() =>
                    {
                        callback?.Invoke(slot);
                        Hide();
                    });
                }

                _spawnedButtons.Add(go);
            }
        }

        private void ClearButtons()
        {
            foreach (var go in _spawnedButtons)
            {
                if (go != null) Destroy(go);
            }
            _spawnedButtons.Clear();
        }

        private void PositionMenu(Vector2 screenPosition)
        {
            if (_menuPanel == null || _parentCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRectTransform,
                screenPosition,
                _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera,
                out Vector2 localPoint
            );

            // With pivot (0, 1) the menu extends right (+x) and down (-y) from localPoint.
            // Clamp so it stays within the canvas.
            Vector2 menuSize = _menuPanel.rect.size;
            Vector2 canvasSize = _canvasRectTransform.rect.size;

            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;

            // Right edge: anchor.x + width must stay within +halfW
            if (localPoint.x + menuSize.x > halfW)
                localPoint.x = halfW - menuSize.x;

            // Left edge: anchor.x must stay within -halfW
            if (localPoint.x < -halfW)
                localPoint.x = -halfW;

            // Top edge: anchor.y must stay within +halfH
            if (localPoint.y > halfH)
                localPoint.y = halfH;

            // Bottom edge: anchor.y - height must stay within -halfH
            if (localPoint.y - menuSize.y < -halfH)
                localPoint.y = -halfH + menuSize.y;

            _menuPanel.anchoredPosition = localPoint;
        }

        #endregion
    }
}
