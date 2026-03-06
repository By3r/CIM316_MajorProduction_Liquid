Shader "Liquid/VisorGlass"
{
    Properties
    {
        [Header(Glass Tint)]
        _TintColor      ("Tint Color", Color)                       = (0.06, 0.09, 0.12, 1)
        _CenterAlpha    ("Center Alpha (clear)", Range(0, 0.15))    = 0.03
        _EdgeAlpha      ("Edge Alpha (rim)", Range(0, 0.4))         = 0.15

        [Header(Scratches)]
        [NoScaleOffset]
        _BumpMap        ("Scratches Normal Map", 2D)                = "bump" {}
        _BumpTiling     ("Tiling", Vector)                          = (1, 1, 0, 0)
        _BumpScale      ("Scratch Intensity", Range(0, 2))          = 0.5

        [Header(Specular)]
        _SpecColor      ("Specular Color", Color)                   = (1, 1, 1, 1)
        _SpecPower      ("Specular Sharpness", Range(4, 512))       = 128
        _SpecStrength   ("Specular Strength", Range(0, 1))          = 0.35

        [Header(Fresnel)]
        _FresnelPower   ("Fresnel Power", Range(0.5, 8))            = 3.0

        [Header(Dirt and Vignette)]
        [NoScaleOffset]
        _DirtMap        ("Dirt / Smudge Map (R)", 2D)               = "black" {}
        _DirtStrength   ("Dirt Opacity", Range(0, 0.3))             = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VisorGlass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // --- Textures ---
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DirtMap);
            SAMPLER(sampler_DirtMap);

            // --- Material properties ---
            CBUFFER_START(UnityPerMaterial)
                half4  _TintColor;
                half   _CenterAlpha;
                half   _EdgeAlpha;
                float4 _BumpTiling;
                half   _BumpScale;
                half4  _SpecColor;
                half   _SpecPower;
                half   _SpecStrength;
                half   _FresnelPower;
                half   _DirtStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD2;
                float3 bitangentWS  : TEXCOORD3;
                float3 viewDirWS    : TEXCOORD4;
                float3 positionWS   : TEXCOORD5;
                float  fogFactor    : TEXCOORD6;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.uv          = IN.uv;
                OUT.normalWS    = norInputs.normalWS;
                OUT.tangentWS   = norInputs.tangentWS;
                OUT.bitangentWS = norInputs.bitangentWS;
                OUT.viewDirWS   = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS    = normalize(IN.normalWS);
                float3 tangentWS   = normalize(IN.tangentWS);
                float3 bitangentWS = normalize(IN.bitangentWS);
                float3 viewDir     = normalize(IN.viewDirWS);

                // ---- Fresnel: edges more opaque, center clear ----
                half NdotV   = saturate(dot(normalWS, viewDir));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);
                half alpha   = lerp(_CenterAlpha, _EdgeAlpha, fresnel);

                // ---- Scratches normal map ----
                float2 scratchUV = IN.uv * _BumpTiling.xy + _BumpTiling.zw;
                float3 normalTS  = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, scratchUV), _BumpScale);

                // Swap X/Y to correct tangent space orientation on cube mesh.
                // Cube front face tangent is rotated 90° vs expected, causing
                // perpendicular specular streaks. This realigns scratches.
                normalTS.xy = normalTS.yx;

                float3x3 TBN = float3x3(tangentWS, bitangentWS, normalWS);
                float3 perturbedNormal = normalize(mul(normalTS, TBN));

                // Scratch mask: how much this pixel deviates from flat (0,0,1).
                // Smooth glass = 0, scratched areas = non-zero.
                // Specular ONLY appears where scratches exist.
                half scratchMask = saturate(length(normalTS.xy) * 2.0);

                // ---- Specular from main light hitting scratches ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight    = GetMainLight(shadowCoord);
                float3 halfDir     = normalize(mainLight.direction + viewDir);
                half spec          = pow(saturate(dot(perturbedNormal, halfDir)), _SpecPower);
                spec              *= _SpecStrength * scratchMask
                                   * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 specular     = spec * _SpecColor.rgb * mainLight.color;

                // ---- Additional lights specular ----
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light addLight   = GetAdditionalLight(i, IN.positionWS);
                    float3 addHalf   = normalize(addLight.direction + viewDir);
                    half addSpec     = pow(saturate(dot(perturbedNormal, addHalf)), _SpecPower);
                    addSpec         *= _SpecStrength * scratchMask
                                    * addLight.distanceAttenuation * addLight.shadowAttenuation;
                    specular        += addSpec * _SpecColor.rgb * addLight.color;
                }
                #endif

                // ---- Dirt / smudge overlay ----
                half dirt = SAMPLE_TEXTURE2D(_DirtMap, sampler_DirtMap, IN.uv).r;
                alpha    += dirt * _DirtStrength;

                // ---- Specular adds to alpha (scratches glow when lit) ----
                alpha = saturate(alpha + spec * 0.4);

                // ---- Combine ----
                half3 color = _TintColor.rgb + specular;
                color       = MixFog(color, IN.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
