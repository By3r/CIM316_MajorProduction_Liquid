using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Windows-style right-click context menu for inventory items.
    /// Shows options like Drop, Examine, etc.
    /// </summary>
    public class ItemContextMenu : MonoBehaviour
    {
        #region Events

        public event Action<int> OnDropRequested;
        public event Action<int> OnExamineRequested;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private RectTransform _menuPanel;
        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _examineButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Settings")]
        [SerializeField] private float _fadeSpeed = 10f;
        [SerializeField] private Vector2 _menuOffset = new Vector2(5f, -5f);

        #endregion

        #region Private Fields

        private int _currentSlotIndex = -1;
        private bool _isOpen = false;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;

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

            SetupButtons();
            Hide();
        }

        private void Update()
        {
            // Close menu when clicking outside
            if (_isOpen && Input.GetMouseButtonDown(0))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_menuPanel, Input.mousePosition, _parentCanvas.worldCamera))
                {
                    Hide();
                }
            }

            // Close menu on escape
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        #endregion

        #region Setup

        private void SetupButtons()
        {
            if (_dropButton != null)
            {
                _dropButton.onClick.AddListener(OnDropClicked);
            }

            if (_examineButton != null)
            {
                _examineButton.onClick.AddListener(OnExamineClicked);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the context menu at the specified screen position for the given slot.
        /// </summary>
        public void Show(int slotIndex, Vector2 screenPosition)
        {
            _currentSlotIndex = slotIndex;

            // Position the menu at click location
            PositionMenu(screenPosition);

            _menuPanel.gameObject.SetActive(true);
            _isOpen = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
        }

        /// <summary>
        /// Hides the context menu.
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
        }

        #endregion

        #region Private Methods

        private void PositionMenu(Vector2 screenPosition)
        {
            if (_menuPanel == null || _parentCanvas == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRectTransform,
                screenPosition,
                _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera,
                out localPoint
            );

            // Apply offset
            localPoint += _menuOffset;

            // Clamp to stay within canvas bounds
            Vector2 menuSize = _menuPanel.sizeDelta;
            Vector2 canvasSize = _canvasRectTransform.sizeDelta;

            float halfCanvasWidth = canvasSize.x * 0.5f;
            float halfCanvasHeight = canvasSize.y * 0.5f;

            // Prevent menu from going off screen
            if (localPoint.x + menuSize.x > halfCanvasWidth)
            {
                localPoint.x = halfCanvasWidth - menuSize.x;
            }
            if (localPoint.y - menuSize.y < -halfCanvasHeight)
            {
                localPoint.y = -halfCanvasHeight + menuSize.y;
            }

            _menuPanel.anchoredPosition = localPoint;
        }

        private void OnDropClicked()
        {
            if (_currentSlotIndex >= 0)
            {
                OnDropRequested?.Invoke(_currentSlotIndex);
            }
            Hide();
        }

        private void OnExamineClicked()
        {
            if (_currentSlotIndex >= 0)
            {
                OnExamineRequested?.Invoke(_currentSlotIndex);
            }
            Hide();
        }

        #endregion
    }
}
