using UnityEngine;

namespace Liquid.Audio
{
    [DisallowMultipleComponent]
    public sealed class NoiseHearingProfile : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("Overall hearing multiplier. >1 hears better, <1 hears worse.")]
        [SerializeField] private float hearingSensitivity = 1f;

        [Tooltip("If perceived loudness (after distance/occlusion/ambient) is below this, ignore.")]
        [Range(0f, 1f)]
        [SerializeField] private float minPerceivedLoudness = 0.08f;

        [Header("Per-category multipliers")]
        [SerializeField] private float footsteps = 1f;
        [SerializeField] private float sprint = 1.1f;
        [SerializeField] private float jump = 1f;
        [SerializeField] private float gunshot = 1.4f;
        [SerializeField] private float objectImpact = 1.2f;
        [SerializeField] private float other = 1f;

        public float HearingSensitivity => hearingSensitivity;
        public float MinPerceivedLoudness => minPerceivedLoudness;

        public float GetCategoryMultiplier(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footsteps: return footsteps;
                case NoiseCategory.Sprint: return sprint;
                case NoiseCategory.Jump: return jump;
                case NoiseCategory.Gunshot: return gunshot;
                case NoiseCategory.ObjectImpact: return objectImpact;
                default: return other;
            }
        }
    }
}