Shader "Liquid/HolographicObject"
{
    Properties
    {
        _ColorA             ("Color A (Core)",        Color)         = (0.0, 0.9, 0.8, 1.0)
        _ColorB             ("Color B (Mid)",         Color)         = (0.2, 0.7, 1.0, 1.0)
        _ColorC             ("Color C (Rim/Gold)",    Color)         = (1.0, 0.9, 0.1, 1.0)
        _FresnelPower       ("Fresnel Power",         Range(0.5, 8)) = 2.5
        _FresnelIntensity   ("Fresnel Intensity",     Range(0, 5))   = 2.0
        _ScanlineCount      ("Scanline Count",        Range(10, 300))= 80
        _ScanlineSpeed      ("Scanline Speed",        Range(0, 5))   = 0.6
        _ScanlineWidth      ("Scanline Width",        Range(0.01, 0.99)) = 0.4
        _ScanlineIntensity  ("Scanline Intensity",    Range(0, 2))   = 0.8
        _IridescenceScale   ("Iridescence Scale",     Range(0.1, 5)) = 1.5
        _IridescenceSpeed   ("Iridescence Speed",     Range(0, 3))   = 0.4
        _IridescenceShift   ("Iridescence Hue Shift", Range(0, 1))   = 0.5
        _EmissionIntensity  ("Emission Intensity",    Range(0, 8))   = 3.0
        _Alpha              ("Base Alpha",            Range(0, 1))   = 0.6
        _AlphaFresnelBoost  ("Alpha Fresnel Boost",   Range(0, 1))   = 0.4
        _FlickerSpeed       ("Flicker Speed",         Range(0, 20))  = 6.0
        _FlickerIntensity   ("Flicker Intensity",     Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;
            float  _FresnelPower;
            float  _FresnelIntensity;
            float  _ScanlineCount;
            float  _ScanlineSpeed;
            float  _ScanlineWidth;
            float  _ScanlineIntensity;
            float  _IridescenceScale;
            float  _IridescenceSpeed;
            float  _IridescenceShift;
            float  _EmissionIntensity;
            float  _Alpha;
            float  _AlphaFresnelBoost;
            float  _FlickerSpeed;
            float  _FlickerIntensity;
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
            float3 positionWS : TEXCOORD2;
            float2 uv         : TEXCOORD3;
        };

        float3 HsvToRgb(float h, float s, float v)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(h.xxx + K.xyz) * 6.0 - K.www);
            return v * lerp(K.xxx, saturate(p - K.xxx), s);
        }

        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
        }

        float SmoothNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(Hash21(i),               Hash21(i + float2(1, 0)), u.x),
                lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x),
                u.y);
        }

        Varyings HoloVert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
            OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(OUT.positionWS));
            OUT.uv         = IN.uv;
            return OUT;
        }

        float4 HoloFrag(Varyings IN, float emissiveScale)
        {
            float t = _Time.y;

            float3 N = normalize(IN.normalWS);
            float3 V = normalize(IN.viewDirWS);

            float NdotV   = saturate(dot(N, V));
            float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

            float  hue      = NdotV * _IridescenceScale + t * _IridescenceSpeed + _IridescenceShift;
            float3 iriColor = HsvToRgb(frac(hue), 0.9, 1.0);

            float3 col = lerp(_ColorA.rgb, _ColorB.rgb, NdotV);
            col = lerp(col, iriColor, 0.55);
            col = lerp(col, _ColorC.rgb, saturate(fresnel));

            float scanPos    = IN.positionWS.y * _ScanlineCount - t * _ScanlineSpeed;
            float scanMask   = step(_ScanlineWidth, frac(scanPos));
            float brightLine = 1.0 - step(0.97, frac(scanPos));

            col = col * (1.0 - scanMask * _ScanlineIntensity * 0.4);
            col += brightLine * _ColorC.rgb * 0.4;

            float aberr = sin(IN.positionWS.y * _ScanlineCount * 0.5 - t * 2.0) * 0.5 + 0.5;
            col.r += aberr * 0.08 * fresnel;
            col.b += (1.0 - aberr) * 0.08 * fresnel;

            col *= _EmissionIntensity * emissiveScale;

            float alpha = _Alpha + fresnel * _AlphaFresnelBoost;
            alpha *= (1.0 - scanMask * 0.3);

            float flicker = 1.0 - _FlickerIntensity * SmoothNoise(float2(t * _FlickerSpeed, 1.3));
            col   *= flicker;
            alpha *= flicker;

            return float4(col, saturate(alpha));
        }

        ENDHLSL

        Pass
        {
            Name "HologramBack"
            Cull Front
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            Varyings vert(Attributes IN) { return HoloVert(IN); }
            float4 frag(Varyings IN) : SV_Target { return HoloFrag(IN, 0.5); }

            ENDHLSL
        }

        Pass
        {
            Name "HologramFront"
            Cull Back
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            Varyings vert(Attributes IN) { return HoloVert(IN); }
            float4 frag(Varyings IN) : SV_Target { return HoloFrag(IN, 1.0); }

            ENDHLSL
        }

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
