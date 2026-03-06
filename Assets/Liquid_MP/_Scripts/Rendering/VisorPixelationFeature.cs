using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Liquid.Rendering
{
    /// <summary>
    /// URP Renderer Feature that applies LCD sub-pixel pixelation with per-channel
    /// chromatic aberration. All visual settings are exposed as static properties
    /// driven by <see cref="VisorPixelationAnimator"/> at runtime.
    ///
    /// The pass is automatically skipped when <c>PixelCount</c> is at or above
    /// the screen resolution (zero overhead when inactive).
    ///
    /// Setup:
    /// 1. Add this feature to your URP Renderer (same one CAS Sharpening is on).
    ///    Place it AFTER CAS Sharpening in the feature list.
    /// 2. Assign the "Liquid/PostProcess/Pixelation" shader (or leave empty for auto-find).
    /// 3. Add a <see cref="VisorPixelationAnimator"/> component to drive everything.
    /// </summary>
    public class VisorPixelationFeature : ScriptableRendererFeature
    {
        #region Static Properties (driven by VisorPixelationAnimator)

        /// <summary>
        /// Number of virtual pixels across the screen height.
        /// Resolution-independent: 60 = very blocky, 200 = subtle, 1080+ = off.
        /// </summary>
        public static float PixelCount { get; set; } = 10000f;

        /// <summary>Red channel UV offset in virtual-pixel units.</summary>
        public static Vector2 ChromaR { get; set; }

        /// <summary>Green channel UV offset in virtual-pixel units.</summary>
        public static Vector2 ChromaG { get; set; }

        /// <summary>Blue channel UV offset in virtual-pixel units.</summary>
        public static Vector2 ChromaB { get; set; }

        /// <summary>Dark gap width between sub-pixels (0 = none).</summary>
        public static float GapSize { get; set; } = 0.1f;

        /// <summary>Sub-pixel corner rounding (0 = sharp rectangles).</summary>
        public static float CornerRadius { get; set; } = 0.15f;

        /// <summary>Brightness multiplier (~3.0 to compensate sub-pixel filtering).</summary>
        public static float Brightness { get; set; } = 3f;

        /// <summary>Whether the effect is currently active.</summary>
        public static bool IsActive { get; set; }

        #endregion

        [Header("Shader Reference")]
        [Tooltip("Assign the Pixelation shader. If left empty, looks up 'Liquid/PostProcess/Pixelation' by name.")]
        [SerializeField] private Shader _shader;

        private Material _material;
        private PixelationPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find("Liquid/PostProcess/Pixelation");

            if (_shader == null)
            {
                Debug.LogWarning("[VisorPixelationFeature] Shader 'Liquid/PostProcess/Pixelation' not found. " +
                                 "Assign it manually in the Renderer Feature inspector.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new PixelationPass(_material);
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
        /// Render pass that performs the LCD sub-pixel pixelation blit via Render Graph.
        /// </summary>
        private class PixelationPass : ScriptableRenderPass
        {
            private readonly Material _material;

            private static readonly int PixelCountId = Shader.PropertyToID("_PixelCount");
            private static readonly int ChromaRId = Shader.PropertyToID("_ChromaR");
            private static readonly int ChromaGId = Shader.PropertyToID("_ChromaG");
            private static readonly int ChromaBId = Shader.PropertyToID("_ChromaB");
            private static readonly int GapSizeId = Shader.PropertyToID("_GapSize");
            private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
            private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

            public PixelationPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!IsActive) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                _material.SetFloat(PixelCountId, PixelCount);
                _material.SetVector(ChromaRId, ChromaR);
                _material.SetVector(ChromaGId, ChromaG);
                _material.SetVector(ChromaBId, ChromaB);
                _material.SetFloat(GapSizeId, GapSize);
                _material.SetFloat(CornerRadiusId, CornerRadius);
                _material.SetFloat(BrightnessId, Brightness);

                TextureHandle source = resourceData.activeColorTexture;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PixelationTemp";
                desc.clearBuffer = false;
                TextureHandle temp = renderGraph.CreateTexture(desc);

                RenderGraphUtils.BlitMaterialParameters blitParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(blitParams, "Visor Pixelation");

                renderGraph.AddCopyPass(temp, source);
            }
        }
    }
}
