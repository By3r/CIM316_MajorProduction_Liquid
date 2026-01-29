using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Scripts.Core.Managers;
using _Scripts.UI;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// UI for selecting floors in the elevator.
    /// Displays a grid of floor buttons with color-coded states.
    /// Floor X and Floor 0 are blocked (red), floors start from 1.
    /// Uses singleton pattern for easy access from Elevator components.
    /// </summary>
    public class ElevatorFloorUI : MonoBehaviour
    {
        #region Singleton

        private static ElevatorFloorUI _instance;
        public static ElevatorFloorUI Instance => _instance;

        #endregion

        #region Events

        public event Action<int> OnFloorSelected;
        public event Action OnUIClosed;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _floorButtonPrefab;

        [Header("Current Floor Colors")]
        [SerializeField] private Color _currentFloorOutline = new Color(0.5f, 1f, 1f, 1f); // Light cyan
        [SerializeField] private Color _currentFloorBackground = new Color(0.3f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color _currentFloorText = Color.black;

        [Header("Other Floor Colors")]
        [SerializeField] private Color _otherFloorOutline = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey
        [SerializeField] private Color _otherFloorBackground = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _otherFloorText = Color.white;

        [Header("Opacity")]
        [Tooltip("Opacity for unvisited floors.")]
        [Range(0f, 1f)]
        [SerializeField] private float _unvisitedFloorOpacity = 0.5f;

        [Header("Settings")]
        [SerializeField] private int _totalFloors = 20;

        [Header("PowerCell Status")]
        [Tooltip("Icon that shows PowerCell status (green = has PowerCell, grey = no PowerCell).")]
        [SerializeField] private Image _powerCellIcon;
        [Tooltip("Text that shows PowerCell status message.")]
        [SerializeField] private TextMeshProUGUI _powerCellStatusText;
        [SerializeField] private Color _powerCellAvailableColor = Color.green;
        [SerializeField] private Color _powerCellUnavailableColor = Color.grey;

        #endregion

        #region Private Fields

        private int _currentFloor;
        private int _highestUnlockedFloor;
        private FloorButtonData[] _floorButtons;
        private bool _isOpen;
        private bool _justOpened; // Prevents closing on the same frame as opening
        private bool _isPowered;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;
        public int CurrentFloor => _currentFloor;
        public int HighestUnlockedFloor => _highestUnlockedFloor;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ElevatorFloorUI] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Skip closing check on the same frame the UI was opened
            // (prevents the same E keypress from immediately closing the UI)
            if (_justOpened)
            {
                _justOpened = false;
                return;
            }

            // Close UI on Escape, Tab, or E
            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.Tab) ||
                Input.GetKeyDown(KeyCode.E))
            {
                Hide();
                OnUIClosed?.Invoke();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the floor UI with the specified floor count.
        /// </summary>
        public void Initialize(int totalFloors, int currentFloor, int highestUnlockedFloor)
        {
            _totalFloors = totalFloors;
            _currentFloor = currentFloor;
            _highestUnlockedFloor = highestUnlockedFloor;

            CreateFloorButtons();
            RefreshButtonStates();
        }

        /// <summary>
        /// Opens the floor selection UI.
        /// </summary>
        public void Show(int currentFloor, int highestUnlockedFloor, bool isPowered = false)
        {
            _currentFloor = currentFloor;
            _highestUnlockedFloor = highestUnlockedFloor;
            _isPowered = isPowered;

            RefreshButtonStates();
            UpdatePowerCellStatus();

            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            _isOpen = true;
            _justOpened = true; // Prevent closing on the same frame

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Closes the floor selection UI.
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            _isOpen = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Updates the current floor display.
        /// </summary>
        public void SetCurrentFloor(int floor)
        {
            _currentFloor = floor;
            RefreshButtonStates();
        }

        /// <summary>
        /// Updates the highest unlocked floor.
        /// </summary>
        public void SetHighestUnlockedFloor(int floor)
        {
            _highestUnlockedFloor = floor;
            RefreshButtonStates();
        }

        /// <summary>
        /// Updates the PowerCell status display.
        /// </summary>
        public void SetPoweredState(bool isPowered)
        {
            _isPowered = isPowered;
            UpdatePowerCellStatus();
        }

        #endregion

        #region Private Methods

        private void UpdatePowerCellStatus()
        {
            if (_powerCellIcon != null)
            {
                _powerCellIcon.color = _isPowered ? _powerCellAvailableColor : _powerCellUnavailableColor;
            }

            if (_powerCellStatusText != null)
            {
                _powerCellStatusText.text = _isPowered ? "PowerCell Ready" : "PowerCell Required";
            }
        }

        private void CreateFloorButtons()
        {
            // Clear existing buttons
            if (_floorButtons != null)
            {
                foreach (var btn in _floorButtons)
                {
                    if (btn?.ButtonComponent != null)
                    {
                        Destroy(btn.ButtonComponent.gameObject);
                    }
                }
            }

            // Create buttons: Floor X, Floor 0, then Floor 1 to _totalFloors
            // Total buttons = 2 (X and 0) + _totalFloors
            int buttonCount = 2 + _totalFloors;
            _floorButtons = new FloorButtonData[buttonCount];

            // Floor X (index 0)
            CreateSpecialFloorButton(0, "X", -2); // -2 represents Floor X internally

            // Floor 0 (index 1)
            CreateSpecialFloorButton(1, "0", 0);

            // Floors 1 to _totalFloors (index 2 onwards)
            for (int i = 0; i < _totalFloors; i++)
            {
                int floorNumber = i + 1;
                int buttonIndex = i + 2;

                GameObject buttonObj = Instantiate(_floorButtonPrefab, _buttonContainer);
                buttonObj.name = $"Floor_{floorNumber}";

                ElevatorFloorButton btnComponent = buttonObj.GetComponent<ElevatorFloorButton>();
                if (btnComponent == null)
                {
                    Debug.LogError($"[ElevatorFloorUI] Floor button prefab missing ElevatorFloorButton component!");
                    continue;
                }

                btnComponent.SetFloorNumber(floorNumber.ToString());

                _floorButtons[buttonIndex] = new FloorButtonData
                {
                    FloorNumber = floorNumber,
                    ButtonComponent = btnComponent,
                    IsBlocked = false
                };

                int floor = floorNumber; // Capture for closure
                btnComponent.Button.onClick.AddListener(() => OnFloorButtonClicked(floor));
            }
        }

        private void CreateSpecialFloorButton(int buttonIndex, string displayText, int internalFloorNumber)
        {
            GameObject buttonObj = Instantiate(_floorButtonPrefab, _buttonContainer);
            buttonObj.name = $"Floor_{displayText}";

            ElevatorFloorButton btnComponent = buttonObj.GetComponent<ElevatorFloorButton>();
            if (btnComponent == null)
            {
                Debug.LogError($"[ElevatorFloorUI] Floor button prefab missing ElevatorFloorButton component!");
                return;
            }

            btnComponent.SetFloorNumber(displayText);
            btnComponent.SetInteractable(false);

            _floorButtons[buttonIndex] = new FloorButtonData
            {
                FloorNumber = internalFloorNumber,
                ButtonComponent = btnComponent,
                IsBlocked = true // Floor X and Floor 0 are blocked
            };
        }

        private void RefreshButtonStates()
        {
            if (_floorButtons == null) return;

            foreach (var floorBtn in _floorButtons)
            {
                if (floorBtn?.ButtonComponent == null) continue;

                FloorState state = GetFloorState(floorBtn);
                ApplyButtonStyle(floorBtn, state);
            }
        }

        private FloorState GetFloorState(FloorButtonData floorBtn)
        {
            // Blocked floors (Floor X and Floor 0)
            if (floorBtn.IsBlocked)
            {
                return FloorState.Blocked;
            }

            int floor = floorBtn.FloorNumber;

            if (floor == _currentFloor)
            {
                return FloorState.Current;
            }
            else if (floor <= _highestUnlockedFloor)
            {
                return FloorState.Visited;
            }
            else
            {
                return FloorState.Unvisited;
            }
        }

        private void ApplyButtonStyle(FloorButtonData floorBtn, FloorState state)
        {
            var btn = floorBtn.ButtonComponent;
            bool interactable = true;

            switch (state)
            {
                case FloorState.Blocked:
                    // Blocked floors (Floor X, Floor 0) use other floor colors with unvisited opacity
                    btn.ApplyOtherFloorStyle(_otherFloorOutline, _otherFloorBackground, _otherFloorText, _unvisitedFloorOpacity);
                    interactable = false;
                    break;

                case FloorState.Current:
                    btn.ApplyCurrentFloorStyle(_currentFloorOutline, _currentFloorBackground, _currentFloorText);
                    interactable = false; // Can't travel to current floor
                    break;

                case FloorState.Visited:
                    btn.ApplyOtherFloorStyle(_otherFloorOutline, _otherFloorBackground, _otherFloorText, 1f);
                    break;

                case FloorState.Unvisited:
                default:
                    // Unvisited floors use reduced opacity
                    // Only the next floor (highestUnlocked + 1) is interactable
                    bool isNextFloor = floorBtn.FloorNumber == _highestUnlockedFloor + 1;
                    btn.ApplyOtherFloorStyle(_otherFloorOutline, _otherFloorBackground, _otherFloorText, _unvisitedFloorOpacity);
                    interactable = isNextFloor;
                    break;
            }

            btn.SetInteractable(interactable);
        }

        private void OnFloorButtonClicked(int floor)
        {
            // Don't allow selecting current floor
            if (floor == _currentFloor) return;

            // Don't allow selecting floors beyond the next unlockable
            if (floor > _highestUnlockedFloor + 1) return;

            // Don't allow blocked floors (should be caught by interactable = false, but double check)
            if (floor <= 0) return;

            OnFloorSelected?.Invoke(floor);
        }

        #endregion

        #region Helper Classes

        private class FloorButtonData
        {
            public int FloorNumber;
            public ElevatorFloorButton ButtonComponent;
            public bool IsBlocked;
        }

        private enum FloorState
        {
            Blocked,
            Current,
            Visited,
            Unvisited
        }

        #endregion
    }
}
