using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Holds references to visual components of a crafting recipe entry
    /// in the fabrication schematics list.
    /// </summary>
    public class SchematicListItemUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _selectionBorder;
        [SerializeField] private TextMeshProUGUI _recipeName;
        [SerializeField] private Image _statusIcon;

        [Header("Status Icons")]
        [SerializeField] private Sprite _readyIcon;
        [SerializeField] private Sprite _lockedIcon;

        [Header("Colors")]
        [SerializeField] private Color _readyIconColor   = new Color(0.20f, 1.00f, 0.53f, 1.00f);
        [SerializeField] private Color _lockedIconColor   = new Color(0.60f, 0.13f, 0.13f, 1.00f);
        [SerializeField] private Color _nameColor         = new Color(1.00f, 0.69f, 0.00f, 1.00f);
        [SerializeField] private Color _nameUnavailColor  = new Color(0.50f, 0.38f, 0.00f, 0.35f);
        [SerializeField] private Color _selectedBgColor   = new Color(0.15f, 0.13f, 0.09f, 1.00f);
        [SerializeField] private Color _defaultBgColor    = new Color(0.06f, 0.05f, 0.04f, 0.00f);
        [SerializeField] private Color _selectionAccent   = new Color(1.00f, 0.69f, 0.00f, 1.00f);

        #endregion

        #region Private Fields

        private int _index;
        private bool _isAvailable;
        private bool _isSelected;

        #endregion

        #region Properties

        public int Index => _index;
        public bool IsAvailable => _isAvailable;
        public bool IsSelected => _isSelected;
        public Button Button => _button;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets up the list item with recipe data.
        /// </summary>
        public void Initialize(int index, string name, bool isAvailable)
        {
            _index = index;
            _isAvailable = isAvailable;

            if (_recipeName != null)
                _recipeName.text = name;

            SetAvailable(isAvailable);
        }

        /// <summary>
        /// Updates whether the player has the required materials.
        /// </summary>
        public void SetAvailable(bool available)
        {
            _isAvailable = available;

            if (_statusIcon != null)
            {
                _statusIcon.sprite = available ? _readyIcon : _lockedIcon;
                _statusIcon.color = available ? _readyIconColor : _lockedIconColor;
            }

            if (_recipeName != null)
                _recipeName.color = available ? _nameColor : _nameUnavailColor;
        }

        /// <summary>
        /// Sets the selected state (highlighted in the list).
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (_background != null)
                _background.color = selected ? _selectedBgColor : _defaultBgColor;

            if (_selectionBorder != null)
            {
                _selectionBorder.gameObject.SetActive(selected);
                _selectionBorder.color = _selectionAccent;
            }
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
