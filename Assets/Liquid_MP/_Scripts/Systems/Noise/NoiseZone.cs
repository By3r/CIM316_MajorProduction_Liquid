using UnityEngine;

namespace Liquid.Audio
{
    public enum NoiseZoneMode
    {
        Multiply,
        Add
    }

    [DisallowMultipleComponent]
    public sealed class NoiseZone : MonoBehaviour
    {
        [Header("Scope")]
        [Tooltip("If not empty, only affects these categories.")]
        [SerializeField] private NoiseCategory[] categories;

        [Header("Radius")]
        [SerializeField] private NoiseZoneMode radiusMode = NoiseZoneMode.Multiply;
        [SerializeField] private float radiusValue = 1f;

        [Header("Intensity (0..1)")]
        [SerializeField] private NoiseZoneMode intensityMode = NoiseZoneMode.Multiply;
        [SerializeField] private float intensityValue = 1f;

        [Header("Ambient Mask Add")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientAdd01 = 0f;

        public bool Affects(NoiseCategory c)
        {
            if (categories == null || categories.Length == 0)
                return true;

            for (int i = 0; i < categories.Length; i++)
                if (categories[i] == c) return true;

            return false;
        }

        public float ApplyRadius(float radius)
        {
            return radiusMode == NoiseZoneMode.Multiply ? radius * radiusValue : radius + radiusValue;
        }

        public float ApplyIntensity(float intensity01)
        {
            float v = intensityMode == NoiseZoneMode.Multiply ? intensity01 * intensityValue : intensity01 + intensityValue;
            return Mathf.Clamp01(v);
        }

        public float AmbientAdd01 => ambientAdd01;
    }
}