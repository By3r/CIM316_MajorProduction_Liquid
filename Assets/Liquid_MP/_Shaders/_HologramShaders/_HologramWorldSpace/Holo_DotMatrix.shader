Shader "Liquid/Hologram/DotMatrix"
{
    Properties
    {
        _Color            ("Holo Color",       Color)           = (0.7, 1.0, 0.0, 1.0)
        _FresnelPower     ("Fresnel Power",    Range(0.5, 8))   = 1.5
        _FresnelIntensity ("Fresnel Intensity",Range(0, 5))     = 1.0
        _DotScaleU        ("Dot Scale U",      Range(5, 300))   = 60.0
        _DotScaleV        ("Dot Scale V",      Range(5, 300))   = 60.0
        _DotRadius        ("Dot Radius",       Range(0.05,0.49))= 0.35
        _DotSoftness      ("Dot Softness",     Range(0.01,0.5)) = 0.08
        _GlowIntensity    ("Glow Intensity",   Range(0, 8))     = 3.5
        _Alpha            ("Alpha",            Range(0, 1))     = 1.0
        _PulseSpeed       ("Pulse Speed",      Range(0, 5))     = 1.0
        _PulseAmount      ("Pulse Amount",     Range(0, 0.5))   = 0.1
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
            Name "DotMatrix"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _DotScaleU;
                float  _DotScaleV;
                float  _DotRadius;
                float  _DotSoftness;
                float  _GlowIntensity;
                float  _Alpha;
                float  _PulseSpeed;
                float  _PulseAmount;
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
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(posWS));
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));

                float2 dotUV   = frac(IN.uv * float2(_DotScaleU, _DotScaleV)) - 0.5;
                float  dotDist = length(dotUV);
                float  dotMask = 1.0 - smoothstep(_DotRadius - _DotSoftness,
                                                   _DotRadius + _DotSoftness, dotDist);

                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831);

                float3 col   = _Color.rgb * dotMask * _GlowIntensity * pulse;
                float  alpha = dotMask * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
