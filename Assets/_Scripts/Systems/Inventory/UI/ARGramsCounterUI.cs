using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// UI component for displaying AR grams counter.
    /// Shows current/max AR grams in container.
    /// </summary>
    public class ARGramsCounterUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Slider _fillBar;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.cyan;
        [SerializeField] private Color _fullColor = Color.green;
        [SerializeField] private Color _emptyColor = new Color(0.3f, 0.3f, 0.3f);

        private void Start()
        {
            if (_labelText != null)
            {
                _labelText.text = "AR";
            }
        }

        public void UpdateCounter(int grams, int cap)
        {
            if (_countText != null)
            {
                _countText.text = $"{grams}g/{cap}g";

                if (grams >= cap)
                {
                    _countText.color = _fullColor;
                }
                else if (grams == 0)
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
                _fillBar.value = grams;
            }
        }
    }
}
