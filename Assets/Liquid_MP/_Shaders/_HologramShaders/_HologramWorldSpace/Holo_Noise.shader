Shader "Liquid/Hologram/Noise"
{
    Properties
    {
        _Color            ("Holo Color",        Color)          = (0.0, 0.8, 1.0, 1.0)
        _FresnelPower     ("Fresnel Power",     Range(0.5, 8))  = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5))    = 2.0
        _GlowIntensity    ("Glow Intensity",    Range(0, 8))    = 4.0
        _Alpha            ("Alpha",             Range(0, 1))    = 0.85
        _NoiseScaleUV     ("Noise Scale UV",    Range(0.5, 20)) = 4.0
        _NoiseScaleTime   ("Noise Time Scale",  Range(0, 5))    = 0.8
        _NoiseContrast    ("Noise Contrast",    Range(0.1, 10)) = 3.0
        _NoiseBrightness  ("Noise Brightness",  Range(-1, 1))   = 0.0
        _NoiseRimMix      ("Rim-Noise Mix",     Range(0, 1))    = 0.5
        _NoiseScale2      ("Noise Scale 2",     Range(0.5, 20)) = 8.0
        _NoiseSpeed2      ("Noise Speed 2",     Range(0, 5))    = 1.5
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
            Name "Noise"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _GlowIntensity;
                float  _Alpha;
                float  _NoiseScaleUV;
                float  _NoiseScaleTime;
                float  _NoiseContrast;
                float  _NoiseBrightness;
                float  _NoiseRimMix;
                float  _NoiseScale2;
                float  _NoiseSpeed2;
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

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(i),               Hash(i + float2(1, 0)), u.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x),
                    u.y);
            }

            float FBM(float2 p)
            {
                float val = 0.0;
                float amp = 0.5;
                float2 pp = p;
                for (int i = 0; i < 3; i++)
                {
                    val += VNoise(pp) * amp;
                    pp  *= 2.1;
                    amp *= 0.5;
                }
                return val;
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

                float t  = _Time.y;
                float n1 = FBM(IN.uv * _NoiseScaleUV + float2(t * _NoiseScaleTime, t * _NoiseScaleTime * 0.7));
                float n2 = FBM(IN.uv * _NoiseScale2   + float2(-t * _NoiseSpeed2,  t * _NoiseSpeed2 * 0.5));
                float n  = saturate(n1 * 0.6 + n2 * 0.4);
                n        = saturate(n * _NoiseContrast + _NoiseBrightness);

                float mask = lerp(n, fres, _NoiseRimMix);

                float3 col   = _Color.rgb * mask * _GlowIntensity;
                float  alpha = mask * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
