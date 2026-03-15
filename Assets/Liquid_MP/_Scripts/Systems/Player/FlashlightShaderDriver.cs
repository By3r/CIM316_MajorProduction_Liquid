using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Pushes the player's flashlight (spotlight) position, direction, angle,
    /// range, and intensity to global shader properties every frame.
    /// Used by the "Liquid/Particles/PickupDust" shader to make dust particles
    /// glow when hit by the flashlight beam.
    ///
    /// Setup: Add this component to the same GameObject as the spotlight Light,
    /// or assign the Light reference in the Inspector.
    /// </summary>
    public sealed class FlashlightShaderDriver : MonoBehaviour
    {
        [Tooltip("The spotlight Light component. If empty, searches this GameObject.")]
        [SerializeField] private Light _spotlight;

        private static readonly int FlashlightPosId = Shader.PropertyToID("_FlashlightPos");
        private static readonly int FlashlightDirId = Shader.PropertyToID("_FlashlightDir");
        private static readonly int FlashlightAngleId = Shader.PropertyToID("_FlashlightAngle");
        private static readonly int FlashlightRangeId = Shader.PropertyToID("_FlashlightRange");
        private static readonly int FlashlightIntensityId = Shader.PropertyToID("_FlashlightIntensity");

        private void Awake()
        {
            if (_spotlight == null)
                _spotlight = GetComponent<Light>();

            if (_spotlight == null)
                _spotlight = GetComponentInChildren<Light>();

            if (_spotlight == null)
            {
                Debug.LogWarning("[FlashlightShaderDriver] No Light found. Disabling.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_spotlight == null || !_spotlight.enabled) return;

            Transform t = _spotlight.transform;

            Shader.SetGlobalVector(FlashlightPosId, t.position);
            Shader.SetGlobalVector(FlashlightDirId, t.forward);
            Shader.SetGlobalFloat(FlashlightAngleId, Mathf.Cos(_spotlight.spotAngle * 0.5f * Mathf.Deg2Rad));
            Shader.SetGlobalFloat(FlashlightRangeId, _spotlight.range);
            Shader.SetGlobalFloat(FlashlightIntensityId, _spotlight.intensity);
        }

        private void OnDisable()
        {
            // Zero out so particles don't stay lit when flashlight is off
            Shader.SetGlobalFloat(FlashlightIntensityId, 0f);
        }
    }
}
