Shader "Liquid/Hologram/Glitch"
{
    Properties
    {
        _Color            ("Holo Color",        Color)          = (1.0, 0.05, 0.1, 1.0)
        _FresnelPower     ("Fresnel Power",     Range(0.5, 8))  = 1.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5))    = 3.0
        _RimWidth         ("Rim Width",         Range(0.01, 1)) = 0.35
        _GlowIntensity    ("Glow Intensity",    Range(0, 8))    = 5.0
        _Alpha            ("Alpha",             Range(0, 1))    = 0.95
        _GlitchSpeed      ("Glitch Speed",      Range(0, 30))   = 12.0
        _GlitchAmount     ("Glitch Amount",     Range(0, 0.5))  = 0.12
        _GlitchFrequency  ("Glitch Frequency",  Range(0, 1))    = 0.4
        _ChromaShift      ("Chroma Shift",      Range(0, 0.1))  = 0.015
        _NoiseBands       ("Noise Bands",       Range(1, 40))   = 12.0
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
            Name "Glitch"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _RimWidth;
                float  _GlowIntensity;
                float  _Alpha;
                float  _GlitchSpeed;
                float  _GlitchAmount;
                float  _GlitchFrequency;
                float  _ChromaShift;
                float  _NoiseBands;
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

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
                float  fres  = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                float  rim   = smoothstep(0.0, _RimWidth, fres);

                float t    = _Time.y;
                float band = floor(IN.uv.y * _NoiseBands);
                float rnd  = Hash(float2(band, floor(t * _GlitchSpeed)));
                float on   = step(1.0 - _GlitchFrequency, rnd);
                float xShift = (Hash(float2(band * 2.1, t * 0.5)) - 0.5) * _GlitchAmount * on;

                float rimR = rim * (1.0 + xShift * 3.0);
                float rimB = rim * (1.0 - xShift * 3.0);
                float flash = step(0.97, Hash(float2(floor(t * _GlitchSpeed * 0.3), 3.7)));

                float3 col;
                col.r = _Color.r * saturate(rimR) * _GlowIntensity;
                col.g = _Color.g * rim             * _GlowIntensity * (1.0 + flash * 0.5);
                col.b = _Color.b * saturate(rimB)  * _GlowIntensity;

                float alpha = rim * _Alpha + flash * 0.15;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
