Shader "Custom/HolographicItem"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Hologram Tint", Color) = (0.3, 0.8, 1.0, 0.7)
        _EmissionColor ("Emission Color", Color) = (0.3, 0.8, 1.0, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 1.5

        [Header(Texture Blending)]
        _TextureBlend ("Texture Visibility", Range(0, 1)) = 0.6

        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _ScanlineSpeed ("Scanline Scroll Speed", Range(0, 10)) = 2.0
        _ScanlineCount ("Scanline Count", Range(10, 500)) = 100
        _ScanlineThickness ("Scanline Thickness", Range(0, 1)) = 0.5

        [Header(Flicker)]
        _FlickerSpeed ("Flicker Speed", Range(0, 50)) = 15
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.1

        [Header(Edge Glow)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5)) = 1.5

        [Header(Transparency)]
        _Alpha ("Overall Alpha", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Overlay"
            "Queue" = "Overlay+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Back

        Pass
        {
            Name "HolographicPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _TextureBlend;
                float _ScanlineIntensity;
                float _ScanlineSpeed;
                float _ScanlineCount;
                float _ScanlineThickness;
                float _FlickerSpeed;
                float _FlickerIntensity;
                float _FresnelPower;
                float _FresnelIntensity;
                float _Alpha;
            CBUFFER_END

            // Simple noise function for flicker
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sample base texture (original item texture)
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Calculate fresnel (edge glow)
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;

                // Scanlines based on screen position
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float scanline = sin((screenUV.y + _Time.y * _ScanlineSpeed * 0.1) * _ScanlineCount * 3.14159);
                scanline = step(_ScanlineThickness, scanline);
                float scanlineFactor = lerp(1.0, 1.0 - _ScanlineIntensity, scanline);

                // Flicker effect
                float flickerTime = floor(_Time.y * _FlickerSpeed);
                float flicker = 1.0 - _FlickerIntensity * hash(flickerTime);

                // Hologram tint color
                half3 hologramColor = _Color.rgb;

                // Blend original texture with hologram tint
                // _TextureBlend: 0 = pure hologram color, 1 = full original texture tinted
                half3 blendedColor = lerp(hologramColor, texColor.rgb * hologramColor * 2.0, _TextureBlend);

                // Add emission and fresnel glow
                half3 emission = _EmissionColor.rgb * _EmissionIntensity * fresnel;

                // Final color with effects
                half3 finalColor = blendedColor + emission;
                finalColor *= scanlineFactor * flicker;

                // Alpha with fresnel boost at edges
                float finalAlpha = _Alpha * _Color.a * (0.7 + fresnel * 0.5);
                finalAlpha *= flicker;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
