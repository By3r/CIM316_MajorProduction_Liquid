Shader "Liquid/Hologram/ScanlineWorldSpace"
{
    Properties
    {
        _Color             ("Holo Color",        Color)          = (1.0, 0.9, 0.0, 1.0)
        _FresnelPower      ("Fresnel Power",      Range(0.5, 8))  = 2.0
        _FresnelIntensity  ("Fresnel Intensity",  Range(0, 5))    = 2.5
        _ScanlineCount     ("Scanline Count",     Range(5, 300))  = 60
        _ScanlineSpeed     ("Scanline Speed",     Range(0, 5))    = 0.5
        _ScanlineSharpness ("Scanline Sharpness", Range(1, 50))   = 20.0
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
            Name "ScanlineWS"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _ScanlineCount;
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
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(OUT.positionWS));
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));
                float  fres  = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                float scan = IN.positionWS.y * _ScanlineCount - _Time.y * _ScanlineSpeed;
                float scanBand = pow(abs(sin(scan * 3.14159265)), _ScanlineSharpness);

                float3 col   = _Color.rgb * (scanBand + fres * 0.5) * _GlowIntensity;
                float  alpha = (scanBand * 0.7 + fres * 0.3) * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
