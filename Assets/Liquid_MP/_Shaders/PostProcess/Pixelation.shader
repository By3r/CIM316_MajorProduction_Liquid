Shader "Liquid/PostProcess/Pixelation"
{
    // LCD sub-pixel pixelation with per-channel chromatic aberration.
    //
    // Two rendering layers that blend automatically based on virtual pixel size:
    //   1. Sub-pixel LCD  (screenPixelSize >= 8px) — RGB columns, SDF shapes, full detail
    //   2. Plain pixelated (screenPixelSize < 8px)  — blocky with chroma + grid overlay
    //
    // Chromatic aberration and grid lines work at ALL pixel sizes, not just in
    // the LCD layer, so the effect stays visible at higher pixel percentages.
    //
    // Resolution-independent: _PixelCount = virtual pixels across screen height.
    //
    // Driven by:
    //   _PixelCount    — virtual pixels across screen height (lower = blockier)
    //   _ChromaR/G/B   — per-channel UV offset in virtual-pixel units
    //   _GapSize       — dark gap / grid line width between pixels
    //   _CornerRadius  — sub-pixel corner rounding (LCD layer only)
    //   _Brightness    — compensates for sub-pixel brightness loss (LCD layer only)

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Pixelation"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelCount;
            float2 _ChromaR;
            float2 _ChromaG;
            float2 _ChromaB;
            float _GapSize;
            float _CornerRadius;
            float _Brightness;

            float2 SnapUV(float2 uv, float2 cellCount)
            {
                return (floor(uv * cellCount) + 0.5) / cellCount;
            }

            float RoundedRectSDF(float2 p, float2 halfSize, float r)
            {
                float2 d = abs(p) - halfSize + r;
                return length(max(d, 0.0)) - r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 resolution = _BlitTexture_TexelSize.zw;

                float pixelCount = max(_PixelCount, 4.0);

                float aspect = resolution.x / resolution.y;
                float2 cellCount = floor(float2(pixelCount * aspect, pixelCount));

                // How many screen pixels each virtual pixel covers.
                float screenPixelSize = resolution.y / pixelCount;

                // ---- Blend thresholds ----
                // Below ~1.5 screen pixels per virtual pixel the effect is invisible.
                float pixelBlend = smoothstep(1.5, 3.0, screenPixelSize);

                // LCD sub-pixel columns only when virtual pixels are large enough
                // to avoid rainbow aliasing (3 sub-columns need ~3px each = ~9px total).
                float subPixelBlend = smoothstep(6.0, 12.0, screenPixelSize);

                // Early out: effect is invisible at this pixel count.
                if (pixelBlend < 0.001)
                {
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                }

                // ---- Per-channel snapped UVs (chromatic aberration at ALL sizes) ----
                float2 uvStepR = _ChromaR / cellCount;
                float2 uvStepG = _ChromaG / cellCount;
                float2 uvStepB = _ChromaB / cellCount;

                float2 snappedR = SnapUV(uv + uvStepR, cellCount);
                float2 snappedG = SnapUV(uv + uvStepG, cellCount);
                float2 snappedB = SnapUV(uv + uvStepB, cellCount);

                half3 plainColor = half3(
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedR).r,
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedG).g,
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedB).b
                );

                // ---- Grid overlay (works at ALL sizes) ----
                float2 cellUV = frac(uv * cellCount);

                // Distance from cell edge (0 at edge, 0.5 at center).
                float2 edgeDist = 0.5 - abs(cellUV - 0.5);

                // Normalised gap: fraction of cell consumed by grid lines.
                float gap = _GapSize;
                float gridMask = smoothstep(gap * 0.5, gap * 0.5 + 0.04, edgeDist.x)
                               * smoothstep(gap * 0.35, gap * 0.35 + 0.04, edgeDist.y);

                plainColor *= lerp(1.0, gridMask, step(0.001, gap));

                // ---- Sub-pixel LCD effect (large virtual pixels only) ----
                half3 subPixelColor = plainColor;

                if (subPixelBlend > 0.001)
                {
                    // Sub-pixel column: R=0, G=1, B=2
                    float columnPos = cellUV.x * 3.0;
                    int col = clamp((int)floor(columnPos), 0, 2);
                    float subCellX = frac(columnPos);

                    // Sample per-channel with chroma offsets (already computed above).
                    half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedR).r;
                    half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedG).g;
                    half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedB).b;

                    // Each column shows only its channel.
                    half3 channelMask = half3(col == 0, col == 1, col == 2);
                    half3 lcdColor = half3(r, g, b) * channelMask;

                    // Sub-pixel rounded rectangle shape + gaps.
                    float2 p = float2(subCellX - 0.5, cellUV.y - 0.5);
                    float2 halfSize = float2(0.5 - _GapSize, 0.5 - _GapSize * 0.7);
                    float cornerR = min(halfSize.x, halfSize.y) * _CornerRadius;

                    float dist = RoundedRectSDF(p, halfSize, cornerR);
                    float mask = smoothstep(0.02, -0.02, dist);

                    lcdColor *= mask * _Brightness;

                    subPixelColor = lcdColor;
                }

                // ---- Composite: sub-pixel LCD ↔ plain (with chroma + grid) ↔ original ----
                half3 pixelated = lerp(plainColor, subPixelColor, subPixelBlend);
                half4 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                half3 finalColor = lerp(original.rgb, pixelated, pixelBlend);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
