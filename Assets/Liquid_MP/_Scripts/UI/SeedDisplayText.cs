using TMPro;
using UnityEngine;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// Attach to a TextMeshProUGUI to display the current world seed.
    /// Automatically registers with FloorStateManager on scene load.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class SeedDisplayText : MonoBehaviour
    {
        private TextMeshProUGUI _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            if (FloorStateManager.Instance != null)
            {
                FloorStateManager.Instance.SetSeedDisplayText(_text);
            }
        }
    }
}