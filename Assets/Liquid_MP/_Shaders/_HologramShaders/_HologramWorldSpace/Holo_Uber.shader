Shader "Liquid/Hologram/Uber"
{
    Properties
    {
        _ColorA           ("Color A (Core)",      Color)           = (1.0, 0.0, 0.8, 1.0)
        _ColorB           ("Color B (Mid)",       Color)           = (0.4, 0.0, 1.0, 1.0)
        _ColorC           ("Color C (Rim)",       Color)           = (0.0, 0.6, 1.0, 1.0)
        _FresnelPower     ("Fresnel Power",       Range(0.5, 8))   = 2.0
        _FresnelIntensity ("Fresnel Intensity",   Range(0, 5))     = 2.5
        _ScanlineCount    ("Scanline Count",      Range(5, 300))   = 60
        _ScanlineSpeed    ("Scanline Speed",      Range(0, 5))     = 0.6
        _ScanlineSharpness("Scanline Sharpness",  Range(1, 50))    = 20.0
        _ScanlineIntensity("Scanline Intensity",  Range(0, 2))     = 0.8
        _GridScale        ("Grid Scale",          Range(0, 100))   = 0.0
        _GridLineWidth    ("Grid Line Width",      Range(0.01,0.49))= 0.05
        _GridIntensity    ("Grid Intensity",      Range(0, 1))     = 0.5
        _IriScale         ("Iridescence Scale",   Range(0.1, 5))   = 1.5
        _IriSpeed         ("Iridescence Speed",   Range(0, 3))     = 0.4
        _IriAmount        ("Iridescence Amount",  Range(0, 1))     = 0.5
        _GlitchSpeed      ("Glitch Speed",        Range(0, 30))    = 10.0
        _GlitchAmount     ("Glitch Amount",       Range(0, 0.3))   = 0.04
        _GlitchFrequency  ("Glitch Frequency",    Range(0, 1))     = 0.15
        _NoiseScale       ("Noise Scale",         Range(0, 20))    = 3.0
        _NoiseSpeed       ("Noise Speed",         Range(0, 5))     = 0.5
        _NoiseAmount      ("Noise Amount",        Range(0, 1))     = 0.2
        _GlowIntensity    ("Glow Intensity",      Range(0, 8))     = 4.0
        _Alpha            ("Alpha",               Range(0, 1))     = 0.85
        _AlphaFresnelBoost("Alpha Fresnel Boost", Range(0, 1))     = 0.3
        _FlickerSpeed     ("Flicker Speed",       Range(0, 20))    = 6.0
        _FlickerIntensity ("Flicker Intensity",   Range(0, 0.5))   = 0.08
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
            Name "UberHologram"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _ScanlineCount;
                float  _ScanlineSpeed;
                float  _ScanlineSharpness;
                float  _ScanlineIntensity;
                float  _GridScale;
                float  _GridLineWidth;
                float  _GridIntensity;
                float  _IriScale;
                float  _IriSpeed;
                float  _IriAmount;
                float  _GlitchSpeed;
                float  _GlitchAmount;
                float  _GlitchFrequency;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseAmount;
                float  _GlowIntensity;
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
                float2 uv         : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
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

            float3 HsvToRgb(float h, float s, float v)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(h.xxx + K.xyz) * 6.0 - K.www);
                return v * lerp(K.xxx, saturate(p - K.xxx), s);
            }

            float GridMask(float2 uv, float scale, float width)
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
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(OUT.positionWS));
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));
                float  fres  = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // Glitch UV offset
                float  band    = floor(IN.uv.y * 20.0);
                float  rnd     = Hash(float2(band, floor(t * _GlitchSpeed)));
                float  active  = step(1.0 - _GlitchFrequency, rnd);
                float  xShift  = (Hash(float2(band * 2.1, t * 0.5)) - 0.5) * _GlitchAmount * active;
                float2 gUV     = IN.uv + float2(xShift, 0);

                // Iridescence
                float3 iriCol = HsvToRgb(frac(NdotV * _IriScale + t * _IriSpeed), 0.9, 1.0);

                // Base colour
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, NdotV);
                col = lerp(col, _ColorC.rgb, saturate(fres * 0.5));
                col = lerp(col, iriCol, _IriAmount);

                // Scanlines
                float scan     = IN.positionWS.y * _ScanlineCount - t * _ScanlineSpeed;
                float scanLine = pow(abs(sin(scan * 3.14159265)), _ScanlineSharpness);
                col *= 1.0 - (1.0 - scanLine) * _ScanlineIntensity * 0.5;
                col += scanLine * _ColorC.rgb * 0.25 * _ScanlineIntensity;

                // Grid overlay (only when GridScale > 0)
                float gridMask = GridMask(gUV, _GridScale, _GridLineWidth);
                col = lerp(col, col * gridMask + _ColorA.rgb * gridMask * 0.4,
                           _GridIntensity * step(0.5, _GridScale));

                // Noise
                float noise = VNoise(gUV * _NoiseScale + t * _NoiseSpeed);
                col = lerp(col, col * (0.5 + noise), _NoiseAmount);

                // Emission
                col *= _GlowIntensity;

                // Alpha
                float alpha = _Alpha + fres * _AlphaFresnelBoost;
                alpha      *= (0.5 + scanLine * 0.5);

                // Flicker
                float flicker = 1.0 - _FlickerIntensity * VNoise(float2(t * _FlickerSpeed, 1.3));
                col   *= flicker;
                alpha *= flicker;

                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
