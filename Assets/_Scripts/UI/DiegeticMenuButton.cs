using UnityEngine;
using TMPro; 

namespace MainMenu.UI
{
    public class DiegeticMenuButton : MonoBehaviour
    {
        public enum ButtonType
        {
            NewGame,
            LoadGame,
            Settings,
            Exit
        }

        [Header("Button Settings")]
        [SerializeField] private ButtonType _buttonType = ButtonType.NewGame;

        [Tooltip("Point the flashlight should look at. If empty, this object's transform is used.")]
        [SerializeField] private Transform _aimTarget;

        [Header("Visuals")]
        [Tooltip("The text component for this button label.")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("Text colour when the button is not selected.")]
        [SerializeField] private Color _normalColor = Color.gray;

        [Tooltip("Text colour when the button is selected.")]
        [SerializeField] private Color _selectedColor = Color.white;

        public ButtonType Type => _buttonType;

        public Transform AimTarget => _aimTarget != null ? _aimTarget : transform;

        private void Reset()
        {
            _aimTarget ??= transform;

            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>();
            }
        }

        /// <summary>
        /// Called when the player confirms this button.
        /// </summary>
        public void Activate()
        {
            Debug.Log($"Activate: {_buttonType}", this);
        }

        public void SetSelected(bool isSelected)
        {
            if (_label == null)
            {
                return;
            }

            _label.color = isSelected ? _selectedColor : _normalColor;
        }
    }
}