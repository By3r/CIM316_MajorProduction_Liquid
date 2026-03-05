using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Holds references to visual components of a single ingredient row
    /// in the fabrication schematic detail view.
    /// </summary>
    public class IngredientRowUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _ingredientName;
        [SerializeField] private TextMeshProUGUI _quantityText;

        [Header("Colors")]
        [SerializeField] private Color _sufficientColor   = new Color(0.20f, 1.00f, 0.53f, 1.00f);
        [SerializeField] private Color _insufficientColor  = new Color(1.00f, 0.27f, 0.27f, 1.00f);
        [SerializeField] private Color _nameColor          = new Color(1.00f, 0.69f, 0.00f, 1.00f);
        [SerializeField] private Color _separatorColor     = new Color(0.50f, 0.38f, 0.00f, 1.00f);

        #endregion

        #region Public Methods

        /// <summary>
        /// Populates the ingredient row with data.
        /// </summary>
        public void Setup(string ingredientName, Sprite icon, int have, int need)
        {
            if (_ingredientName != null)
                _ingredientName.text = ingredientName;

            if (_icon != null && icon != null)
                _icon.sprite = icon;

            UpdateQuantity(have, need);
        }

        /// <summary>
        /// Updates the quantity display and color based on sufficiency.
        /// </summary>
        public void UpdateQuantity(int have, int need)
        {
            bool sufficient = have >= need;

            if (_quantityText != null)
            {
                // Format: "2 / 2" with the separator in a dimmer color using rich text
                string haveStr = have.ToString();
                string needStr = need.ToString();
                string sepColor = ColorUtility.ToHtmlStringRGB(_separatorColor);
                _quantityText.text = $"{haveStr} <color=#{sepColor}>/</color> {needStr}";
                _quantityText.color = sufficient ? _sufficientColor : _insufficientColor;
            }
        }

        #endregion
    }
}
