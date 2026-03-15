Shader "Liquid/PickupGlow"
{
    // Soft ambient emission overlay for pickupable items.
    // Adds a very faint uniform inner glow across the surface,
    // with a slow breathing pulse. Applied as a second material pass.

    Properties
    {
        _GlowColor ("Glow Color", Color) = (0.6, 0.85, 1.0, 1.0)
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 0.8
        _PulseMin ("Pulse Min Intensity", Range(0, 0.1)) = 0.008
        _PulseMax ("Pulse Max Intensity", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PickupGlow"

            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            half4 _GlowColor;
            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);

                // Soft AO weighting: surfaces facing the camera glow slightly more
                float ndotv = saturate(dot(normal, viewDir));
                float aoFactor = lerp(0.5, 1.0, ndotv);

                // Slow sine pulse
                float pulse = lerp(_PulseMin, _PulseMax, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);

                float glow = pulse * aoFactor;

                return half4(_GlowColor.rgb * glow, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
