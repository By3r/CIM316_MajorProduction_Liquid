Shader "Liquid/PostProcess/Pixelation"
{
    // CRT-style pixelation with per-channel chromatic aberration.
    //
    // Each virtual pixel is a bright rounded dot on a dark background,
    // matching the look of real CRT / LED displays. The dark area between
    // dots is the natural gap — not a grid drawn on top of blocks.
    //
    // Two detail tiers blend based on virtual pixel size:
    //   1. Dot grid       (all sizes)         — rounded pixel dots + chroma
    //   2. Sub-pixel LCD  (screenPixelSize >= 8px) — RGB column split inside each dot
    //
    // Resolution-independent: _PixelCount = virtual pixels across screen height.
    //
    // Driven by:
    //   _PixelCount    — virtual pixels across screen height (lower = blockier)
    //   _ChromaR/G/B   — per-channel UV offset in virtual-pixel units
    //   _GapSize       — dark space between pixel dots (0 = no gap, 0.2 = wide)
    //   _CornerRadius  — dot corner rounding (0 = sharp rect, 1 = circular)
    //   _Brightness    — compensates for brightness loss from dot masking

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

            // Snap a screen-pixel coordinate to the center of its virtual pixel,
            // then convert back to UV. Guarantees every virtual pixel is exactly
            // `virtualSize` screen pixels — no size variation.
            float2 SnapToVirtualPixel(float2 pixelCoord, float virtualSize, float2 resolution)
            {
                float2 snapped = floor(pixelCoord / virtualSize) * virtualSize + virtualSize * 0.5;
                return snapped / resolution;
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

                // Integer virtual-pixel size ensures every pixel is uniform and square.
                float virtualSize = max(round(resolution.y / pixelCount), 1.0);

                // How many screen pixels each virtual pixel covers (integer).
                float screenPixelSize = virtualSize;

                // ---- Blend thresholds ----
                float pixelBlend = smoothstep(1.5, 3.0, screenPixelSize);
                float subPixelBlend = smoothstep(6.0, 12.0, screenPixelSize);

                // Early out: effect is invisible at this pixel count.
                if (pixelBlend < 0.001)
                {
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                }

                // ---- Screen-pixel-space snapping with per-channel chroma offsets ----
                float2 pixelCoord = input.positionCS.xy;

                float2 chromaOffsetR = _ChromaR * virtualSize;
                float2 chromaOffsetG = _ChromaG * virtualSize;
                float2 chromaOffsetB = _ChromaB * virtualSize;

                float2 snappedR = SnapToVirtualPixel(pixelCoord + chromaOffsetR, virtualSize, resolution);
                float2 snappedG = SnapToVirtualPixel(pixelCoord + chromaOffsetG, virtualSize, resolution);
                float2 snappedB = SnapToVirtualPixel(pixelCoord + chromaOffsetB, virtualSize, resolution);

                half3 color = half3(
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedR).r,
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedG).g,
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedB).b
                );

                // ---- Pixel dot shape (CRT-style) ----
                // Position within virtual pixel cell: center = (0,0), edges = ±0.5
                float2 cellUV = frac(pixelCoord / virtualSize);
                float2 cellCenter = cellUV - 0.5;

                float gap = _GapSize;

                if (gap > 0.001)
                {
                    // Dot fills the cell minus the gap on each side.
                    float2 dotHalf = float2(0.5 - gap, 0.5 - gap * 0.7);
                    float cornerR = min(dotHalf.x, dotHalf.y) * _CornerRadius;
                    float sdf = RoundedRectSDF(cellCenter, max(dotHalf, 0.01), cornerR);

                    // Soft falloff over ~1.5 screen pixels suppresses moiré by
                    // avoiding sharp periodic brightness transitions.
                    float softness = 1.5 / virtualSize;
                    float dotMask = smoothstep(softness, -softness, sdf);

                    // Per-dot noise breaks perfect periodicity that causes
                    // display-level moiré (dot grid vs physical pixel grid).
                    // ±3% brightness variation — imperceptible but kills interference.
                    float2 cellId = floor(pixelCoord / virtualSize);
                    float noise = frac(sin(dot(cellId, float2(12.9898, 78.233))) * 43758.5453);
                    dotMask *= lerp(0.97, 1.0, noise);

                    color *= dotMask;
                }

                // ---- Sub-pixel LCD effect (large virtual pixels only) ----
                if (subPixelBlend > 0.001)
                {
                    float columnPos = cellUV.x * 3.0;
                    int col = clamp((int)floor(columnPos), 0, 2);
                    float subCellX = frac(columnPos);

                    half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedR).r;
                    half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedG).g;
                    half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, snappedB).b;

                    half3 channelMask = half3(col == 0, col == 1, col == 2);
                    half3 lcdColor = half3(r, g, b) * channelMask;

                    float2 p = float2(subCellX - 0.5, cellUV.y - 0.5);
                    float2 halfSize = float2(0.5 - gap, 0.5 - gap * 0.7);
                    float cornerR = min(halfSize.x, halfSize.y) * _CornerRadius;

                    float dist = RoundedRectSDF(p, max(halfSize, 0.01), cornerR);
                    float softness = 1.5 / virtualSize;
                    float mask = smoothstep(softness, -softness, dist);

                    lcdColor *= mask * _Brightness;

                    color = lerp(color, lcdColor, subPixelBlend);
                }

                // ---- Composite ----
                half4 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                half3 finalColor = lerp(original.rgb, color, pixelBlend);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
