using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Scripts.Core.Managers;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// UI for selecting floors in the elevator.
    /// Displays a grid of floor buttons with color-coded states.
    /// Floor X and Floor 0 are blocked (red), floors start from 1.
    /// </summary>
    public class ElevatorFloorUI : MonoBehaviour
    {
        #region Events

        public event Action<int> OnFloorSelected;
        public event Action OnUIClosed;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _floorButtonPrefab;

        [Header("Colors")]
        [Tooltip("Current floor the player is on.")]
        [SerializeField] private Color _currentFloorColor = new Color(0.5f, 1f, 1f, 1f); // Light cyan
        [Tooltip("Floors already visited/unlocked.")]
        [SerializeField] private Color _unlockedFloorColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey
        [Tooltip("Next floor that needs to be unlocked (requires PowerCell).")]
        [SerializeField] private Color _nextFloorColor = new Color(1f, 1f, 0.3f, 1f); // Yellow
        [Tooltip("Floors beyond the next one (locked).")]
        [SerializeField] private Color _lockedFloorColor = new Color(1f, 1f, 0.3f, 0.5f); // Yellow faded
        [Tooltip("Blocked floors (Floor X, Floor 0) - permanently inaccessible.")]
        [SerializeField] private Color _blockedFloorColor = new Color(0.8f, 0.2f, 0.2f, 1f); // Red
        [Tooltip("Text color for current floor.")]
        [SerializeField] private Color _currentFloorTextColor = Color.black;
        [Tooltip("Text color for other floors.")]
        [SerializeField] private Color _defaultTextColor = Color.white;
        [Tooltip("Text color for blocked floors.")]
        [SerializeField] private Color _blockedTextColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Dark grey

        [Header("Settings")]
        [SerializeField] private int _totalFloors = 20;

        #endregion

        #region Private Fields

        private int _currentFloor;
        private int _highestUnlockedFloor;
        private FloorButton[] _floorButtons;
        private bool _isOpen;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;
        public int CurrentFloor => _currentFloor;
        public int HighestUnlockedFloor => _highestUnlockedFloor;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

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
        public void Show(int currentFloor, int highestUnlockedFloor)
        {
            _currentFloor = currentFloor;
            _highestUnlockedFloor = highestUnlockedFloor;

            RefreshButtonStates();

            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            _isOpen = true;

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

        #endregion

        #region Private Methods

        private void CreateFloorButtons()
        {
            // Clear existing buttons
            if (_floorButtons != null)
            {
                foreach (var btn in _floorButtons)
                {
                    if (btn != null && btn.Button != null)
                    {
                        Destroy(btn.Button.gameObject);
                    }
                }
            }

            // Create buttons: Floor X, Floor 0, then Floor 1 to _totalFloors
            // Total buttons = 2 (X and 0) + _totalFloors
            int buttonCount = 2 + _totalFloors;
            _floorButtons = new FloorButton[buttonCount];

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

                Button button = buttonObj.GetComponent<Button>();
                TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null)
                {
                    text.text = floorNumber.ToString();
                }

                _floorButtons[buttonIndex] = new FloorButton
                {
                    FloorNumber = floorNumber,
                    Button = button,
                    Text = text,
                    Image = button.GetComponent<Image>(),
                    IsBlocked = false
                };

                int floor = floorNumber; // Capture for closure
                button.onClick.AddListener(() => OnFloorButtonClicked(floor));
            }
        }

        private void CreateSpecialFloorButton(int buttonIndex, string displayText, int internalFloorNumber)
        {
            GameObject buttonObj = Instantiate(_floorButtonPrefab, _buttonContainer);
            buttonObj.name = $"Floor_{displayText}";

            Button button = buttonObj.GetComponent<Button>();
            TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = displayText;
            }

            _floorButtons[buttonIndex] = new FloorButton
            {
                FloorNumber = internalFloorNumber,
                Button = button,
                Text = text,
                Image = button.GetComponent<Image>(),
                IsBlocked = true // Floor X and Floor 0 are blocked
            };

            // Blocked floors don't have click handlers
            button.interactable = false;
        }

        private void RefreshButtonStates()
        {
            if (_floorButtons == null) return;

            foreach (var floorBtn in _floorButtons)
            {
                if (floorBtn == null || floorBtn.Button == null) continue;

                FloorState state = GetFloorState(floorBtn);
                ApplyButtonStyle(floorBtn, state);
            }
        }

        private FloorState GetFloorState(FloorButton floorBtn)
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
                return FloorState.Unlocked;
            }
            else if (floor == _highestUnlockedFloor + 1)
            {
                return FloorState.NextToUnlock;
            }
            else
            {
                return FloorState.Locked;
            }
        }

        private void ApplyButtonStyle(FloorButton floorBtn, FloorState state)
        {
            Color buttonColor;
            Color textColor = _defaultTextColor;
            bool interactable = true;

            switch (state)
            {
                case FloorState.Blocked:
                    buttonColor = _blockedFloorColor;
                    textColor = _blockedTextColor;
                    interactable = false;
                    break;
                case FloorState.Current:
                    buttonColor = _currentFloorColor;
                    textColor = _currentFloorTextColor;
                    interactable = false; // Can't travel to current floor
                    break;
                case FloorState.Unlocked:
                    buttonColor = _unlockedFloorColor;
                    break;
                case FloorState.NextToUnlock:
                    buttonColor = _nextFloorColor;
                    textColor = Color.black;
                    break;
                case FloorState.Locked:
                default:
                    buttonColor = _lockedFloorColor;
                    textColor = Color.black;
                    interactable = false; // Can't travel to floors beyond next
                    break;
            }

            if (floorBtn.Image != null)
            {
                floorBtn.Image.color = buttonColor;
            }

            if (floorBtn.Text != null)
            {
                floorBtn.Text.color = textColor;
            }

            floorBtn.Button.interactable = interactable;
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

        private class FloorButton
        {
            public int FloorNumber;
            public Button Button;
            public TextMeshProUGUI Text;
            public Image Image;
            public bool IsBlocked;
        }

        private enum FloorState
        {
            Blocked,
            Current,
            Unlocked,
            NextToUnlock,
            Locked
        }

        #endregion
    }
}
