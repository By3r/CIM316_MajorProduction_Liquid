using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Liquid.Rendering
{
    /// <summary>
    /// URP Renderer Feature that applies AMD FidelityFX Contrast Adaptive Sharpening (CAS)
    /// as a fullscreen post-processing pass. Intensity is driven by the
    /// <see cref="CASSharpeningVolume"/> component on any active Volume.
    ///
    /// Setup:
    /// 1. Add this feature to your URP Renderer (URP Asset_Renderer).
    /// 2. Assign the "Liquid/PostProcess/CASSharpening" shader in the inspector.
    /// 3. Add a CAS Sharpening override to your Global Volume Profile.
    /// 4. Set intensity > 0.
    /// </summary>
    public class CASSharpeningFeature : ScriptableRendererFeature
    {
        [Header("Shader Reference")]
        [Tooltip("Assign the CASSharpening shader. If left empty, looks up 'Liquid/PostProcess/CASSharpening' by name.")]
        [SerializeField] private Shader _shader;

        private Material _material;
        private CASSharpeningPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find("Liquid/PostProcess/CASSharpening");

            if (_shader == null)
            {
                Debug.LogWarning("[CASSharpeningFeature] Shader 'Liquid/PostProcess/CASSharpening' not found. " +
                                 "Assign it manually in the Renderer Feature inspector.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new CASSharpeningPass(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;

            // Skip if no active CAS volume override
            var stack = VolumeManager.instance.stack;
            var cas = stack.GetComponent<CASSharpeningVolume>();
            if (cas == null || !cas.IsActive()) return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        /// <summary>
        /// Render pass that performs the CAS fullscreen blit using the Render Graph API.
        /// </summary>
        private class CASSharpeningPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

            public CASSharpeningPass(Material material)
            {
                _material = material;

                // Run after all other post-processing (bloom, tonemapping, etc.)
                // so CAS sharpens the final composited image.
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var stack = VolumeManager.instance.stack;
                var cas = stack.GetComponent<CASSharpeningVolume>();
                if (cas == null || !cas.IsActive()) return;

                var resourceData = frameData.Get<UniversalResourceData>();

                // Can't process when rendering directly to back buffer.
                if (resourceData.isActiveTargetBackBuffer) return;

                _material.SetFloat(IntensityId, cas.intensity.value);

                TextureHandle source = resourceData.activeColorTexture;

                // Create temp texture for CAS output
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_CASTemp";
                desc.clearBuffer = false;
                TextureHandle temp = renderGraph.CreateTexture(desc);

                // Pass 1: Apply CAS shader — source → temp
                RenderGraphUtils.BlitMaterialParameters blitParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(blitParams, "CAS Sharpening");

                // Pass 2: Copy result back — temp → source
                renderGraph.AddCopyPass(temp, source);
            }
        }
    }
}
