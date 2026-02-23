using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.UI
{
    /// <summary>
    /// Holds references to the visual components of an elevator floor button.
    /// Allows separate control of outline, background, and text colors.
    /// </summary>
    public class ElevatorFloorButton : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _outline;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _floorText;

        #endregion

        #region Properties

        public Button Button => _button;
        public Image Outline => _outline;
        public Image Background => _background;
        public TextMeshProUGUI FloorText => _floorText;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the floor number text.
        /// </summary>
        public void SetFloorNumber(string text)
        {
            if (_floorText != null)
            {
                _floorText.text = text;
            }
        }

        /// <summary>
        /// Sets the button interactable state.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        /// <summary>
        /// Applies colors for the current floor state.
        /// </summary>
        public void ApplyCurrentFloorStyle(Color outlineColor, Color backgroundColor, Color textColor)
        {
            SetColors(outlineColor, backgroundColor, textColor, 1f);
        }

        /// <summary>
        /// Applies colors for other floors (visited, unvisited, or blocked).
        /// Use opacity to differentiate unvisited/blocked floors.
        /// </summary>
        public void ApplyOtherFloorStyle(Color outlineColor, Color backgroundColor, Color textColor, float opacity = 1f)
        {
            SetColors(outlineColor, backgroundColor, textColor, opacity);
        }

        #endregion

        #region Private Methods

        private void SetColors(Color outlineColor, Color backgroundColor, Color textColor, float opacity)
        {
            if (_outline != null)
            {
                Color outline = outlineColor;
                outline.a *= opacity;
                _outline.color = outline;
            }

            if (_background != null)
            {
                Color bg = backgroundColor;
                bg.a *= opacity;
                _background.color = bg;
            }

            if (_floorText != null)
            {
                Color text = textColor;
                text.a *= opacity;
                _floorText.color = text;
            }
        }

        #endregion

        #region Editor Helpers

        private void OnValidate()
        {
            // Auto-find components if not assigned
            if (_button == null)
                _button = GetComponent<Button>();
        }

        #endregion
    }
}
