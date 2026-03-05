using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Manages the Elevator Control tab of the safe room terminal.
    /// Left side: floor selection grid. Right side: status panel.
    /// </summary>
    public class ElevatorControlPanelUI : MonoBehaviour
    {
        #region Events

        /// <summary>
        /// Fired when the player confirms travel/breach. Passes the target floor.
        /// </summary>
        public event Action<int> OnTravelConfirmed;

        #endregion

        #region Serialized Fields

        [Header("Floor Grid — Left Panel")]
        [SerializeField] private Transform _floorGridContainer;
        [SerializeField] private GameObject _floorButtonPrefab;
        [SerializeField] private TextMeshProUGUI _floorCountText;

        [Header("Current Floor Display — Right Panel")]
        [SerializeField] private TextMeshProUGUI _currentFloorNumber;

        [Header("Direction Indicator")]
        [SerializeField] private Image _dirArrowLeft;
        [SerializeField] private Image _dirArrowRight;
        [SerializeField] private TextMeshProUGUI _dirText;

        [Header("Direction Arrow Sprites")]
        [SerializeField] private Sprite _arrowUpSprite;
        [SerializeField] private Sprite _arrowDownSprite;

        [Header("Destination Display")]
        [SerializeField] private TextMeshProUGUI _destNumber;

        [Header("Systems Block")]
        [SerializeField] private TextMeshProUGUI _statusValue;
        [SerializeField] private TextMeshProUGUI _sealsOpenValue;
        [SerializeField] private TextMeshProUGUI _nextSealValue;

        [Header("Power Cell Display")]
        [SerializeField] private Image _cellOutline;
        [SerializeField] private Image _cellFill;
        [SerializeField] private TextMeshProUGUI _cellTitle;
        [SerializeField] private TextMeshProUGUI _cellStatus;

        [Header("Action Button")]
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionButtonText;
        [SerializeField] private TextMeshProUGUI _actionWarningText;

        [Header("Direction Colors")]
        [SerializeField] private Color _downColor   = new Color(1.00f, 0.69f, 0.00f, 1.00f);
        [SerializeField] private Color _upColor     = new Color(0.10f, 0.60f, 0.31f, 1.00f);
        [SerializeField] private Color _breachColor = new Color(0.27f, 0.87f, 0.87f, 1.00f);
        [SerializeField] private Color _noneColor   = new Color(0.25f, 0.19f, 0.00f, 1.00f);

        [Header("Action Button Colors")]
        [SerializeField] private Color _travelBtnColor  = new Color(1.00f, 0.69f, 0.00f, 1.00f);
        [SerializeField] private Color _breachBtnColor  = new Color(0.27f, 0.87f, 0.87f, 1.00f);
        [SerializeField] private Color _disabledBtnColor = new Color(0.25f, 0.19f, 0.00f, 1.00f);

        [Header("Power Cell Colors")]
        [SerializeField] private Color _cellInsertedColor = new Color(0.20f, 1.00f, 0.53f, 1.00f);
        [SerializeField] private Color _cellMissingColor  = new Color(0.60f, 0.13f, 0.13f, 1.00f);

        #endregion

        #region Private Fields

        private FloorButtonUI[] _floorButtons;
        private int _totalFloors;
        private int _currentFloor;
        private int _highestUnsealed;
        private bool _hasPowerCell;
        private int _powerCellCharges;
        private int _selectedDestination = -1;

        #endregion

        #region Properties

        public int SelectedDestination => _selectedDestination;

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds the floor grid and sets initial state.
        /// </summary>
        public void Initialize(int totalFloors, int currentFloor, int highestUnsealed,
                               bool hasPowerCell, int powerCellCharges)
        {
            _totalFloors = totalFloors;
            _currentFloor = currentFloor;
            _highestUnsealed = highestUnsealed;
            _hasPowerCell = hasPowerCell;
            _powerCellCharges = powerCellCharges;
            _selectedDestination = -1;

            BuildFloorGrid();
            RefreshAllButtons();
            RefreshStatusPanel();
            RefreshActionButton();

            if (_actionButton != null)
                _actionButton.onClick.AddListener(OnActionButtonClicked);
        }

        /// <summary>
        /// Refreshes all state without rebuilding the grid.
        /// Call when power cell state changes or floor changes.
        /// </summary>
        public void Refresh(int currentFloor, int highestUnsealed,
                            bool hasPowerCell, int powerCellCharges)
        {
            _currentFloor = currentFloor;
            _highestUnsealed = highestUnsealed;
            _hasPowerCell = hasPowerCell;
            _powerCellCharges = powerCellCharges;

            RefreshAllButtons();
            RefreshStatusPanel();
            RefreshActionButton();
        }

        /// <summary>
        /// Clears the selected destination.
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedDestination > 0 && _floorButtons != null)
            {
                int idx = _selectedDestination - 1;
                if (idx >= 0 && idx < _floorButtons.Length)
                    _floorButtons[idx].SetDestination(false);
            }

            _selectedDestination = -1;
            RefreshActionButton();
            RefreshDirectionIndicator();
        }

        #endregion

        #region Private Methods — Grid

        private void BuildFloorGrid()
        {
            // Clear existing buttons
            if (_floorButtons != null)
            {
                foreach (var btn in _floorButtons)
                {
                    if (btn != null)
                        Destroy(btn.gameObject);
                }
            }

            _floorButtons = new FloorButtonUI[_totalFloors];

            // Build low-to-high: floor 1 at top-left, fills left→right, top→bottom
            for (int i = 1; i <= _totalFloors; i++)
            {
                GameObject obj = Instantiate(_floorButtonPrefab, _floorGridContainer);
                obj.name = $"Floor_{i:D2}";

                var btn = obj.GetComponent<FloorButtonUI>();
                if (btn == null)
                {
                    Debug.LogError("[ElevatorControlPanelUI] Floor button prefab missing FloorButtonUI!");
                    continue;
                }

                btn.Initialize(i);
                _floorButtons[i - 1] = btn;

                int floor = i; // Capture for closure
                btn.Button.onClick.AddListener(() => OnFloorClicked(floor));
            }

            if (_floorCountText != null)
                _floorCountText.text = $"{_totalFloors} FLOORS";
        }

        private void RefreshAllButtons()
        {
            if (_floorButtons == null) return;

            int breachFloor = _highestUnsealed + 1;

            for (int i = 0; i < _floorButtons.Length; i++)
            {
                if (_floorButtons[i] == null) continue;

                int floor = i + 1;
                FloorButtonUI.FloorState state;

                if (floor == _currentFloor)
                    state = FloorButtonUI.FloorState.Current;
                else if (floor <= _highestUnsealed)
                    state = FloorButtonUI.FloorState.Unsealed;
                else if (floor == breachFloor && _hasPowerCell)
                    state = FloorButtonUI.FloorState.Breachable;
                else
                    state = FloorButtonUI.FloorState.Sealed;

                _floorButtons[i].SetState(state);

                // Restore destination if still valid
                _floorButtons[i].SetDestination(floor == _selectedDestination);
            }
        }

        private void OnFloorClicked(int floor)
        {
            if (floor == _currentFloor) return;

            // Clear previous destination
            if (_selectedDestination > 0 && _selectedDestination <= _totalFloors)
            {
                _floorButtons[_selectedDestination - 1].SetDestination(false);
            }

            _selectedDestination = floor;
            _floorButtons[floor - 1].SetDestination(true);

            RefreshDirectionIndicator();
            RefreshActionButton();
        }

        #endregion

        #region Private Methods — Status Panel

        private void RefreshStatusPanel()
        {
            // Current floor
            if (_currentFloorNumber != null)
                _currentFloorNumber.text = _currentFloor.ToString("D2");

            // Systems block
            if (_statusValue != null)
                _statusValue.text = "IDLE \u2014 READY";

            if (_sealsOpenValue != null)
                _sealsOpenValue.text = $"{_highestUnsealed} / {_totalFloors}";

            if (_nextSealValue != null)
            {
                if (_hasPowerCell)
                {
                    int nextSeal = _highestUnsealed + 1;
                    _nextSealValue.text = nextSeal <= _totalFloors ? $"FLOOR {nextSeal:D2}" : "ALL OPEN";
                    _nextSealValue.color = _breachColor;
                }
                else
                {
                    _nextSealValue.text = "NO POWER CELL";
                    _nextSealValue.color = _cellMissingColor;
                }
            }

            // Power cell block
            RefreshPowerCellDisplay();

            // Direction starts at none
            RefreshDirectionIndicator();
        }

        private void RefreshPowerCellDisplay()
        {
            bool inserted = _hasPowerCell;

            if (_cellOutline != null)
                _cellOutline.color = inserted ? _cellInsertedColor : _cellMissingColor;

            if (_cellFill != null)
                _cellFill.gameObject.SetActive(inserted);

            if (_cellStatus != null)
            {
                _cellStatus.text = inserted ? $"INSERTED \u2014 {_powerCellCharges} CHARGE" : "NOT INSERTED";
                _cellStatus.color = inserted ? _cellInsertedColor : _cellMissingColor;
            }
        }

        private void RefreshDirectionIndicator()
        {
            if (_selectedDestination < 1)
            {
                // No destination selected — hide arrows
                SetDirectionArrows(null, _noneColor, false);
                if (_dirText != null) { _dirText.text = "SELECT A FLOOR"; _dirText.color = _noneColor; }
                SetDestinationDisplay(null);
                return;
            }

            string from = _currentFloor.ToString("D2");
            string to = _selectedDestination.ToString("D2");
            bool isBreach = _selectedDestination > _highestUnsealed;

            if (_selectedDestination > _currentFloor)
            {
                // Going down (deeper)
                Color c = isBreach ? _breachColor : _downColor;
                SetDirectionArrows(_arrowDownSprite, c, true);
                if (_dirText != null) { _dirText.text = $"FLOOR {from} \u2192 FLOOR {to}"; _dirText.color = c; }
            }
            else
            {
                // Going up (returning)
                SetDirectionArrows(_arrowUpSprite, _upColor, true);
                if (_dirText != null) { _dirText.text = $"FLOOR {from} \u2192 FLOOR {to}"; _dirText.color = _upColor; }
            }

            SetDestinationDisplay(_selectedDestination.ToString("D2"));
        }

        private void SetDirectionArrows(Sprite sprite, Color color, bool visible)
        {
            if (_dirArrowLeft != null)
            {
                _dirArrowLeft.gameObject.SetActive(visible);
                if (sprite != null) _dirArrowLeft.sprite = sprite;
                _dirArrowLeft.color = color;
            }

            if (_dirArrowRight != null)
            {
                _dirArrowRight.gameObject.SetActive(visible);
                if (sprite != null) _dirArrowRight.sprite = sprite;
                _dirArrowRight.color = color;
            }
        }

        private void SetDestinationDisplay(string floorText)
        {
            if (_destNumber == null) return;

            if (string.IsNullOrEmpty(floorText))
            {
                _destNumber.text = "\u2014 NONE \u2014";
                _destNumber.color = _noneColor;
            }
            else
            {
                bool isBreach = _selectedDestination > _highestUnsealed;
                _destNumber.text = floorText;
                _destNumber.color = isBreach ? _breachColor : _downColor;
            }
        }

        #endregion

        #region Private Methods — Action Button

        private void RefreshActionButton()
        {
            if (_actionButton == null || _actionButtonText == null) return;

            if (_selectedDestination < 1)
            {
                // No destination
                _actionButtonText.text = "SELECT DESTINATION";
                _actionButtonText.color = _disabledBtnColor;
                _actionButton.interactable = false;

                if (_actionWarningText != null)
                    _actionWarningText.text = "SELECT A DESTINATION";
                return;
            }

            bool isBreach = _selectedDestination > _highestUnsealed;
            string floorStr = _selectedDestination.ToString("D2");

            if (isBreach)
            {
                _actionButtonText.text = $"BREACH SEAL \u2014 FLOOR {floorStr}";
                _actionButtonText.color = _breachBtnColor;
                _actionButton.interactable = _hasPowerCell;

                if (_actionWarningText != null)
                    _actionWarningText.text = "SEAL BREACH WILL CONSUME 1 CHARGE";
            }
            else
            {
                // Travel to open floor
                _actionButtonText.text = $"TRAVEL \u2014 FLOOR {floorStr}";
                _actionButtonText.color = _travelBtnColor;
                _actionButton.interactable = true;

                if (_actionWarningText != null)
                {
                    _actionWarningText.text = _hasPowerCell
                        ? "TRAVEL TO OPEN FLOORS \u2014 NO CHARGE USED"
                        : "POWER CELL REQUIRED TO BREACH SEALS";
                }
            }
        }

        private void OnActionButtonClicked()
        {
            if (_selectedDestination < 1) return;
            OnTravelConfirmed?.Invoke(_selectedDestination);
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_actionButton != null)
                _actionButton.onClick.RemoveListener(OnActionButtonClicked);
        }

        #endregion
    }
}
