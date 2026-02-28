using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Haze.Runtime
{
    public sealed class HazeRendererFeature : ScriptableRendererFeature
    {
        public enum HeightFogMode
        {
            Global,
            Local,
            CameraRelative
        }

        public enum Resolution
        {
            _16 = 0,
            _32 = 1,
            _64 = 2,
            _128 = 3,
            _256 = 4
        }

        public enum AspectRatioAdjustment
        {
            None = 0,
            Upscale = 1,
            Downscale = 2
        }
        
#region Density Volume Data

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct HazeDensityVolumeData
        {
            public float4x4 WorldToLocal;
            public float Shape;
            public float Density;
            public float MainLightDensityBoost;
            public float SecondaryLightDensityBoost;
            public float NoiseThreshold;
            public float3 AmbientColor;
            public float3 LightContribution;
            public float4 HeightFog;
            public float AdditionalLightContribution;
            public float ProbeVolumeContribution;
            public float MainLightPhase;
            public float GradientSamplingIndex;

            public static int SizeInBytes => sizeof(float) * ((4 * 4) + 1 + 1 + 1 + 1 + 1 + 3 + 3 + 4 + 1 + 1 + 1 + 1);
            
            public void SetData(HazeDensityVolume densityVolume)
            {
                var ambientColor = densityVolume.AmbientColor;
                var lightContribution = densityVolume.MainLightContribution;

                WorldToLocal = densityVolume.WorldToLocal;
                Shape = (float)densityVolume.VolumeShape;
                Density = densityVolume.Density;
                MainLightDensityBoost = densityVolume.MainLightDensityBoost;
                SecondaryLightDensityBoost = densityVolume.SecondaryLightDensityBoost;
                NoiseThreshold = densityVolume.NoiseThreshold;
                AmbientColor = new float3(ambientColor.r, ambientColor.g, ambientColor.b);
                LightContribution = new float3(lightContribution.r, lightContribution.g, lightContribution.b);
                HeightFog = new float4(densityVolume.MaxFogHeight, densityVolume.HeightFogSmoothness,
                    densityVolume.HeightFogFactor, (float)densityVolume.VolumeHeightFogMode);
                AdditionalLightContribution = densityVolume.AdditionalLightContribution;
                ProbeVolumeContribution = densityVolume.ProbeVolumeContribution;
                MainLightPhase = densityVolume.MainLightScattering;
                GradientSamplingIndex = densityVolume.VolumeIndex;
            }
        }

        private HazeDensityVolumeData[] _densityVolumeData;
        private GraphicsBuffer _densityVolumeDataBuffer;

        private static Texture2D _volumeGradientTexture; 
        
        private const int MaximumDensityVolumes = 16;

        private static Camera _currentCamera;
        private static readonly List<HazeDensityVolume> DensityVolumes = new();
        private readonly List<HazeDensityVolume> _visibleDensityVolumes = new();
        private readonly Plane[] _planeArray = new Plane[6];

#endregion

        [Serializable]
        public class NoiseData
        {
            [Tooltip("The tiling of the noise texture. Increase for higher detail frequency.")]
            [SerializeField] internal float noiseTiling = 0.1f;
            [Tooltip("The panning speed of the noise texture in each direction.")]
            [SerializeField] internal float3 noisePanningSpeed = new(0, 0, 0);
            [Tooltip("The weights of the noise texture. Each component is multiplied by the corresponding channel (RGBA) of the noise texture.")]
            [SerializeField] internal float4 noiseWeights = new(1, 1, 1, 1);
        }

        [Serializable]
        public class MultipleScatteringData
        {
            [Tooltip("Determines the blend between regular fog and fog with screen-space multiple scattering. Set to 0 to completely disable multiple scattering.")]
            [SerializeField] [Range(0,1)] internal float intensity = 1.0f;
            [Tooltip("Determines the brightness of the blurred image that gets composited with the fog. The lower the value, the brighter the result.")]
            [SerializeField] [Min(0.01f)] internal float radius = 1.0f;
            [Tooltip("Determines the blur amount of the multiple scattering buffer. A lower value will make the blur effect less intense.")]
            [SerializeField] [Range(0, 1)] internal float scatter = 1.0f;
            [Tooltip("Determines the brightness threshold for the multiple scattering pre-filtering. A value of 0 does no filtering, blurring the whole image, while a larger value will only blur brighter parts of the image.")]
            [SerializeField] [Range(0, 1)] internal float threshold = 0.0f;
            [Tooltip("Determines the maximum number of blur iterations for the multiple scattering effect. A larger amount of iterations will result in a increased blurring distance, but it will increase performance overhead.")]
            [SerializeField] [Range(3, 10)] internal int maxIterations = 5;
        }

        public struct FroxelFogPassSettings
        {
            public int3 Resolution;
            public float2 FroxelFogRange;
            public ComputeShader FroxelFogComputeShader;
            public Texture3D NoiseTexture;
            public NoiseData NoiseData;
            public float TemporalAccumulationBlending;
            public float MainLightShadowBias;
        }
        
        private static readonly int VolumeNearClipPlane = Shader.PropertyToID("_VolumeNearClipPlane");
        private static readonly int VolumeFarClipPlane = Shader.PropertyToID("_VolumeFarClipPlane");
        private static readonly int SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
        private static readonly int BloomParams = Shader.PropertyToID("_Params");
        private static readonly int BloomIntensity = Shader.PropertyToID("_HazeBloomIntensity");
        private static readonly int FroxelVolumeVp = Shader.PropertyToID("_FroxelVolumeVP");
        private static readonly int SampleScale = Shader.PropertyToID("_SampleScale");
        private static readonly int BlurRadius = Shader.PropertyToID("_HazeBlurRadius");
        private static readonly int IgnStrength = Shader.PropertyToID("_IGNStrength");

        [Header("Shaders")]
        [SerializeField, HideInInspector] private ComputeShader _froxelFogComputeShader;
        [SerializeField, HideInInspector] private Shader _froxelFogCompositeShader;
        [SerializeField, HideInInspector] private Shader _bloomShader;

        [SerializeField] private bool _renderBeforeTransparents;
        
        [Header("Resolution")]
        [Tooltip("The resolution for the width and height of the froxel buffers.")]
        [SerializeField] private Resolution _froxelBufferResolution = Resolution._128;
        [Tooltip("Aspect ratio adjustment mode for froxel resolution. Set to none to keep X and Y resolution equal. Upscale will increase the X/Y resolution to match the aspect ratio, while Downscale will reduce the Y/X resolution.")]
        [SerializeField] private AspectRatioAdjustment _aspectRatioAdjustment = AspectRatioAdjustment.None;
        [Tooltip("The amount of depth slices of the froxel buffers.")]
        [SerializeField] private Resolution _froxelBufferDepth = Resolution._64;
        [Tooltip("The near and far clipping planes of the froxel fog effect.")]
        [SerializeField] private float2 _froxelFogRange = new(0.1f, 500.0f);
        [Tooltip("Toggles tricubic sampling of the fog buffer. Enabling it greatly reduces aliasing artifacts at a small performance cost.")]
        [SerializeField] private bool _tricubicSampling = true;
        [Tooltip("Adjusts the strength of the interleaved gradient noise (IGN) which reduces artifacts when using TAA.")]
        [SerializeField, Range(0,1)] private float _interleavedGradientNoiseStrength = 1.0f;

        [Header("Temporal accumulation")]
        [Tooltip("Controls the temporal accumulation blending. Set to 0 to disable temporal accumulation.")]
        [SerializeField, Range(0, 0.99f)] private float _temporalAccumulationBlending = 0.95f;

        [Header("Lighting settings")] 
        [Tooltip("Main light shadow bias to help with light leaking from walls.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float _mainLightShadowBias = 0.0f;
        
        [Header("Volume settings")]
        [Tooltip("Maximum distance at which density volumes are considered visible.")]
        [SerializeField] private float _maximumVolumeDistance = 100;

        [Header("Noise")]
        [Tooltip("The 3D noise texture used for the fog density")]
        [SerializeField] private Texture3D _noiseTexture;
        [SerializeField] private NoiseData _noiseData = new();

        [Header("Multiple Scattering")]
        [SerializeField] private MultipleScatteringData _multipleScatteringData = new();
        
        private static FroxelFogRenderPass _froxelFogRenderPass;
        private static FroxelFogCompositePass _froxelFogCompositePass;
        private static MultipleScatteringPass _multipleScatteringPass;

#if UNITY_EDITOR
        private static bool _initialized = false;
#endif

        #region Density Volume Methods
        
        private static int DensityVolumeSorting(HazeDensityVolume a, HazeDensityVolume b)
        {
            var biasA = a.Density < 0 ? 0 : 1000;
            var biasB = b.Density < 0 ? 0 : 1000;
            var distanceComparison = (math.distancesq(_currentCamera.transform.position, a.transform.position) + biasA)
                .CompareTo(math.distancesq(_currentCamera.transform.position, b.transform.position) + biasB);
            return distanceComparison;
        }

        private void UpdateVolumeVisibility()
        {
            if (_currentCamera == null || (Application.isPlaying && (!_currentCamera.transform.hasChanged || _currentCamera.cameraType == CameraType.SceneView)) || _currentCamera.cameraType == CameraType.Preview)
            {
                return;
            }

            _currentCamera.transform.hasChanged = false;

            GeometryUtility.CalculateFrustumPlanes(_currentCamera, _planeArray);
            _visibleDensityVolumes.Clear();

            foreach (var densityVolume in DensityVolumes)
            {
                if (densityVolume.IsWithinRange(_currentCamera.transform.position, _maximumVolumeDistance)
                    && densityVolume.IsWithinCameraFrustum(_planeArray))
                {
                    _visibleDensityVolumes.Add(densityVolume);
                }
            }

            _visibleDensityVolumes.Sort(DensityVolumeSorting);
            _froxelFogRenderPass?.UpdateVisibleDensityVolumeCount(_visibleDensityVolumes.Count);
        }

        private void InitializeDensityVolumeData()
        {
            _densityVolumeData = new HazeDensityVolumeData[MaximumDensityVolumes];
            for (var i = 0; i < _densityVolumeData.Length; i++)
            {
                _densityVolumeData[i] = new HazeDensityVolumeData();
            }

            _densityVolumeDataBuffer?.Release();
            _densityVolumeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaximumDensityVolumes, HazeDensityVolumeData.SizeInBytes);
            _froxelFogRenderPass?.UpdateDensityVolumeDataBuffer(_densityVolumeDataBuffer);
        }

        private void AssignDensityVolumeData()
        {
            if (_visibleDensityVolumes.Count <= 0)
            {
                return;
            }

            for (var i = 0; i < _visibleDensityVolumes.Count; i++)
            {
                _densityVolumeData[i] = _visibleDensityVolumes[i].DensityVolumeData;
            }
            _densityVolumeDataBuffer.SetData(_densityVolumeData);
        }

        private static void NotifyVisibilityUpdate()
        {
            if (Application.isPlaying && _currentCamera != null)
            {
                _currentCamera.transform.hasChanged = true;
            }
        } 

        public static int AddVolume(HazeDensityVolume densityVolume)
        {
            if (!DensityVolumes.Contains(densityVolume))
            {
                DensityVolumes.Add(densityVolume);
                UpdateVolumeGradientTexture();
                NotifyVisibilityUpdate();
                return DensityVolumes.Count - 1;
            }

            return -1;
        }

        public static void RemoveVolume(HazeDensityVolume densityVolume)
        {
            if (DensityVolumes.Contains(densityVolume))
            {
                DensityVolumes.Remove(densityVolume);
                
                // Re-assign indices
                for (var i = 0; i < DensityVolumes.Count; i++)
                {
                    DensityVolumes[i].ReassignIndex(i);
                }
                UpdateVolumeGradientTexture(false);
                NotifyVisibilityUpdate();
            }
        }

        public static void UpdateVolumeGradientTexture(bool recreate = true)
        {
            if (DensityVolumes.Count <= 0)
            {
                _volumeGradientTexture = Texture2D.whiteTexture;
                return;
            }

            if (recreate)
            {
                _volumeGradientTexture = new Texture2D(16, 2 * DensityVolumes.Count);
            }
            for (var i = 0; i < DensityVolumes.Count * 2; i += 2)
            {
                var gradient = DensityVolumes[i / 2].ColorGradient;
                for (var j = 0; j < 16; j++)
                {
                    var col = gradient.Evaluate(j / 16.0f);
                    _volumeGradientTexture.SetPixel(j, i, col);
                    _volumeGradientTexture.SetPixel(j, i + 1, col);
                }
            }
            
            _volumeGradientTexture.Apply();
            _froxelFogRenderPass?.UpdateVolumeGradientTexture(_volumeGradientTexture);
        }

#endregion

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            _currentCamera = cameraData.camera;
            UpdateVolumeVisibility();
            AssignDensityVolumeData();

#if UNITY_EDITOR
            if (!_initialized && _currentCamera != null)
            {
                Create();
                EditorSceneManager.sceneOpened += OnSceneOpened;
                _initialized = true;
            }
#endif
        }

#if UNITY_EDITOR
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            _initialized = false;
        }
#endif

        public override void Create()
        {
            Dispose();
            //Filthy hack to get around AdditionalLightsCookieAtlasTexture not being assigned when there's no additional lights using cookies.
            //Leads to the CS breaking for an unassigned texture, even if we only use cookies on the directional light.
            Shader.SetGlobalTexture(Shader.PropertyToID("_AdditionalLightsCookieAtlasTexture"), Texture2D.whiteTexture);
            Shader.SetGlobalTexture(Shader.PropertyToID("_MainLightCookieTexture"), Texture2D.whiteTexture);

            _currentCamera = Camera.main;

            if (_froxelFogComputeShader == null)
            {
                return;
            }

            //Force first visibility update
            if (_currentCamera != null)
            {
                _currentCamera.transform.hasChanged = true;
            }

            var aspectRatio = _aspectRatioAdjustment switch
            {
                AspectRatioAdjustment.None => 1.0f,
                _ => _currentCamera ? _currentCamera.aspect : 1f
            };

            var bufferResolution = 16 << (int)_froxelBufferResolution;
            var bufferDepth = 16 << (int)_froxelBufferDepth;
            var resolution = new int3(bufferResolution, bufferResolution, bufferDepth);
            switch (_aspectRatioAdjustment)
            {
                case AspectRatioAdjustment.Upscale:
                    if (aspectRatio > 1.0f)
                    {
                        resolution.x = (int)(resolution.x * aspectRatio);
                    }
                    else
                    {
                        resolution.y = (int)(resolution.y * math.rcp(aspectRatio));
                    }
                    break;
                case AspectRatioAdjustment.Downscale:
                    if (aspectRatio > 1.0f)
                    {
                        resolution.y = (int)(resolution.y * math.rcp(aspectRatio));
                    }
                    else
                    {
                        resolution.x = (int)(resolution.x * aspectRatio);
                    }
                    break;
                case AspectRatioAdjustment.None:
                default:
                    break;
            }

            var froxelFogPassSettings = new FroxelFogPassSettings
            {
                Resolution = resolution,
                FroxelFogRange = _froxelFogRange,
                FroxelFogComputeShader = _froxelFogComputeShader,
                NoiseTexture = _noiseTexture,
                NoiseData = _noiseData,
                TemporalAccumulationBlending = _temporalAccumulationBlending,
                MainLightShadowBias = _mainLightShadowBias
            };
            
            _froxelFogRenderPass = new FroxelFogRenderPass(froxelFogPassSettings)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };

            if (_froxelFogCompositeShader == null)
            {
                return;
            }
            
            _froxelFogCompositePass = new FroxelFogCompositePass(_froxelFogCompositeShader, _tricubicSampling, _multipleScatteringData, _interleavedGradientNoiseStrength)
            {
                renderPassEvent = _renderBeforeTransparents ? RenderPassEvent.BeforeRenderingTransparents : RenderPassEvent.BeforeRenderingPostProcessing
            };

            InitializeDensityVolumeData();
            UpdateVolumeGradientTexture();

            if (_bloomShader == null)
            {
                return;
            }

            _multipleScatteringPass = new MultipleScatteringPass(_bloomShader, _froxelFogRange, _multipleScatteringData)
            {
                renderPassEvent = _renderBeforeTransparents ? RenderPassEvent.AfterRenderingSkybox : RenderPassEvent.AfterRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.Reflection || !renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            var hazeGlobalFog = VolumeManager.instance.stack?.GetComponent<HazeGlobalFogVolumeComponent>();
            if ((hazeGlobalFog == null || !hazeGlobalFog.active || hazeGlobalFog.GlobalDensityMultiplier.value <= 0) &&
                _visibleDensityVolumes.Count <= 0)
            {
                return;
            }

            if (_froxelFogComputeShader == null)
            {
                return;
            }
            
            _froxelFogRenderPass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);

            renderer.EnqueuePass(_froxelFogRenderPass);

            var multipleScatteringIntensity = _multipleScatteringData.intensity;
                
            var hazeSettingsOverrides = VolumeManager.instance.stack?.GetComponent<HazeOverridesVolumeComponent>();
            if (hazeSettingsOverrides != null && hazeSettingsOverrides.IsActive())
            {
                if (hazeSettingsOverrides.MultipleScatteringIntensity.overrideState)
                {
                    multipleScatteringIntensity = hazeSettingsOverrides.MultipleScatteringIntensity.value;
                }
            }
            
            if (_multipleScatteringPass != null && multipleScatteringIntensity > 0)
            {
                renderer.EnqueuePass(_multipleScatteringPass);
            }
            
            if (_froxelFogCompositePass == null)
            {
                return;
            }

            renderer.EnqueuePass(_froxelFogCompositePass);
        }

        protected override void Dispose(bool disposing)
        {
            _froxelFogRenderPass?.Dispose();
            _froxelFogCompositePass?.Dispose();
            _multipleScatteringPass?.Dispose();

            _densityVolumeDataBuffer?.Release();
            _densityVolumeDataBuffer = null;
            
#if UNITY_EDITOR
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            _initialized = false;
#endif
        }

        private class FroxelFogRenderPass : ScriptableRenderPass
        {
            private class PassData
            {
                internal TextureHandle WriteBuffer;
                internal TextureHandle ReadBuffer;
            }

            private class DensityGatherPassData
            {
                internal TextureHandle WriteBuffer;
                internal TextureHandle ReadBuffer;
                internal TextureHandle NoiseTexture;
                internal Matrix4x4 InverseViewProjectionMatrix;
                internal Matrix4x4 PrevViewProjectionMatrix;
                internal float4 ZBufferParameters;
                internal bool TemporalReprojection;
                internal GraphicsBuffer LightAlphaBuffer;
                internal BufferHandle DensityVolumeDataBuffer;
                internal TextureHandle VolumeGradientTexture;
            }
            
            private GraphicsBuffer _densityVolumeDataBuffer;
            private GraphicsBuffer _lightAlphaBuffer;
            private int _visibleDensityVolumes;
            
            private readonly ComputeShader _froxelFogComputeShader;
            private readonly RenderTexture _colorDensityBuffer;
            private readonly RenderTexture _colorDensityHistoryBuffer;
            private readonly RenderTexture _scatterBuffer;
            
            private readonly RTHandle _noiseTextureHandle;
            private RTHandle _colorDensityBufferHandle;
            private RTHandle _colorDensityHistoryBufferHandle;
            private readonly RTHandle _volumeNoiseTextureHandle; 
            private readonly RTHandle _scatterBufferHandle;
            private RTHandle _volumeGradientTextureHandle;
            
            private readonly int3 _resolution;
            private readonly float2 _froxelFogRange;
            private readonly RenderTargetInfo _renderTargetInfo;
            private readonly int3 _densityGatherThreadGroups;
            private readonly int3 _scatterThreadGroups;
            private readonly float _temporalAccumulationBlending;
            private readonly float _mainLightShadowBias;
            
            private Matrix4x4 _prevViewProjectionMatrix;

            private const int DensityGatherKernelIndex = 0;
            private const int ScatterKernelIndex = 1;

            private readonly NoiseData _noiseData;
            private readonly Texture3D _fallbackNoiseTexture;

            private void InitializeRenderTexture(ref RenderTexture renderTexture)
            {
                if (renderTexture != null)
                {
                    return;
                }

                renderTexture = new RenderTexture(_resolution.x, _resolution.y, 0, GraphicsFormat.R16G16B16A16_SFloat)
                {
                    volumeDepth = _resolution.z,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true
                };
                renderTexture.Create();
            }

            public void UpdateDensityVolumeDataBuffer(in GraphicsBuffer densityVolumeDataBuffer)
            {
                _densityVolumeDataBuffer = densityVolumeDataBuffer;
            }
            
            public void UpdateVolumeGradientTexture(in Texture2D volumeGradientTexture)
            {
                _volumeGradientTextureHandle?.Release();
                _volumeGradientTextureHandle = RTHandles.Alloc(volumeGradientTexture);
            }

            public void UpdateVisibleDensityVolumeCount(int count)
            {
                _visibleDensityVolumes = count;
            }

            public FroxelFogRenderPass(FroxelFogPassSettings settings)
            {
                profilingSampler = new ProfilingSampler("Haze Froxel Fog");
                _resolution = settings.Resolution;
                _froxelFogComputeShader = settings.FroxelFogComputeShader;
                _froxelFogRange = settings.FroxelFogRange;
                _noiseData = settings.NoiseData;
                _temporalAccumulationBlending = settings.TemporalAccumulationBlending;
                _mainLightShadowBias = settings.MainLightShadowBias;
                
                InitializeRenderTexture(ref _colorDensityBuffer);
                InitializeRenderTexture(ref _colorDensityHistoryBuffer);
                InitializeRenderTexture(ref _scatterBuffer);

                _renderTargetInfo = new RenderTargetInfo()
                {
                    format = GraphicsFormat.R16G16B16A16_SFloat,
                    width = _resolution.x,
                    height = _resolution.y,
                    volumeDepth = _resolution.z,
                    msaaSamples = 1
                };

                _colorDensityBufferHandle = RTHandles.Alloc(_colorDensityBuffer);
                _colorDensityHistoryBufferHandle = RTHandles.Alloc(_colorDensityHistoryBuffer);
                _scatterBufferHandle = RTHandles.Alloc(_scatterBuffer);

                _fallbackNoiseTexture = new Texture3D(1, 1, 1, GraphicsFormat.R16G16B16A16_SFloat,
                    TextureCreationFlags.DontInitializePixels);
                _fallbackNoiseTexture.SetPixel(0,0,0,Color.white);
                _fallbackNoiseTexture.Apply();

                _noiseTextureHandle = RTHandles.Alloc(settings.NoiseTexture == null ? _fallbackNoiseTexture : settings.NoiseTexture);
                _volumeGradientTextureHandle = RTHandles.Alloc(Texture2D.whiteTexture);
                
                _froxelFogComputeShader.GetKernelThreadGroupSizes(DensityGatherKernelIndex, out var threadGroupSizesX, out var threadGroupSizesY, out var threadGroupSizesZ);
                _densityGatherThreadGroups = new int3(  Mathf.CeilToInt((float) _resolution.x / threadGroupSizesX), 
                                                        Mathf.CeilToInt((float) _resolution.y / threadGroupSizesY), 
                                                        Mathf.CeilToInt((float) _resolution.z / threadGroupSizesZ));
                
                _froxelFogComputeShader.GetKernelThreadGroupSizes(ScatterKernelIndex, out threadGroupSizesX, out threadGroupSizesY, out threadGroupSizesZ);
                _scatterThreadGroups = new int3(Mathf.CeilToInt((float) _resolution.x / threadGroupSizesX), 
                                                Mathf.CeilToInt((float) _resolution.y / threadGroupSizesY), 
                                                Mathf.CeilToInt((float) _resolution.z / threadGroupSizesZ));
            }

            private class CustomData : ContextItem
            {
                public TextureHandle IntermediateBuffer;
                
                public override void Reset()
                {
                    IntermediateBuffer = TextureHandle.nullHandle;
                }
            }

            private void ExecuteDensityGatherPass(DensityGatherPassData data, ComputeGraphContext context)
            {
                context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_ZBufferParameters", data.ZBufferParameters);
                context.cmd.SetComputeMatrixParam(_froxelFogComputeShader, "_InverseViewProjectionMatrix", data.InverseViewProjectionMatrix);
                context.cmd.SetComputeMatrixParam(_froxelFogComputeShader, "_PrevViewProjectionMatrix", data.PrevViewProjectionMatrix);
                
                context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_TemporalReprojection", data.TemporalReprojection ? 1 : 0);
                context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_TemporalAccumulationBlending", 1.0f - _temporalAccumulationBlending);
                context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_MainLightShadowBias", _mainLightShadowBias);

                var noiseTiling = _noiseData.noiseTiling;
                var noisePanningSpeed = _noiseData.noisePanningSpeed;
                var noiseWeights = _noiseData.noiseWeights;
                
                var hazeSettingsOverrides = VolumeManager.instance.stack?.GetComponent<HazeOverridesVolumeComponent>();
                if (hazeSettingsOverrides != null && hazeSettingsOverrides.IsActive())
                {
                    if (hazeSettingsOverrides.NoiseTiling.overrideState)
                    {
                        noiseTiling = hazeSettingsOverrides.NoiseTiling.value;
                    }

                    if (hazeSettingsOverrides.NoisePanningSpeed.overrideState)
                    {
                        noisePanningSpeed = hazeSettingsOverrides.NoisePanningSpeed.value;
                    }

                    if (hazeSettingsOverrides.NoiseWeights.overrideState)
                    {
                        noiseWeights = hazeSettingsOverrides.NoiseWeights.value;
                    }
                }
                
                context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_GlobalNoisePanningTiling", new float4(noisePanningSpeed, noiseTiling));
                context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_GlobalNoiseWeights", noiseWeights);
                context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_VisibleDensityVolumes", _visibleDensityVolumes);

                //Volume parameters
                var globalFogVolumeComponent = VolumeManager.instance.stack?.GetComponent<HazeGlobalFogVolumeComponent>();

                if (globalFogVolumeComponent != null && globalFogVolumeComponent.active)
                {
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalDensityMultiplier", globalFogVolumeComponent.GlobalDensityMultiplier.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalDensityThreshold", globalFogVolumeComponent.GlobalDensityThreshold.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalMainLightDensityBoost", globalFogVolumeComponent.GlobalMainLightDensityBoost.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalSecondaryLightDensityBoost", globalFogVolumeComponent.GlobalSecondaryLightDensityBoost.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_LightScattering", globalFogVolumeComponent.MainLightScattering.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalAdditionalLightContribution", globalFogVolumeComponent.AdditionalLightContribution.value);
                    context.cmd.SetComputeFloatParam(_froxelFogComputeShader, "_GlobalProbeVolumeContribution", globalFogVolumeComponent.ProbeVolumeContribution.value);
                    context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_AmbientColor", globalFogVolumeComponent.AmbientColor.value);
                    context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_GlobalMainLightContribution", globalFogVolumeComponent.MainLightContribution.value);
                    context.cmd.SetComputeVectorParam(_froxelFogComputeShader, "_GlobalHeightFog", new float4(globalFogVolumeComponent.MaxFogHeight.value,
                    globalFogVolumeComponent.HeightFogSmoothness.value, globalFogVolumeComponent.HeightFogFactor.value, globalFogVolumeComponent.CameraRelativeHeightFog.value ? 1 : 0));
                }

                context.cmd.SetComputeTextureParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_ColorDensityBuffer", data.WriteBuffer);
                context.cmd.SetComputeTextureParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_ColorDensityReadBuffer", data.ReadBuffer);
                context.cmd.SetComputeTextureParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_GlobalNoiseTexture", data.NoiseTexture);
                context.cmd.SetComputeTextureParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_VolumeGradientTexture", data.VolumeGradientTexture);
                
                context.cmd.SetComputeBufferParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_HazeDensityVolumeBuffer", data.DensityVolumeDataBuffer);
                context.cmd.SetComputeBufferParam(_froxelFogComputeShader, DensityGatherKernelIndex, "_SecondaryLightAlphaBuffer", data.LightAlphaBuffer);
                
                context.cmd.DispatchCompute(_froxelFogComputeShader, DensityGatherKernelIndex, _densityGatherThreadGroups.x, _densityGatherThreadGroups.y, _densityGatherThreadGroups.z);
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                
                var visibleLights = lightData.visibleLights;
                var visiblePunctualLights = new List<VisibleLight>();
                foreach (var visibleLight in visibleLights)
                {
                    if (visibleLight.lightType is LightType.Spot or LightType.Point)
                    {
                        visiblePunctualLights.Add(visibleLight);
                    }
                }

                var lightAlphaArray = new []{1.0f};

                if (visiblePunctualLights.Count > 0)
                {
                    lightAlphaArray = new float[visiblePunctualLights.Count];
                    for (var i = 0; i < visiblePunctualLights.Count; i++)
                    {
                        lightAlphaArray[i] = visiblePunctualLights[i].light.color.a;
                    }
                }
                
                _lightAlphaBuffer?.Release();
                _lightAlphaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lightAlphaArray.Length, sizeof(float));
                _lightAlphaBuffer.SetData(lightAlphaArray);

                var colorDensityBuffer = renderGraph.ImportTexture(_colorDensityBufferHandle, _renderTargetInfo);
                var colorDensityHistoryBuffer = renderGraph.ImportTexture(_colorDensityHistoryBufferHandle, _renderTargetInfo);
                var scatterBuffer = renderGraph.ImportTexture(_scatterBufferHandle, _renderTargetInfo);
                var noiseTexture = renderGraph.ImportTexture(_noiseTextureHandle);
                var volumeGradientTexture = renderGraph.ImportTexture(_volumeGradientTextureHandle);
                var densityVolumeDataBuffer = renderGraph.ImportBuffer(_densityVolumeDataBuffer);

                using (var builder = renderGraph.AddComputePass("Color density accumulation", out DensityGatherPassData passData, profilingSampler))
                {
                    passData.WriteBuffer = colorDensityBuffer;
                    passData.ReadBuffer = colorDensityHistoryBuffer;
                    passData.NoiseTexture = noiseTexture;
                    passData.PrevViewProjectionMatrix = _prevViewProjectionMatrix;
                    passData.LightAlphaBuffer = _lightAlphaBuffer;
                    passData.DensityVolumeDataBuffer = densityVolumeDataBuffer;
                    passData.VolumeGradientTexture = volumeGradientTexture;
                    
                    builder.UseTexture(passData.WriteBuffer, AccessFlags.Write);
                    builder.UseTexture(passData.ReadBuffer);
                    builder.UseTexture(passData.NoiseTexture);
                    builder.UseTexture(passData.VolumeGradientTexture);
                    builder.UseBuffer(passData.DensityVolumeDataBuffer);
                    builder.AllowPassCulling(false);

                    var customData = frameData.Create<CustomData>();
                    customData.IntermediateBuffer = colorDensityBuffer;
                    
                    var farDivNear = _froxelFogRange.y / _froxelFogRange.x;
                    var zBufferParameters = new float4(1.0f - farDivNear, farDivNear, _froxelFogRange.x, _froxelFogRange.y);
                    
                    var viewMatrix = cameraData.camera.worldToCameraMatrix;
                    var projMatrix = Matrix4x4.Perspective(cameraData.camera.GetGateFittedFieldOfView(), cameraData.camera.aspect, _froxelFogRange.x, _froxelFogRange.y);
                    var viewProjectionMatrix = projMatrix * viewMatrix;
                    
                    Shader.SetGlobalFloat(VolumeNearClipPlane, _froxelFogRange.x);
                    Shader.SetGlobalFloat(VolumeFarClipPlane, _froxelFogRange.y);
                    Shader.SetGlobalMatrix(FroxelVolumeVp, viewProjectionMatrix);

                    passData.InverseViewProjectionMatrix = Matrix4x4.Inverse(viewProjectionMatrix);
                    passData.ZBufferParameters = zBufferParameters;
                    passData.TemporalReprojection = (Application.isPlaying && cameraData.cameraType == CameraType.Game) ||
                                                    (!Application.isPlaying && cameraData.cameraType == CameraType.SceneView);
                    
                    builder.SetRenderFunc((DensityGatherPassData data, ComputeGraphContext context) => ExecuteDensityGatherPass(data, context));

                    if (passData.TemporalReprojection)
                    {
                        (_colorDensityBufferHandle, _colorDensityHistoryBufferHandle) = (_colorDensityHistoryBufferHandle, _colorDensityBufferHandle);
                        // Orthographic projection doesn't use VP matrix; assign previous view matrix instead
                        _prevViewProjectionMatrix = cameraData.camera.orthographic ? viewMatrix : viewProjectionMatrix;
                    }
                }

                using (var builder = renderGraph.AddComputePass("Scatter", out PassData passData, profilingSampler))
                {
                    var customData = frameData.Get<CustomData>();
                    passData.WriteBuffer = scatterBuffer;
                    passData.ReadBuffer = customData.IntermediateBuffer;

                    customData.IntermediateBuffer = scatterBuffer;
                    builder.UseTexture(passData.WriteBuffer, AccessFlags.Write);
                    builder.UseTexture(passData.ReadBuffer);
                    builder.AllowPassCulling(false);
                    
                    builder.SetGlobalTextureAfterPass(passData.WriteBuffer, Shader.PropertyToID("_ScatterBuffer"));
                    
                    builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeTextureParam(_froxelFogComputeShader, ScatterKernelIndex, "_ScatterBuffer", data.WriteBuffer);
                        context.cmd.SetComputeTextureParam(_froxelFogComputeShader, ScatterKernelIndex, "_ColorDensityReadBuffer", data.ReadBuffer);
                        context.cmd.DispatchCompute(_froxelFogComputeShader, ScatterKernelIndex, _scatterThreadGroups.x, _scatterThreadGroups.y, _densityGatherThreadGroups.z);
                    });
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_colorDensityBuffer);
                CoreUtils.Destroy(_scatterBuffer);
                
                _colorDensityBufferHandle.Release();
                _colorDensityHistoryBufferHandle.Release();
                _scatterBufferHandle.Release();
                _noiseTextureHandle.Release();

                _lightAlphaBuffer?.Release();
                _lightAlphaBuffer = null;
            }
        }

        private class FroxelFogCompositePass : ScriptableRenderPass
        {
            private readonly Material _froxelFogCompositeMaterial;
            private readonly bool _tricubicSampling;
            private readonly float _interleavedGradientNoiseStrength;
            private readonly MultipleScatteringData _multipleScatteringData;

            public FroxelFogCompositePass(Shader froxelFogCompositeShader, bool tricubicSampling, MultipleScatteringData multipleScatteringData, float interleavedGradientNoiseStrength)
            {
                _froxelFogCompositeMaterial = CoreUtils.CreateEngineMaterial(froxelFogCompositeShader);
                _tricubicSampling = tricubicSampling;
                _multipleScatteringData = multipleScatteringData;
                _interleavedGradientNoiseStrength = interleavedGradientNoiseStrength;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var srcCamColor = resourceData.activeColorTexture;
                var textureDesc = srcCamColor.GetDescriptor(renderGraph);

                textureDesc.depthBufferBits = 0;
                
                var copiedTexture = renderGraph.CreateTexture(textureDesc);

                if (_tricubicSampling)
                {
                    _froxelFogCompositeMaterial.EnableKeyword("TRICUBIC_SAMPLING");
                }
                else
                {
                    _froxelFogCompositeMaterial.DisableKeyword("TRICUBIC_SAMPLING");
                }

                Shader.SetGlobalFloat(IgnStrength, _interleavedGradientNoiseStrength);
                
                var multipleScatteringIntensity = _multipleScatteringData.intensity;
                var multipleScatteringRadius = _multipleScatteringData.radius;
                
                var hazeSettingsOverrides = VolumeManager.instance.stack?.GetComponent<HazeOverridesVolumeComponent>();
                if (hazeSettingsOverrides != null && hazeSettingsOverrides.IsActive())
                {
                    if (hazeSettingsOverrides.MultipleScatteringIntensity.overrideState)
                    {
                        multipleScatteringIntensity = hazeSettingsOverrides.MultipleScatteringIntensity.value;
                    }

                    if (hazeSettingsOverrides.MultipleScatteringRadius.overrideState)
                    {
                        multipleScatteringRadius = hazeSettingsOverrides.MultipleScatteringRadius.value;
                    }
                }
                
                Shader.SetGlobalFloat(BloomIntensity, multipleScatteringIntensity);
                Shader.SetGlobalFloat(BlurRadius, multipleScatteringRadius);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(srcCamColor, copiedTexture, _froxelFogCompositeMaterial, 0), passName: "Froxel fog composite/Blit");
                renderGraph.AddBlitPass(copiedTexture, srcCamColor, Vector2.one, Vector2.zero, passName:"Froxel fog composite/Copy");
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_froxelFogCompositeMaterial);
            }
        }

        private class MultipleScatteringPass : ScriptableRenderPass
        {
            private readonly Material _ssmsMaterial;
            private float _scatter;
            private float _threshold;
            private int _maxIterations;
            private float2 _froxelFogRange;

            private class PassData
            {
                internal Material BloomMaterial;
                internal TextureHandle ColorTexture;
                internal TextureHandle[] DownsampleBuffers;
                internal TextureHandle[] UpsampleBuffers;
                internal int MipCount;
            }
            
            public MultipleScatteringPass(Shader bloomShader,float2 froxelFogRange, MultipleScatteringData multipleScatteringData)
            {
                _ssmsMaterial = CoreUtils.CreateEngineMaterial(bloomShader);
                _froxelFogRange = froxelFogRange;
                _scatter = multipleScatteringData.scatter;
                _threshold = multipleScatteringData.threshold;
                _maxIterations = multipleScatteringData.maxIterations;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var srcCamColor = resourceData.activeColorTexture;
                var textureDesc = srcCamColor.GetDescriptor(renderGraph);

                textureDesc.depthBufferBits = 0;
                textureDesc.msaaSamples = MSAASamples.None;
                var width = textureDesc.width;
                var height = textureDesc.height;

                textureDesc.width = width;
                textureDesc.height = height;

                var scatter = _scatter;
                var threshold = _threshold;
                var maxIterations = _maxIterations;
                
                var hazeSettingsOverrides = VolumeManager.instance.stack?.GetComponent<HazeOverridesVolumeComponent>();
                if (hazeSettingsOverrides != null && hazeSettingsOverrides.IsActive())
                {
                    if (hazeSettingsOverrides.MultipleScatteringScatter.overrideState)
                    {
                        scatter = hazeSettingsOverrides.MultipleScatteringScatter.value;
                    }

                    if (hazeSettingsOverrides.MultipleScatteringThreshold.overrideState)
                    {
                        threshold = hazeSettingsOverrides.MultipleScatteringThreshold.value;
                    }

                    if (hazeSettingsOverrides.MaxMultipleScatteringIterations.overrideState)
                    {
                        maxIterations = hazeSettingsOverrides.MaxMultipleScatteringIterations.value;
                    }
                }
                
                var maxSize = Mathf.Max(width, height);
                var iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
                var mipCount = Mathf.Clamp(iterations, 1, maxIterations);
                
                var downsampleBuffers = new TextureHandle[mipCount];
                var upsampleBuffers = new TextureHandle[mipCount];

                textureDesc.clearBuffer = false;

                var thresholdValue = Mathf.GammaToLinearSpace(threshold);
                var thresholdKnee = thresholdValue * 0.5f;
                var bloomParameters = new float4(scatter, 65472, thresholdValue, thresholdKnee);
                _ssmsMaterial.SetVector(BloomParams, bloomParameters);
                _ssmsMaterial.SetFloat(SampleScale, 0.5f + math.log2(height) - (int)math.log2(height));

                textureDesc.name = "DownscaledBuffer_0";
                downsampleBuffers[0] = renderGraph.CreateTexture(textureDesc);
                textureDesc.name = "UpscaledBuffer_0";
                upsampleBuffers[0] = renderGraph.CreateTexture(textureDesc);
                
                for (var i = 1; i < mipCount; i++)
                {
                    width = math.max(1, width >> 1);
                    height = math.max(1, height >> 1);
                    textureDesc.width = width;
                    textureDesc.height = height;
                    textureDesc.name = $"DownscaledBuffer_{i}";
                    downsampleBuffers[i] = renderGraph.CreateTexture(textureDesc);
                    textureDesc.name = $"UpscaledBuffer_{i}";
                    upsampleBuffers[i] = renderGraph.CreateTexture(textureDesc);
                }

                using (var builder = renderGraph.AddUnsafePass<PassData>("SSMS Bloom", out var passData))
                {
                    passData.BloomMaterial = _ssmsMaterial;
                    passData.DownsampleBuffers = downsampleBuffers;
                    passData.UpsampleBuffers = upsampleBuffers;
                    passData.MipCount = mipCount;
                    
                    passData.ColorTexture = srcCamColor;
                    
                    builder.UseTexture(passData.ColorTexture);
                    for (var i = 0; i < mipCount; i++)
                    {
                        builder.UseTexture(passData.DownsampleBuffers[i], AccessFlags.ReadWrite);
                        builder.UseTexture(passData.UpsampleBuffers[i], AccessFlags.ReadWrite);
                    }

                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(passData.UpsampleBuffers[0], Shader.PropertyToID("_GLOBAL_BloomTexture"));
                    
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        var unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        
                        var loadAction = RenderBufferLoadAction.DontCare;
                        var storeAction = RenderBufferStoreAction.Store;

                        using (new ProfilingScope(unsafeCmd, new ProfilingSampler("Prefilter")))
                        {
                            Blitter.BlitCameraTexture(unsafeCmd, data.ColorTexture, data.DownsampleBuffers[0], loadAction, storeAction, data.BloomMaterial, 0);
                        }

                        using (new ProfilingScope(unsafeCmd, new ProfilingSampler("Downscale")))
                        {
                            var last = data.DownsampleBuffers[0];
                            
                            for (var i = 1; i < data.MipCount; i++)
                            {
                                var mipDown = data.DownsampleBuffers[i];
                                var mipUp = data.UpsampleBuffers[i];
                                
                                Blitter.BlitCameraTexture(unsafeCmd, last, mipUp, loadAction, storeAction, data.BloomMaterial, 1);
                                Blitter.BlitCameraTexture(unsafeCmd, mipUp, mipDown, loadAction, storeAction, data.BloomMaterial, 2);

                                last = mipDown;
                            }
                        }

                        using (new ProfilingScope(unsafeCmd, new ProfilingSampler("Upscale")))
                        {
                            for (var i = data.MipCount - 2; i >= 0; i--)
                            {
                                var lowMip = (i == data.MipCount - 2) ? data.DownsampleBuffers[i + 1] : data.UpsampleBuffers[i + 1];
                                var highMip = data.DownsampleBuffers[i];
                                var mipUp = data.UpsampleBuffers[i];
                                
                                unsafeCmd.SetGlobalTexture(SourceTexLowMip, lowMip);
                                Blitter.BlitCameraTexture(unsafeCmd, highMip, mipUp, loadAction, storeAction, data.BloomMaterial, 3);
                            }
                        }
                    });
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_ssmsMaterial);
            }
        }
    }
}
