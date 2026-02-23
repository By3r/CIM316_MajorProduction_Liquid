Shader "Liquid/BulletTrailLit"
{
    Properties
    {
        _BaseMap        ("Trail Texture", 2D)                       = "white" {}
        _BaseColor      ("Tint", Color)                             = (1, 1, 1, 1)
        _EmissionColor  ("Emission Color", Color)                   = (0, 0, 0, 0)
        _EmissionStrength ("Emission Strength", Range(0, 10))       = 1.0
        _FadeDuration   ("Fade Duration (seconds)", Range(0.01, 5)) = 0.3
        _FadeExponent   ("Fade Curve", Range(0.1, 5))               = 1.0

        // Set per-instance at runtime via MaterialPropertyBlock — do not edit manually
        [HideInInspector] _SpawnTime("__spawnTime", Float)          = -999

        // URP surface settings
        [HideInInspector] _Surface("__surface", Float)              = 1.0
        [HideInInspector] _Blend("__blend", Float)                  = 0.0
        [HideInInspector] _SrcBlend("__src", Float)                 = 5.0
        [HideInInspector] _DstBlend("__dst", Float)                 = 10.0
        [HideInInspector] _ZWrite("__zw", Float)                    = 0.0
        [HideInInspector] _Cull("__cull", Float)                    = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                half   _EmissionStrength;
                half   _FadeDuration;
                half   _FadeExponent;
                float  _SpawnTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 color       : COLOR;
                float  fogFactor   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = norInputs.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- Per-instance time-based fade ---
                float age = _Time.y - _SpawnTime;
                half timeFade = 1.0 - saturate(age / max(_FadeDuration, 0.001));
                timeFade = pow(timeFade, _FadeExponent);

                clip(timeFade - 0.001);

                // Sample trail texture
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Combine: texture * tint * vertex color
                half4 baseColor = texColor * _BaseColor * IN.color;

                // Apply time fade to alpha
                baseColor.a *= timeFade;

                clip(baseColor.a - 0.001);

                // --- Lighting ---
                float3 normalWS = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half wrap = NdotL * 0.5 + 0.5;
                half3 diffuse = baseColor.rgb * lighting * wrap;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction)) * 0.5 + 0.5;
                    diffuse += baseColor.rgb * addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation * addNdotL;
                }
                #endif

                half3 ambient = SampleSH(normalWS) * baseColor.rgb;

                // Emission also fades with the trail
                half3 emission = _EmissionColor.rgb * _EmissionStrength * timeFade;

                half3 finalColor = diffuse + ambient + emission;
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                half   _EmissionStrength;
                half   _FadeDuration;
                half   _FadeExponent;
                float  _SpawnTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                float age = _Time.y - _SpawnTime;
                half timeFade = 1.0 - saturate(age / max(_FadeDuration, 0.001));
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a * IN.color.a * timeFade;
                clip(alpha - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
