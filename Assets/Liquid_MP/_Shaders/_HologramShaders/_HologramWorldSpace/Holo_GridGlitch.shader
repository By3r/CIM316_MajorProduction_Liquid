Shader "Liquid/Hologram/GridGlitch"
{
    Properties
    {
        _Color            ("Holo Color",       Color)           = (0.0, 0.8, 1.0, 1.0)
        _FresnelPower     ("Fresnel Power",    Range(0.5, 8))   = 2.0
        _FresnelIntensity ("Fresnel Intensity",Range(0, 5))     = 1.5
        _GridScaleU       ("Grid Scale U",     Range(1, 200))   = 25.0
        _GridScaleV       ("Grid Scale V",     Range(1, 200))   = 25.0
        _LineWidth        ("Line Width",       Range(0.01,0.49))= 0.05
        _GlowIntensity    ("Glow Intensity",   Range(0, 8))     = 4.0
        _Alpha            ("Alpha",            Range(0, 1))     = 0.9
        _GlitchSpeed      ("Glitch Speed",     Range(0, 20))    = 8.0
        _GlitchIntensity  ("Glitch Intensity", Range(0, 0.3))   = 0.06
        _GlitchFrequency  ("Glitch Frequency", Range(0, 1))     = 0.25
        _BlockSize        ("Block Size",       Range(0.01,0.5)) = 0.08
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
            Name "GridGlitch"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _GridScaleU;
                float  _GridScaleV;
                float  _LineWidth;
                float  _GlowIntensity;
                float  _Alpha;
                float  _GlitchSpeed;
                float  _GlitchIntensity;
                float  _GlitchFrequency;
                float  _BlockSize;
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

            float GridLine(float2 uv, float2 scale, float width)
            {
                float2 g  = frac(uv * scale);
                float2 dg = fwidth(uv * scale);
                float2 ln = smoothstep(width - dg, width + dg, g)
                          * smoothstep(width - dg, width + dg, 1.0 - g);
                return 1.0 - ln.x * ln.y;
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

                float t = _Time.y;
                float2 blockUV  = floor(IN.uv / _BlockSize) * _BlockSize;
                float  blockRnd = Hash(blockUV + floor(t * _GlitchSpeed));
                float  active   = step(1.0 - _GlitchFrequency, blockRnd);
                float  xShift   = (Hash(blockUV + t * 0.3) - 0.5) * _GlitchIntensity * active;
                float2 gUV      = IN.uv + float2(xShift, 0);

                float  grid  = GridLine(gUV, float2(_GridScaleU, _GridScaleV), _LineWidth);
                float  flash = active * step(0.8, Hash(blockUV + t));

                float3 col   = _Color.rgb * (grid + fres * 0.5 + flash * 0.4) * _GlowIntensity;
                float  alpha = (grid * 0.85 + fres * 0.15 + flash * 0.2) * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
