Shader "Liquid/CRTScreen"
{
    // Material shader for in-world CRT terminal screens.
    // Adapted from Liquid/PostProcess/Pixelation — same visual style,
    // but reads from a Render Texture assigned as _MainTex instead of
    // the fullscreen blit texture. Unlit so the screen glows naturally.
    //
    // Assign the terminal's Render Texture to the Base Map slot.
    // Tweak _PixelCount to control blockiness on the monitor.

    Properties
    {
        _MainTex       ("Screen Texture", 2D)              = "black" {}
        _PixelCount    ("Pixel Count (height)", Float)      = 200
        _ChromaR       ("Chroma Offset R", Vector)          = (0.4, 0, 0, 0)
        _ChromaG       ("Chroma Offset G", Vector)          = (0, 0, 0, 0)
        _ChromaB       ("Chroma Offset B", Vector)          = (-0.4, 0, 0, 0)
        _GapSize       ("Gap Size", Range(0, 0.5))          = 0.08
        _CornerRadius  ("Corner Radius", Range(0, 1))       = 0.7
        _Brightness    ("Brightness", Float)                = 1.4
        _Emission      ("Emission Intensity", Float)        = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "CRTScreen"

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // (1/w, 1/h, w, h)

            float  _PixelCount;
            float2 _ChromaR;
            float2 _ChromaG;
            float2 _ChromaB;
            float  _GapSize;
            float  _CornerRadius;
            float  _Brightness;
            float  _Emission;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Snap a texel coordinate to the center of its virtual pixel,
            // then convert back to UV.
            float2 SnapToVirtualPixel(float2 texelCoord, float virtualSize, float2 resolution)
            {
                float2 snapped = floor(texelCoord / virtualSize) * virtualSize + virtualSize * 0.5;
                return snapped / resolution;
            }

            float RoundedRectSDF(float2 p, float2 halfSize, float r)
            {
                float2 d = abs(p) - halfSize + r;
                return length(max(d, 0.0)) - r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 resolution = _MainTex_TexelSize.zw; // RT width, height

                float pixelCount = max(_PixelCount, 4.0);
                float virtualSize = max(round(resolution.y / pixelCount), 1.0);
                float screenPixelSize = virtualSize;

                // ---- Blend thresholds ----
                float pixelBlend  = smoothstep(1.5, 3.0, screenPixelSize);
                float subPixelBlend = smoothstep(6.0, 12.0, screenPixelSize);

                // If virtual pixels are too small, just show the raw texture.
                if (pixelBlend < 0.001)
                {
                    half4 raw = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                    return half4(raw.rgb * _Emission, 1.0);
                }

                // ---- Texel-space coordinates (UV * resolution) ----
                float2 texelCoord = uv * resolution;

                float2 chromaOffsetR = _ChromaR * virtualSize;
                float2 chromaOffsetG = _ChromaG * virtualSize;
                float2 chromaOffsetB = _ChromaB * virtualSize;

                float2 snappedR = SnapToVirtualPixel(texelCoord + chromaOffsetR, virtualSize, resolution);
                float2 snappedG = SnapToVirtualPixel(texelCoord + chromaOffsetG, virtualSize, resolution);
                float2 snappedB = SnapToVirtualPixel(texelCoord + chromaOffsetB, virtualSize, resolution);

                half3 color = half3(
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedR).r,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedG).g,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedB).b
                );

                // ---- Pixel dot shape (CRT-style) ----
                float2 cellUV = frac(texelCoord / virtualSize);
                float2 cellCenter = cellUV - 0.5;

                float gap = _GapSize;

                if (gap > 0.001)
                {
                    float2 dotHalf = float2(0.5 - gap, 0.5 - gap * 0.7);
                    float cornerR  = min(dotHalf.x, dotHalf.y) * _CornerRadius;
                    float sdf      = RoundedRectSDF(cellCenter, max(dotHalf, 0.01), cornerR);

                    float softness = 1.5 / virtualSize;
                    float dotMask  = smoothstep(softness, -softness, sdf);

                    // Per-dot noise breaks moiré.
                    float2 cellId = floor(texelCoord / virtualSize);
                    float noise   = frac(sin(dot(cellId, float2(12.9898, 78.233))) * 43758.5453);
                    dotMask *= lerp(0.97, 1.0, noise);

                    color *= dotMask;
                }

                // ---- Sub-pixel LCD effect (large virtual pixels only) ----
                if (subPixelBlend > 0.001)
                {
                    float columnPos = cellUV.x * 3.0;
                    int col         = clamp((int)floor(columnPos), 0, 2);
                    float subCellX  = frac(columnPos);

                    half r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedR).r;
                    half g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedG).g;
                    half b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedB).b;

                    half3 channelMask = half3(col == 0, col == 1, col == 2);
                    half3 lcdColor    = half3(r, g, b) * channelMask;

                    float2 p        = float2(subCellX - 0.5, cellUV.y - 0.5);
                    float2 halfSize = float2(0.5 - gap, 0.5 - gap * 0.7);
                    float cornerR   = min(halfSize.x, halfSize.y) * _CornerRadius;

                    float dist     = RoundedRectSDF(p, max(halfSize, 0.01), cornerR);
                    float softness = 1.5 / virtualSize;
                    float mask     = smoothstep(softness, -softness, dist);

                    lcdColor *= mask * _Brightness;
                    color = lerp(color, lcdColor, subPixelBlend);
                }

                // ---- Final output ----
                // Brightness compensates for light lost in dot gaps.
                // Emission makes the screen glow without scene lighting.
                color *= _Brightness * _Emission;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
