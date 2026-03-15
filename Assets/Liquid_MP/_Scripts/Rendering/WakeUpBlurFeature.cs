using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Liquid.Rendering
{
    /// <summary>
    /// URP Renderer Feature that applies a full screen black tint + Gaussian blur
    /// for the tutorial wake up effect. Renders after post processing so UI (subtitles)
    /// remains visible on top.
    ///
    /// All parameters are driven via static properties from <see cref="_Scripts.Tutorial.TutorialWakeUp"/>.
    /// Automatically skipped when <see cref="IsActive"/> is false (zero overhead).
    ///
    /// Setup:
    /// 1. Add this feature to your URP Renderer (same one with CAS Sharpening, Pixelation).
    /// 2. Assign the "Liquid/PostProcess/WakeUpBlur" shader or leave empty for auto find.
    /// </summary>
    public class WakeUpBlurFeature : ScriptableRendererFeature
    {
        #region Static Properties (driven by TutorialWakeUp)

        /// <summary>Whether the effect is currently active.</summary>
        public static bool IsActive { get; set; }

        /// <summary>Black tint amount (0 = no tint, 1 = fully black).</summary>
        public static float BlackAmount { get; set; }

        /// <summary>Blur strength (0 = sharp, 1 = maximum blur).</summary>
        public static float BlurAmount { get; set; }

        /// <summary>Maximum blur radius in texels. Higher = more spread.</summary>
        public static float BlurRadius { get; set; } = 8f;

        #endregion

        [Header("Shader Reference")]
        [Tooltip("Assign the WakeUpBlur shader. If left empty, looks up 'Liquid/PostProcess/WakeUpBlur' by name.")]
        [SerializeField] private Shader _shader;

        private Material _material;
        private WakeUpBlurPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find("Liquid/PostProcess/WakeUpBlur");

            if (_shader == null)
            {
                Debug.LogWarning("[WakeUpBlurFeature] Shader 'Liquid/PostProcess/WakeUpBlur' not found. " +
                                 "Assign it manually in the Renderer Feature inspector.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new WakeUpBlurPass(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (!IsActive) return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        /// <summary>
        /// Render pass that performs the black tint + blur blit via Render Graph.
        /// </summary>
        private class WakeUpBlurPass : ScriptableRenderPass
        {
            private readonly Material _material;

            private static readonly int BlackAmountId = Shader.PropertyToID("_BlackAmount");
            private static readonly int BlurAmountId = Shader.PropertyToID("_BlurAmount");
            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");

            public WakeUpBlurPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!IsActive) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                _material.SetFloat(BlackAmountId, BlackAmount);
                _material.SetFloat(BlurAmountId, BlurAmount);
                _material.SetFloat(BlurRadiusId, BlurRadius);

                TextureHandle source = resourceData.activeColorTexture;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_WakeUpBlurTemp";
                desc.clearBuffer = false;
                TextureHandle temp = renderGraph.CreateTexture(desc);

                RenderGraphUtils.BlitMaterialParameters blitParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(blitParams, "WakeUp Blur");

                renderGraph.AddCopyPass(temp, source);
            }
        }
    }
}
