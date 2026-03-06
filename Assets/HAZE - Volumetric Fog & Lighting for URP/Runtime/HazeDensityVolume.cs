using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Haze.Runtime.HazeRendererFeature;

namespace Haze.Runtime
{
   [ExecuteAlways]
   public class HazeDensityVolume : MonoBehaviour
   {
      public enum Shape
      {
         Cube,
         Sphere
      }

      public enum VolumeDensityMode
      {
         Additive,
         Subtractive
      }
      
      [Tooltip("The shape of the density volume.")]
      [SerializeField] private Shape _shape = Shape.Cube;

      [Header("Density")]
      [Tooltip("Determines the density of the fog inside the volume.")]
      [SerializeField, Min(0)] private float _density = 1.0f;
      [Tooltip("Determines the threshold at which the 3D noise will cut away the density of the fog inside the volume.")]
      [SerializeField] private float _noiseThreshold = 0.5f;
      [FormerlySerializedAs("volumeDensityMode")]
      [Tooltip("Determines how the volume interacts with the fog density. Default behavior is additive, while subtractive mode can be used to remove fog from the area inside the volume.")]
      [SerializeField] private VolumeDensityMode _densityMode = VolumeDensityMode.Additive;
      
      [Header("Color")]
      [Tooltip("The main color of the fog inside the volume.")]
      [SerializeField, ColorUsage(false, true)] private Color _ambientColor = Color.white;
      [Tooltip("The color gradient the volume uses to modify the fog's ambient color")]
      [SerializeField] private Gradient _colorGradient = new();
      [Tooltip("The additional color that gets multiplied by the sun light. Increase the HDR intensity for more intense sun rays.")]
      [SerializeField, ColorUsage(false, true)] private Color _mainLightContribution = Color.white;
      
      [Header("Height fog")]
      [Tooltip("The factor by which the fog is reduced based on height.")]
      [SerializeField, Min(0)] private float _heightFogFactor = 0;
      [Tooltip("The maximum height of the fog inside the volume.")]
      [SerializeField] private float _maxFogHeight = 0;
      [Tooltip("The smoothness of the height fog threshold. Values close to 0 will make the height threshold sharper, while negative values will invert the height fog.")]
      [SerializeField] private float _heightFogSmoothness = 0.1f;
      [Tooltip("The mode of the height fog. \"Global\" makes the height fog work based on world-space height. \"Local\" makes the height fog work relative to the volume, in which case the max height would range from 0 to 1. \"Camera Relative\" mode makes the maximum height of the fog be relative to the camera's position on the world-space Y axis.")]
      [SerializeField] private HeightFogMode _heightFogMode = HeightFogMode.Global;
      
      [Header("Lighting")]
      [Tooltip("Only available in Forward+; determines how much additional lights contribute to the color of the fog inside the volume.")]
      [SerializeField, Min(0)] private float _additionalLightContribution = 1;
      [Tooltip("Determines how much adaptive probe volume illumination contributes to the final color of the fog inside the volume.")]
      [SerializeField, Min(0)] private float _probeVolumeContribution = 0;
      [FormerlySerializedAs("_mainLightPhase")]
      [Tooltip("The main light scattering amount; values closer to 1 make the main light scatter more into the fog inside the volume.")]
      [SerializeField, Range(0, 1)] private float _mainLightScattering = 1;
      [FormerlySerializedAs("_lightDensityBoost")]
      [Tooltip("Increases the density in non-shadow areas; used to enhance the effect of sun rays coming in from the shadows.")]
      [SerializeField, Min(0)] private float _mainLightDensityBoost = 0.0f;
      [Tooltip("Increases the density of fog based on secondary light attenuation. Use light color alpha value to adjust the density boost per-light.")]
      [SerializeField, Min(0)] private float _secondaryLightDensityBoost = 0.0f;
      
      private Bounds _bounds;
      private HazeDensityVolumeData _densityVolumeData;
      private int _volumeIndex;

