using System;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.Systems.Terminal.UI;

namespace _Scripts.Systems.Terminal
{
    /// <summary>
    /// Master controller for the safe room terminal screen.
    /// Manages tab switching between Fabrication and Elevator Control.
    /// Power cell state gates fabrication access and elevator breach capability.
    /// </summary>
    public class SafeRoomTerminalUI : MonoBehaviour
    {
        #region Singleton

        public static SafeRoomTerminalUI Instance { get; private set; }

        #endregion

        #region Enums

        public enum TerminalTab
        {
            Fabrication,
            ElevatorControl
        }

        #endregion

        #region Events

        public event Action<int> OnTravelConfirmed;
        public event Action<int> OnCraftConfirmed;

        #endregion

        #region Serialized Fields

        [Header("Screen Interaction")]
        [Tooltip("The camera that renders this canvas to the Render Texture.")]
        [SerializeField] private Camera _terminalUICamera;
        [Tooltip("The GraphicRaycaster on this canvas (auto-grabbed if not assigned).")]
        [SerializeField] private GraphicRaycaster _graphicRaycaster;
        [Tooltip("The cursor Image RectTransform on this canvas.")]
        [SerializeField] private RectTransform _cursorRect;

        [Header("Tab Buttons")]
        [SerializeField] private Button _fabricationTabButton;
        [SerializeField] private Button _elevatorTabButton;

        [Header("Tab Visuals")]
        [SerializeField] private Image _fabTabDot;
        [SerializeField] private Image _elevTabDot;
        [SerializeField] private GameObject _fabTabDivider;
        [SerializeField] private GameObject _elevTabDivider;

        [Header("Pages")]
        [SerializeField] private GameObject _fabricationPage;
        [SerializeField] private GameObject _elevatorPage;

        [Header("Panel Controllers")]
        [SerializeField] private FabricationPanelUI _fabricationPanel;
        [SerializeField] private ElevatorControlPanelUI _elevatorPanel;

        [Header("Dot Colors")]
        [SerializeField] private Color _activeDotColor   = new Color(0.20f, 1.00f, 0.53f, 1.00f);
        [SerializeField] private Color _inactiveDotColor = new Color(0.25f, 0.19f, 0.00f, 1.00f);
        [SerializeField] private Color _disabledDotColor = new Color(0.60f, 0.13f, 0.13f, 1.00f);

        #endregion

        #region Private Fields

        private TerminalTab _activeTab = TerminalTab.Fabrication;
        private bool _hasPowerCell;

        #endregion

        #region Properties

        public TerminalTab ActiveTab => _activeTab;
        public bool HasPowerCell => _hasPowerCell;
        public FabricationPanelUI FabricationPanel => _fabricationPanel;
        public ElevatorControlPanelUI ElevatorPanel => _elevatorPanel;

        public Camera TerminalUICamera => _terminalUICamera;
        public GraphicRaycaster GraphicRaycaster => _graphicRaycaster;
        public RectTransform CursorRect => _cursorRect;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Auto-grab GraphicRaycaster if not assigned
            if (_graphicRaycaster == null)
                _graphicRaycaster = GetComponentInParent<Canvas>()?.GetComponent<GraphicRaycaster>();

            // Wire tab buttons
            if (_fabricationTabButton != null)
                _fabricationTabButton.onClick.AddListener(() => SwitchTab(TerminalTab.Fabrication));

            if (_elevatorTabButton != null)
                _elevatorTabButton.onClick.AddListener(() => SwitchTab(TerminalTab.ElevatorControl));

            // Wire panel events
            if (_elevatorPanel != null)
                _elevatorPanel.OnTravelConfirmed += HandleTravelConfirmed;

            if (_fabricationPanel != null)
                _fabricationPanel.OnCraftConfirmed += HandleCraftConfirmed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_fabricationTabButton != null)
                _fabricationTabButton.onClick.RemoveAllListeners();

            if (_elevatorTabButton != null)
                _elevatorTabButton.onClick.RemoveAllListeners();

            if (_elevatorPanel != null)
                _elevatorPanel.OnTravelConfirmed -= HandleTravelConfirmed;

            if (_fabricationPanel != null)
                _fabricationPanel.OnCraftConfirmed -= HandleCraftConfirmed;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the power cell state. Disables fabrication tab when no cell.
        /// </summary>
        public void SetPowerCellState(bool hasPowerCell)
        {
            _hasPowerCell = hasPowerCell;
            RefreshTabs();

            // If on fabrication tab with no power cell, switch to elevator
            if (!_hasPowerCell && _activeTab == TerminalTab.Fabrication)
                SwitchTab(TerminalTab.ElevatorControl);
        }

        /// <summary>
        /// Switches the active tab.
        /// </summary>
        public void SwitchTab(TerminalTab tab)
        {
            // Block switching to fabrication without power cell
            if (tab == TerminalTab.Fabrication && !_hasPowerCell)
                return;

            _activeTab = tab;
            RefreshTabs();
            RefreshPages();
        }

        #endregion

        #region Private Methods

        private void RefreshTabs()
        {
            bool fabActive = _activeTab == TerminalTab.Fabrication;
            bool fabEnabled = _hasPowerCell;

            // Fabrication tab: dot + divider
            if (_fabTabDot != null)
            {
                if (!fabEnabled)
                    _fabTabDot.color = _disabledDotColor;
                else
                    _fabTabDot.color = fabActive ? _activeDotColor : _inactiveDotColor;
            }

            if (_fabTabDivider != null)
                _fabTabDivider.SetActive(!fabActive);

            if (_fabricationTabButton != null)
                _fabricationTabButton.interactable = fabEnabled;

            // Elevator tab: dot + divider
            bool elevActive = _activeTab == TerminalTab.ElevatorControl;

            if (_elevTabDot != null)
                _elevTabDot.color = elevActive ? _activeDotColor : _inactiveDotColor;

            if (_elevTabDivider != null)
                _elevTabDivider.SetActive(!elevActive);
        }

        private void RefreshPages()
        {
            bool showFab = _activeTab == TerminalTab.Fabrication && _hasPowerCell;

            if (_fabricationPage != null)
                _fabricationPage.SetActive(showFab);

            if (_elevatorPage != null)
                _elevatorPage.SetActive(_activeTab == TerminalTab.ElevatorControl);
        }

        private void HandleTravelConfirmed(int floor)
        {
            OnTravelConfirmed?.Invoke(floor);
        }

        private void HandleCraftConfirmed(int recipeIndex)
        {
            OnCraftConfirmed?.Invoke(recipeIndex);
        }

        #endregion
    }
}
