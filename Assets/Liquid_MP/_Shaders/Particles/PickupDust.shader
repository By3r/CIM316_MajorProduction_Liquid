Shader "Liquid/Particles/PickupDust"
{
    // Particle shader for pickup dust motes.
    // Mostly invisible at rest, shines brightly when hit by the player's flashlight.
    // Flashlight position/direction/angle are set as global shader properties
    // by FlashlightShaderDriver.

    Properties
    {
        _MainTex          ("Particle Texture",    2D)              = "white" {}
        _Color            ("Base Color",          Color)           = (0.5, 0.8, 1.0, 0.15)
        _BaseGlow         ("Base Glow",           Range(0, 5))     = 0.3
        _FlashlightGlow   ("Flashlight Glow",     Range(0, 50))    = 8.0
        _FlashlightFalloff("Flashlight Falloff",  Range(0.5, 5))   = 2.0
        _EmissionBoost    ("Emission Boost (HDR)", Range(0.1, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "PickupDust"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float  _BaseGlow;
                float  _FlashlightGlow;
                float  _FlashlightFalloff;
                float  _EmissionBoost;
            CBUFFER_END

            // Global properties set by FlashlightShaderDriver
            float3 _FlashlightPos;
            float3 _FlashlightDir;
            float  _FlashlightAngle;    // cos of half angle
            float  _FlashlightRange;
            float  _FlashlightIntensity;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.color      = IN.color;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Direction from flashlight to particle
                float3 toParticle = IN.positionWS - _FlashlightPos;
                float dist = length(toParticle);
                float3 dirToParticle = toParticle / max(dist, 0.001);

                // Cone check: dot product with flashlight forward
                float cone = dot(dirToParticle, _FlashlightDir);

                // Smooth cone falloff (1 at center, 0 at edge)
                float coneAtten = saturate((cone - _FlashlightAngle) / (1.0 - _FlashlightAngle));
                coneAtten = pow(coneAtten, _FlashlightFalloff);

                // Distance attenuation (inverse square, clamped to range)
                float distAtten = saturate(1.0 - dist / max(_FlashlightRange, 0.1));
                distAtten *= distAtten;

                // Final flashlight contribution (no intensity multiplier, glow value controls directly)
                float flashlight = coneAtten * distAtten;

                // Combine base glow with flashlight boost
                float glow = _BaseGlow + flashlight * _FlashlightGlow;

                // HDR output: values above 1.0 will trigger bloom post processing
                float3 col = _Color.rgb * IN.color.rgb * texCol.rgb * glow;
                float alpha = _Color.a * IN.color.a * texCol.a;

                return float4(col * _EmissionBoost, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
