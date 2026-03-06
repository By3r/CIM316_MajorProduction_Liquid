using UnityEngine;
using UnityEngine.Rendering;

namespace Liquid.Rendering
{
    /// <summary>
    /// Volume component for AMD FidelityFX Contrast Adaptive Sharpening (CAS).
    /// Add this override to any Volume Profile to control sharpening intensity.
    /// </summary>
    [VolumeComponentMenu("Post-processing/CAS Sharpening")]
    public class CASSharpeningVolume : VolumeComponent
    {
        [Tooltip("Sharpening intensity. 0 = off, 0.3 = subtle, 1.0 = strong, 2.0 = very aggressive.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 2f);

        /// <summary>Whether the effect should be rendered. Skipped when intensity is zero.</summary>
        public bool IsActive() => intensity.overrideState && intensity.value > 0f;
    }
}
