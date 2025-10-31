using UnityEngine;

namespace Liquid.Audio
{
    [CreateAssetMenu(menuName = "Liquid/Noise Config", fileName = "NoiseConfigurer")]
    public class NoiseConfigurer : ScriptableObject
    {
        #region Variables
        [Header("Decay")]
        [Tooltip("Units per second the intensity falls toward 0")]
        public float decayPerSecond = 0.6f;

        [Header("Intensity per Level (0..1)")]
        [Range(0f, 1f)] public float noneIntensity = 0f;
        [Range(0f, 1f)] public float lowIntensity = 0.25f;
        [Range(0f, 1f)] public float mediumIntensity = 0.6f;
        [Range(0f, 1f)] public float highIntensity = 1f;

        [Header("Thresholds for reporting level (0..1)")]
        [Tooltip(">= highThreshold => Maximum")]
        [Range(0f, 1f)] public float highThreshold = 0.8f;

        [Tooltip(">= mediumThreshold => High")]
        [Range(0f, 1f)] public float mediumThreshold = 0.45f;

        [Tooltip(">= lowThreshold => Medium")]
        [Range(0f, 1f)] public float lowThreshold = 0.1f;
        #endregion

        public float IntensityForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Medium: return lowIntensity;
                case NoiseLevel.High: return mediumIntensity;
                case NoiseLevel.Maximum: return highIntensity;
                default: return noneIntensity;
            }
        }

        public NoiseLevel LevelFromIntensity(float intensity)
        {
            if (intensity >= highThreshold) return NoiseLevel.Maximum;
            if (intensity >= mediumThreshold) return NoiseLevel.High;
            if (intensity >= lowThreshold) return NoiseLevel.Medium;
            return NoiseLevel.Low;
        }
    }
}