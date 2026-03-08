Shader "Liquid/Hologram/Basic"
{
    Properties
    {
        _Color          ("Holo Color",    Color)          = (0.0, 1.0, 0.9, 1.0)
        _FresnelPower   ("Fresnel Power", Range(0.5, 8))  = 2.0
        _RimWidth       ("Rim Width",     Range(0.01, 1)) = 0.3
        _GlowIntensity  ("Glow Intensity",Range(0, 8))    = 4.0
        _Alpha          ("Alpha",         Range(0, 1))    = 0.9
        _PulseSpeed     ("Pulse Speed",   Range(0, 5))    = 1.2
        _PulseAmount    ("Pulse Amount",  Range(0, 1))    = 0.2
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
            Name "BasicHologram"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _RimWidth;
                float  _GlowIntensity;
                float  _Alpha;
                float  _PulseSpeed;
                float  _PulseAmount;
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
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(posWS));
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));
                float  fres  = pow(1.0 - NdotV, _FresnelPower);
                float  rim   = smoothstep(0.0, _RimWidth, fres);
                float  pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831);
                float3 col   = _Color.rgb * rim * _GlowIntensity * pulse;
                float  alpha = rim * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
