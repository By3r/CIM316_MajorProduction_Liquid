Shader "Liquid/PostProcess/WakeUpBlur"
{
    // Full screen post process: black tint + Gaussian blur for wake up effect.
    // _BlackAmount (0..1) lerps toward black.
    // _BlurAmount  (0..1) controls blur radius.

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WakeUp Blur"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlackAmount;
            float _BlurAmount;
            float _BlurRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _BlurAmount * _BlurRadius;

                // 9 tap Gaussian blur (center + 8 surrounding samples)
                // Weights: center 4/16, cardinal 2/16 each, diagonal 1/16 each
                half3 color = half3(0, 0, 0);

                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.25;

                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( texel.x, 0)).rgb * 0.125;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-texel.x, 0)).rgb * 0.125;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0,  texel.y)).rgb * 0.125;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0, -texel.y)).rgb * 0.125;

                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( texel.x,  texel.y)).rgb * 0.0625;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-texel.x,  texel.y)).rgb * 0.0625;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( texel.x, -texel.y)).rgb * 0.0625;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-texel.x, -texel.y)).rgb * 0.0625;

                // Apply black tint
                color = lerp(color, half3(0, 0, 0), _BlackAmount);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
