Shader "Liquid/Hologram/Gradient"
{
    Properties
    {
        _ColorTop         ("Color Top",        Color)          = (0.0, 0.5, 1.0, 1.0)
        _ColorBottom      ("Color Bottom",     Color)          = (0.0, 1.0, 0.5, 1.0)
        _FresnelPower     ("Fresnel Power",    Range(0.5, 8))  = 2.0
        _FresnelIntensity ("Fresnel Intensity",Range(0, 5))    = 2.0
        _GlowIntensity    ("Glow Intensity",   Range(0, 8))    = 4.0
        _Alpha            ("Alpha",            Range(0, 1))    = 0.85
        _GradientOffset   ("Gradient Offset",  Range(-1, 1))   = 0.0
        _GradientScale    ("Gradient Scale",   Range(0.1, 3))  = 1.0
        _PulseSpeed       ("Pulse Speed",      Range(0, 5))    = 0.8
        _PulseAmount      ("Pulse Amount",     Range(0, 0.5))  = 0.15
        _ScanlineCount    ("Subtle Scanlines", Range(0, 100))  = 30.0
        _ScanlineIntensity("Scanline Intensity",Range(0, 1))   = 0.2
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
            Name "Gradient"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorTop;
                float4 _ColorBottom;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _GlowIntensity;
                float  _Alpha;
                float  _GradientOffset;
                float  _GradientScale;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _ScanlineCount;
                float  _ScanlineIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(OUT.positionWS));
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));
                float  fres  = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                float  tGrad    = saturate((IN.uv.y + _GradientOffset) * _GradientScale);
                float3 gradCol  = lerp(_ColorBottom.rgb, _ColorTop.rgb, tGrad);

                float scan   = sin(IN.positionWS.y * _ScanlineCount * 6.2831) * 0.5 + 0.5;
                gradCol     *= 1.0 - scan * _ScanlineIntensity;

                float pulse  = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831);

                float3 col   = gradCol * (fres * 0.5 + 0.5) * _GlowIntensity * pulse;
                float  alpha = (fres * 0.3 + 0.7) * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
