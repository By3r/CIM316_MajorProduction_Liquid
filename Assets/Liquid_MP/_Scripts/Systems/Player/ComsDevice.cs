using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// MonoBehaviour placed on the COMS device runtime prefab.
    /// Holds references to the IK target and visual elements.
    /// </summary>
    public class ComsDevice : MonoBehaviour
    {
        [Header("IK References")]
        [Tooltip("Transform where the left hand grips the device (IK target).")]
        [SerializeField] private Transform _leftHandIkTarget;

        [Tooltip("Optional elbow hint for left hand IK.")]
        [SerializeField] private Transform _leftHandIkHint;

        [Header("Visuals")]
        [Tooltip("Root of the COMS screen display (for future screen content).")]
        [SerializeField] private GameObject _screenRoot;

        public Transform LeftHandIkTarget => _leftHandIkTarget;
        public Transform LeftHandIkHint => _leftHandIkHint;

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
