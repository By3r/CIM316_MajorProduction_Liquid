Shader "Custom/Hologram/ScanlineScreenSpace"
{
    Properties
    {
        _Color             ("Holo Color",        Color)          = (0.0, 1.0, 0.2, 1.0)
        _FresnelPower      ("Fresnel Power",      Range(0.5, 8))  = 1.8
        _FresnelIntensity  ("Fresnel Intensity",  Range(0, 5))    = 2.0
        _ScanlineFreq      ("Scanline Frequency", Range(10, 500)) = 120.0
        _ScanlineSpeed     ("Scanline Speed",     Range(0, 10))   = 2.0
        _ScanlineSharpness ("Scanline Sharpness", Range(1, 50))   = 18.0
        _GlowIntensity     ("Glow Intensity",     Range(0, 8))    = 4.0
        _Alpha             ("Alpha",              Range(0, 1))    = 0.85
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
            Name "ScanlineSS"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _ScanlineFreq;
                float  _ScanlineSpeed;
                float  _ScanlineSharpness;
                float  _GlowIntensity;
                float  _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(posWS));
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));
                float  fres  = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                float2 sUV = IN.screenPos.xy / IN.screenPos.w;
                float  scan = sUV.y * _ScanlineFreq - _Time.y * _ScanlineSpeed;
                float  scanBand = pow(abs(sin(scan * 3.14159265)), _ScanlineSharpness);

                float3 col   = _Color.rgb * (scanBand + fres * 0.4) * _GlowIntensity;
                float  alpha = (scanBand * 0.7 + fres * 0.3) * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
