using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// UI component for displaying an ingredient counter (Ferrite, Polymer, or Reagent).
    /// Shows icon, name, and count/cap.
    /// </summary>
    public class IngredientCounterUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private IngredientType _ingredientType;

        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Slider _fillBar;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _fullColor = Color.green;
        [SerializeField] private Color _emptyColor = new Color(0.5f, 0.5f, 0.5f);

        public IngredientType IngredientType => _ingredientType;

        private void Start()
        {
            if (_nameText != null)
            {
                _nameText.text = _ingredientType.ToString();
            }
        }

        public void UpdateCounter(int count, int cap)
        {
            if (_countText != null)
            {
                _countText.text = $"{count}/{cap}";

                // Color based on state
                if (count >= cap)
                {
                    _countText.color = _fullColor;
                }
                else if (count == 0)
                {
                    _countText.color = _emptyColor;
                }
                else
                {
                    _countText.color = _normalColor;
                }
            }

            if (_fillBar != null)
            {
                _fillBar.maxValue = cap;
                _fillBar.value = count;
            }
        }
    }
}
