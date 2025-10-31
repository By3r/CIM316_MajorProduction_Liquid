using System;
using UnityEngine;

namespace Liquid.Audio
{
    [DisallowMultipleComponent]
    public class NoiseEmitter : MonoBehaviour, INoiseSource, INoiseController
    {
        #region Variables
        [SerializeField] private NoiseConfigurer config;

        [SerializeField, Tooltip("Clamp to [0,1]. For debugging you can start higher.")]
        private float currentIntensity = 0f;

        private NoiseLevel currentLevel = NoiseLevel.Low;
        private float lastReportedIntensity = -1f;

        public event Action<NoiseLevel, float> NoiseChanged;

        public NoiseLevel CurrentLevel => currentLevel;
        public float CurrentIntensity => currentIntensity;
        #endregion

        private void Reset()
        {
            currentIntensity = 0f;
            currentLevel = NoiseLevel.Low;
        }

        private void Update()
        {
            if (config == null) return;

            // value decay 
            if (currentIntensity > 0f)
            {
                float noiseValue = config.decayPerSecond * Time.deltaTime;
                currentIntensity = Mathf.Max(0f, currentIntensity - noiseValue);
            }

            // Recompute noise level and notify if changed
            NoiseLevel newLevel = config.LevelFromIntensity(currentIntensity);

            bool levelChanged = newLevel != currentLevel;
            bool intensityChangedEnough = Mathf.Abs(currentIntensity - lastReportedIntensity) > 0.01f;

            if (levelChanged || intensityChangedEnough)
            {
                currentLevel = newLevel;
                lastReportedIntensity = currentIntensity;

                if (levelChanged)
                {
                    Debug.Log($"Noise level changed to {currentLevel} (intensity {currentIntensity:0.00})", this);
                }

                NoiseChanged?.Invoke(currentLevel, currentIntensity);
            }
        }

        public void SetNoiseLevel(NoiseLevel level)
        {
            if (config == null) return;

            float target = Mathf.Clamp01(config.IntensityForLevel(level));
            if (target > currentIntensity)
            {
                currentIntensity = target;
            }
        }
    }
}