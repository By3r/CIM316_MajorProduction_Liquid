Shader "Liquid/Hologram/Grid"
{
    Properties
    {
        _Color            ("Holo Color",       Color)           = (0.8, 0.0, 1.0, 1.0)
        _FresnelPower     ("Fresnel Power",    Range(0.5, 8))   = 2.0
        _FresnelIntensity ("Fresnel Intensity",Range(0, 5))     = 1.5
        _GridScaleU       ("Grid Scale U",     Range(1, 200))   = 30.0
        _GridScaleV       ("Grid Scale V",     Range(1, 200))   = 30.0
        _LineWidth        ("Line Width",       Range(0.01,0.49))= 0.05
        _GlowIntensity    ("Glow Intensity",   Range(0, 8))     = 4.0
        _Alpha            ("Alpha",            Range(0, 1))     = 0.9
        _ScrollSpeed      ("Scroll Speed",     Range(0, 2))     = 0.1
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
            Name "Grid"

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
                float  _ScrollSpeed;
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

                float2 sUV  = IN.uv + float2(0, _Time.y * _ScrollSpeed);
                float  grid = GridLine(sUV, float2(_GridScaleU, _GridScaleV), _LineWidth);

                float3 col   = _Color.rgb * (grid + fres * 0.5) * _GlowIntensity;
                float  alpha = (grid * 0.85 + fres * 0.15) * _Alpha;
                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
