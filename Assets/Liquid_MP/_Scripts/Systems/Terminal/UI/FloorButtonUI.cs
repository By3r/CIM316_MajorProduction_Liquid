using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Holds references to visual components of a floor button in the elevator control grid.
    /// Supports states: Current, Unsealed, Breachable, Sealed, and Destination overlay.
    /// </summary>
    public class FloorButtonUI : MonoBehaviour
    {
        #region Enums

        public enum FloorState
        {
            Current,    // Green — player is here
            Unsealed,   // Amber — previously visited, free to travel
            Breachable, // Cyan — next sealed floor, can breach with power cell
            Sealed      // Dim — locked, not reachable
        }

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _floorNumber;
        [SerializeField] private TextMeshProUGUI _floorLabel;

        [Header("State Colors — Background")]
        [SerializeField] private Color _currentBg    = new Color(0.20f, 1.00f, 0.53f, 0.06f);
        [SerializeField] private Color _unsealedBg   = new Color(0.06f, 0.06f, 0.03f, 1.00f);
        [SerializeField] private Color _breachableBg  = new Color(0.27f, 0.87f, 0.87f, 0.06f);
        [SerializeField] private Color _sealedBg     = new Color(0.06f, 0.06f, 0.03f, 0.15f);

        [Header("State Colors — Number Text")]
        [SerializeField] private Color _currentNumColor    = new Color(0.20f, 1.00f, 0.53f, 1.00f);
        [SerializeField] private Color _unsealedNumColor   = new Color(1.00f, 0.69f, 0.00f, 1.00f);
        [SerializeField] private Color _breachableNumColor  = new Color(0.27f, 0.87f, 0.87f, 1.00f);
        [SerializeField] private Color _sealedNumColor     = new Color(0.25f, 0.19f, 0.00f, 0.15f);

        [Header("State Colors — Label Text")]
        [SerializeField] private Color _currentLabelColor    = new Color(0.10f, 0.60f, 0.31f, 1.00f);
        [SerializeField] private Color _unsealedLabelColor   = new Color(0.25f, 0.19f, 0.00f, 1.00f);
        [SerializeField] private Color _breachableLabelColor  = new Color(0.27f, 0.87f, 0.87f, 0.60f);
        [SerializeField] private Color _sealedLabelColor     = new Color(0.25f, 0.19f, 0.00f, 0.15f);

        #endregion

        #region Private Fields

        private int _floor;
        private FloorState _state;
        private bool _isDestination;

        #endregion

        #region Properties

        public int Floor => _floor;
        public FloorState State => _state;
        public bool IsDestination => _isDestination;
        public Button Button => _button;

        #endregion

        #region Public Methods

        /// <summary>
        /// One-time setup with floor number.
        /// </summary>
        public void Initialize(int floor)
        {
            _floor = floor;

            if (_floorNumber != null)
                _floorNumber.text = floor.ToString("D2");
        }

        /// <summary>
        /// Sets the floor state and updates all visuals.
        /// </summary>
        public void SetState(FloorState state)
        {
            _state = state;
            _isDestination = false;
            ApplyVisuals();
        }

        /// <summary>
        /// Marks or unmarks this button as the selected destination.
        /// Destination is an overlay on top of the current state.
        /// </summary>
        public void SetDestination(bool isDestination)
        {
            _isDestination = isDestination;
            ApplyVisuals();
        }

        #endregion

        #region Private Methods

        private void ApplyVisuals()
        {
            bool interactable = true;
            Color bg, numColor, labelColor;
            string label;

            switch (_state)
            {
                case FloorState.Current:
                    bg = _currentBg;
                    numColor = _currentNumColor;
                    labelColor = _currentLabelColor;
                    label = "YOU";
                    interactable = false;
                    break;

                case FloorState.Unsealed:
                    bg = _unsealedBg;
                    numColor = _unsealedNumColor;
                    labelColor = _unsealedLabelColor;
                    label = "OPEN";
                    break;

                case FloorState.Breachable:
                    bg = _breachableBg;
                    numColor = _breachableNumColor;
                    labelColor = _breachableLabelColor;
                    label = "BREACH";
                    break;

                case FloorState.Sealed:
                default:
                    bg = _sealedBg;
                    numColor = _sealedNumColor;
                    labelColor = _sealedLabelColor;
                    label = "SEALED";
                    interactable = false;
                    break;
            }

            // Destination override — brightens the button
            if (_isDestination)
            {
                label = "DEST";
            }

            if (_background != null) _background.color = bg;
            if (_floorNumber != null) _floorNumber.color = numColor;
            if (_floorLabel != null)
            {
                _floorLabel.text = label;
                _floorLabel.color = labelColor;
            }

            if (_button != null) _button.interactable = interactable;
        }

        #endregion

        #region Editor Helpers

        private void OnValidate()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        #endregion
    }
}
