using UnityEngine;

namespace Liquid.Audio
{
    [CreateAssetMenu(menuName = "Liquid/Audio/Environment Noise Profile")]
    public class EnvironmentNoiseProfile : ScriptableObject
    {
        [Header("Meta")]
        [SerializeField] private string _environmentName = "New Environment";

        [Header("Global")]
        [Tooltip("Applied on top of all per-category multipliers. 0 = completely silent environment.")]
        [SerializeField, Min(0f)] private float _globalMultiplier = 1f;

        [Header("Ambient Noise")]
        [Tooltip("Background ambient noise 0..1. High ambient reduces enemy hearing gain.")]
        [Range(0f, 1f)]
        [SerializeField] private float _ambientNoiseLevel = 0f;

        [Header("Per-Category Multipliers")]
        [Tooltip("0 = silent, 1 = unchanged, >1 = amplified")]
        [SerializeField, Min(0f)] private float _footstepMultiplier = 1f;
        [SerializeField, Min(0f)] private float _sprintMultiplier = 1f;
        [SerializeField, Min(0f)] private float _jumpMultiplier = 1f;
        [SerializeField, Min(0f)] private float _gunshotMultiplier = 1f;
        [SerializeField, Min(0f)] private float _objectImpactMultiplier = 1f;
        [SerializeField, Min(0f)] private float _commDeviceMultiplier = 1f;
        [SerializeField, Min(0f)] private float _otherMultiplier = 1f;

        public string EnvironmentName => _environmentName;
        public float GlobalMultiplier => _globalMultiplier;

        /// <summary>
        /// Background ambient noise 0..1.
        /// </summary>
        public float AmbientNoiseLevel => _ambientNoiseLevel;

        /// <summary>
        /// Returns the final multiplier for a given category, including the global multiplier.
        /// </summary>
        public float GetMultiplier(NoiseCategory category)
        {
            return _globalMultiplier * GetCategoryMultiplier(category);
        }

        private float GetCategoryMultiplier(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footsteps: return _footstepMultiplier;
                case NoiseCategory.Sprint: return _sprintMultiplier;
                case NoiseCategory.Jump: return _jumpMultiplier;
                case NoiseCategory.Gunshot: return _gunshotMultiplier;
                case NoiseCategory.CommDevice: return _commDeviceMultiplier;
                case NoiseCategory.ObjectImpact: return _objectImpactMultiplier;
                default: return _otherMultiplier;
            }
        }
    }
}