      public Shape VolumeShape => _shape;
      public float Density => _densityMode == VolumeDensityMode.Subtractive ?  math.min(-0.01f, -_density) : _density;
      public float NoiseThreshold => _noiseThreshold;
      public VolumeDensityMode DensityMode => _densityMode;
      public Color AmbientColor => _densityMode == VolumeDensityMode.Subtractive ? Color.black : _ambientColor;
      public Gradient ColorGradient => _colorGradient;
      public Color MainLightContribution => _densityMode == VolumeDensityMode.Subtractive ? Color.black : _mainLightContribution;
      public float HeightFogFactor => _heightFogFactor;
      public float MaxFogHeight => _maxFogHeight;
      public float HeightFogSmoothness => _heightFogSmoothness;
      public HeightFogMode VolumeHeightFogMode => _heightFogMode;
      public float4x4 WorldToLocal => transform.worldToLocalMatrix;
      public HazeDensityVolumeData DensityVolumeData => _densityVolumeData;
      public float AdditionalLightContribution => _densityMode == VolumeDensityMode.Subtractive ?  0 : _additionalLightContribution;
      public float ProbeVolumeContribution => _densityMode == VolumeDensityMode.Subtractive ?  0 : _probeVolumeContribution;
      public float MainLightScattering => _densityMode == VolumeDensityMode.Subtractive ?  1 : _mainLightScattering;
      public float MainLightDensityBoost => _densityMode == VolumeDensityMode.Subtractive ?  0 : _mainLightDensityBoost;
      public float SecondaryLightDensityBoost => _secondaryLightDensityBoost;
      public int VolumeIndex
      {
         get => _volumeIndex;
      }

      private void Update()
      {
         if (Application.isPlaying && gameObject.isStatic)
         {
            return;
         }
         
         UpdateData();
      }

      private void UpdateData()
      {
         CalculateBounds();
         _densityVolumeData.SetData(this);
      }

      private void CalculateBounds()
      {
         _bounds = new Bounds(transform.position, Vector3.zero);
         _bounds.Encapsulate(transform.TransformPoint(new float3(-0.5f, -0.5f, -0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3(-0.5f, -0.5f,  0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3(-0.5f,  0.5f, -0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3(-0.5f,  0.5f,  0.5f)));
            
         _bounds.Encapsulate(transform.TransformPoint(new float3( 0.5f, -0.5f, -0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3( 0.5f, -0.5f,  0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3( 0.5f,  0.5f, -0.5f)));
         _bounds.Encapsulate(transform.TransformPoint(new float3( 0.5f,  0.5f,  0.5f)));
      }

      public bool IsWithinCameraFrustum(Plane[] cameraPlanes)
      {
         return _density == 0 || GeometryUtility.TestPlanesAABB(cameraPlanes, _bounds);
      }
      
              
      public bool IsWithinRange(Vector3 position, float distance)
      {
         return _bounds.Contains(position) || Vector3.Distance(_bounds.ClosestPoint(position), position) < distance;
      }

      public void ReassignIndex(int index)
      {
         _volumeIndex = index;
         _densityVolumeData.SetData(this);
      }

      private void OnValidate()
      {
         if (!Application.isPlaying)
         {
            UpdateVolumeGradientTexture();
         }
      }

      private void OnEnable()
      {
         _densityVolumeData = new HazeDensityVolumeData();
         _volumeIndex = AddVolume(this);
         UpdateData();
      }

      private void OnDisable()
      {
         RemoveVolume(this);
      }

      private void OnDrawGizmosSelected()
      {
         Gizmos.matrix = transform.localToWorldMatrix;
         if (_shape == Shape.Cube)
         {
            Gizmos.DrawWireCube(float3.zero, Vector3.one);
         }
         else
         {
            Gizmos.DrawWireSphere(float3.zero, 0.5f);
         }
      }
   }

}
