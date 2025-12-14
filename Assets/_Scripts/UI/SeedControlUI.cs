using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// UI for displaying and setting the world seed.
    /// Collapsible panel with input field to set a specific seed.
    /// </summary>
    public class SeedControlUI : MonoBehaviour
    {
        [Header("Header (Always Visible)")]
        [SerializeField] private Button _headerButton;
        [SerializeField] private TextMeshProUGUI _headerText;

        [Header("Collapsible Panel")]
        [SerializeField] private GameObject _expandedPanel;
        [SerializeField] private TMP_InputField _seedInputField;
        [SerializeField] private Button _setSeedButton;

        [Header("State")]
        [SerializeField] private bool _startExpanded = false;

        private bool _isExpanded;

        private void Start()
        {
            SetupUI();
            RegisterWithFloorStateManager();
            SetExpanded(_startExpanded);
        }

        private void SetupUI()
        {
            if (_headerButton != null)
            {
                _headerButton.onClick.AddListener(ToggleExpanded);
            }

            if (_setSeedButton != null)
            {
                _setSeedButton.onClick.AddListener(OnSetSeedClicked);
            }

            if (_seedInputField != null)
            {
                _seedInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
                _seedInputField.onSubmit.AddListener(_ => OnSetSeedClicked());
            }
        }

        private void RegisterWithFloorStateManager()
        {
            if (FloorStateManager.Instance != null)
            {
                FloorStateManager.Instance.SetSeedControlUI(this);
            }
        }

        /// <summary>
        /// Updates the header text with the current seed.
        /// Called by FloorStateManager.
        /// </summary>
        public void UpdateSeedDisplay(int seed)
        {
            if (_headerText != null)
            {
                _headerText.text = $"Seed: {seed}";
            }
        }

        private void ToggleExpanded()
        {
            SetExpanded(!_isExpanded);
        }

        private void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            if (_expandedPanel != null)
            {
                _expandedPanel.SetActive(_isExpanded);
            }
        }

        private void OnSetSeedClicked()
        {
            if (_seedInputField == null) return;

            string input = _seedInputField.text.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("[SeedControlUI] Seed input is empty.");
                return;
            }

            if (int.TryParse(input, out int seed))
            {
                if (seed == 0)
                {
                    Debug.LogWarning("[SeedControlUI] Seed cannot be 0. Use a positive or negative integer.");
                    return;
                }

                SetSeedAndReload(seed);
            }
            else
            {
                Debug.LogWarning($"[SeedControlUI] Invalid seed: {input}");
            }
        }

        private void SetSeedAndReload(int seed)
        {
            if (FloorStateManager.Instance != null)
            {
                FloorStateManager.Instance.SetSpecificSeed(seed);
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}