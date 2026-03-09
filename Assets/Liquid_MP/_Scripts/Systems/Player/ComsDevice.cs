using TMPro;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// MonoBehaviour placed on the COMS device runtime prefab.
    /// Holds references to IK targets, call display elements (hologram mount,
    /// screen text), and visual toggles.
    /// </summary>
    public class ComsDevice : MonoBehaviour
    {
        [Header("IK References")]
        [Tooltip("Transform where the left hand grips the device (IK target).")]
        [SerializeField] private Transform _leftHandIkTarget;

        [Tooltip("Optional elbow hint for left hand IK.")]
        [SerializeField] private Transform _leftHandIkHint;

        [Header("Visuals")]
        [Tooltip("Root of the COMS screen display.")]
        [SerializeField] private GameObject _screenRoot;

        [Header("Call Display")]
        [Tooltip("Empty transform above the device where the caller hologram is spawned.")]
        [SerializeField] private Transform _hologramMount;

        [Tooltip("TextMeshPro component on the device's world-space canvas. " +
                 "Shows dialogue text during calls, 'NO SIGNAL' when idle.")]
        [SerializeField] private TextMeshProUGUI _screenText;

        public Transform LeftHandIkTarget => _leftHandIkTarget;
        public Transform LeftHandIkHint => _leftHandIkHint;
        public Transform HologramMount => _hologramMount;
        public TextMeshProUGUI ScreenText => _screenText;

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// Sets the screen text content. Pass null or empty to show "NO SIGNAL".
        /// </summary>
        public void SetScreenText(string text)
        {
            if (_screenText == null) return;
            _screenText.text = string.IsNullOrEmpty(text) ? "NO SIGNAL" : text;
        }
    }
}
