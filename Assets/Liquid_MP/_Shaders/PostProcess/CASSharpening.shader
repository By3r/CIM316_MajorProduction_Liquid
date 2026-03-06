Shader "Liquid/PostProcess/CASSharpening"
{
    // AMD FidelityFX Contrast Adaptive Sharpening (CAS) — simplified single-pass.
    //
    // Samples the center pixel + 4 cardinal neighbours, computes local contrast,
    // and applies stronger sharpening in low-contrast areas while pulling back in
    // high-contrast areas to avoid halos / ringing.
    //
    // Driven by _Intensity (0 = bypass, 1 = maximum sharpening).

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CAS Sharpening"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set by CASSharpeningFeature via material property
            float _Intensity;

            // _BlitTexture, sampler_LinearClamp, and _BlitTexture_TexelSize
            // are all declared by Blit.hlsl — do not redeclare.

            /// <summary>
            /// Core CAS filter. Returns the sharpened colour for the given UV.
            ///
            /// Algorithm (per AMD FidelityFX reference):
            ///   1. Sample centre + 4 cardinal neighbours.
            ///   2. Per-channel min/max of the 5-tap neighbourhood.
            ///   3. Amplitude = sqrt(saturate(min(mn, 2-mx) / mx))
            ///      — high contrast → low amplitude → less sharpening.
            ///   4. Weight = amplitude * peak, where peak = -1 / lerp(8, 5, sharpness)
            ///      giving range [-0.125, -0.2].
            ///   5. Weighted sum: (centre + neighbours * w) / (1 + 4w)
            /// </summary>
            half3 CASFilter(float2 uv, float2 texel, float sharpness)
            {
                // 5-tap cross pattern
                half3 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                half3 u = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0.0,  texel.y)).rgb;
                half3 d = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -texel.y)).rgb;
                half3 l = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-texel.x, 0.0)).rgb;
                half3 r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( texel.x, 0.0)).rgb;

                // Per-channel neighbourhood extremes
                half3 mn = min(c, min(min(u, d), min(l, r)));
                half3 mx = max(c, max(max(u, d), max(l, r)));

                // Contrast-adaptive amplitude (high contrast → low amplitude)
                half3 amp = saturate(min(mn, 2.0 - mx) * rcp(mx));
                amp = sqrt(amp);

                // Negative lobe weight — lerp(8, 2.5, sharpness) maps [0,2] → [8, 2.5]
                // peak ranges from -0.125 (gentle) to -0.4 (very aggressive)
                float peak = -rcp(lerp(8.0, 2.5, saturate(sharpness * 0.5)));
                half3 w = amp * peak;

                // Normalised weighted sum (centre + 4 neighbours * w)
                half3 result = (c + (u + d + l + r) * w) * rcp(1.0 + 4.0 * w);

                return saturate(result);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                half3 sharpened = CASFilter(uv, _BlitTexture_TexelSize.xy, _Intensity);

                // Blend: saturate so values > 1 only affect the CAS algorithm, not overshoot the blend
                half3 finalColor = lerp(original, sharpened, saturate(_Intensity));

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
