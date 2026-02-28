using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Haze.Runtime
{
    [VolumeComponentMenu("Haze/Haze Global Fog")]
    [VolumeRequiresRendererFeatures(typeof(HazeRendererFeature))]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class HazeGlobalFogVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        public HazeGlobalFogVolumeComponent()
        {
            displayName = "Haze Global Fog";
        }

        [Header("Density")]
        [Tooltip("Determines the density of the global fog.")]
        [SerializeField] private MinFloatParameter _globalDensityMultiplier = new(0, 0);
        [Tooltip("Determines the threshold at which the 3D noise will cut away the density of the global fog.")]
        [SerializeField] private FloatParameter _globalDensityThreshold = new(0.2f);
        [Header("Color")]
        [Tooltip("The main color of the global fog.")]
        [SerializeField] private ColorParameter _ambientColor = new(Color.white, true, false, true);
        [Tooltip("The additional color that gets multiplied by the sun light. Increase the HDR intensity for more intense sun rays.")]
        [SerializeField] private ColorParameter _mainLightContribution = new(Color.white, true, false, true);

        [Header("Height fog")]
        [Tooltip("The factor by which the fog is reduced based on height.")]
        [SerializeField] private MinFloatParameter _heightFogFactor = new(0, 0);
        [Tooltip("The maximum height of the global fog.")]
        [SerializeField] private FloatParameter _maxFogHeight = new(0);
        [Tooltip("The smoothness of the global height fog threshold. Values close to 0 will make the height threshold sharper, while negative values will invert the height fog.")]
        [SerializeField] private FloatParameter _heightFogSmoothness = new(0);
        [Tooltip("Makes the height threshold relative to the camera's position instead of world-space height.")]
        [SerializeField] private BoolParameter _cameraRelativeHeightFog = new(false);

        [Header("Lighting")]
        [Tooltip("Only available in Forward+; determines how much additional lights contribute to the color of the global fog.")]
        [SerializeField] private MinFloatParameter _additionalLightContribution = new(1, 0);
        [Tooltip("Determines how much adaptive probe volume illumination contributes to the final color of the global fog.")]
        [SerializeField] private MinFloatParameter _probeVolumeContribution = new(0, 0);
        [Tooltip("The main light scattering amount; values closer to 1 make the main light scatter more into the global fog.")]
        [SerializeField] private ClampedFloatParameter _mainLightScattering = new(1, 0, 1);
        [FormerlySerializedAs("_globalLightDensityBoost")]
        [Tooltip("Increases the density in non-shadow areas; used to enhance the effect of sun rays coming in from the shadows.")]
        [SerializeField] private MinFloatParameter _globalMainLightDensityBoost = new(0, 0);
        [Tooltip("Increases the density of fog based on secondary light attenuation. Use light color alpha value to adjust the density boost per-light.")]
        [SerializeField] private MinFloatParameter _globalSecondaryLightDensityBoost = new(0, 0);
        
        public MinFloatParameter GlobalDensityMultiplier => _globalDensityMultiplier;
        public FloatParameter GlobalDensityThreshold => _globalDensityThreshold;
        public ColorParameter AmbientColor => _ambientColor;
        public ColorParameter MainLightContribution => _mainLightContribution;
        public MinFloatParameter HeightFogFactor => _heightFogFactor;
        public FloatParameter MaxFogHeight => _maxFogHeight;
        public FloatParameter HeightFogSmoothness => _heightFogSmoothness;
        public BoolParameter CameraRelativeHeightFog => _cameraRelativeHeightFog;
        public MinFloatParameter AdditionalLightContribution => _additionalLightContribution;
        public MinFloatParameter ProbeVolumeContribution => _probeVolumeContribution;
        public ClampedFloatParameter MainLightScattering => _mainLightScattering;
        public FloatParameter GlobalMainLightDensityBoost => _globalMainLightDensityBoost;
        public MinFloatParameter GlobalSecondaryLightDensityBoost => _globalSecondaryLightDensityBoost;

        public bool IsActive()
        {
            return active;
        }
    }
}
