Shader "Liquid/Beam/WaypointBeacon"
{
    Properties
    {
        _Color          ("Beam Color",          Color)            = (0.4, 0.8, 1.0, 1.0)
        _ColorCore      ("Core Color",          Color)            = (0.9, 1.0, 1.0, 1.0)

        _BeamIntensity  ("Beam Intensity",      Range(0, 8))      = 3.0
        _CoreIntensity  ("Core Intensity",      Range(0, 12))     = 7.0

        // Beam shape — controlled via UV, not world pos
        _BeamWidth      ("Beam Width",          Range(0.0, 0.5))  = 0.12
        _BeamSoftness   ("Beam Softness",       Range(0.001, 0.5))= 0.08
        _CoreWidth      ("Core Width",          Range(0.0, 0.2))  = 0.025
        _CoreSoftness   ("Core Softness",       Range(0.001, 0.2))= 0.02

        // Height fades
        _TopFade        ("Top Fade",            Range(0.1, 8))    = 1.5
        _BottomFade     ("Bottom Fade",         Range(0.01, 1))   = 0.15

        // Scrolling energy bands
        _BandCount      ("Band Count",          Range(0, 30))     = 8.0
        _BandSharpness  ("Band Sharpness",      Range(1, 30))     = 12.0
        _BandSpeed      ("Band Scroll Speed",   Range(0, 10))     = 3.0
        _BandIntensity  ("Band Intensity",      Range(0, 3))      = 0.8

        // Pulse
        _PulseSpeed     ("Pulse Speed",         Range(0, 5))      = 1.2
        _PulseAmount    ("Pulse Amount",        Range(0, 1))      = 0.2

        // Edge shimmer
        _ShimmerScale   ("Shimmer Scale",       Range(0, 20))     = 8.0
        _ShimmerSpeed   ("Shimmer Speed",       Range(0, 5))      = 2.0
        _ShimmerAmount  ("Shimmer Amount",      Range(0, 0.3))    = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BeaconBeam"
            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ColorCore;
                float  _BeamIntensity;
                float  _CoreIntensity;
                float  _BeamWidth;
                float  _BeamSoftness;
                float  _CoreWidth;
                float  _CoreSoftness;
                float  _TopFade;
                float  _BottomFade;
                float  _BandCount;
                float  _BandSharpness;
                float  _BandSpeed;
                float  _BandIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _ShimmerScale;
                float  _ShimmerSpeed;
                float  _ShimmerAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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
                    lerp(Hash(i),                Hash(i + float2(1, 0)), u.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x),
                    u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // UV.x = 0..1 across width, UV.y = 0..1 bottom to top
                // Remap UV.x so 0.5 = centre, dist from centre = 0..0.5
                float  cx      = abs(IN.uv.x - 0.5); // 0 at centre, 0.5 at edge
                float  hn      = IN.uv.y;             // 0 = bottom, 1 = top

                // Shimmer wiggles the apparent centre slightly
                float  shim    = VNoise(float2(hn * _ShimmerScale, t * _ShimmerSpeed));
                float  shim2   = VNoise(float2(hn * _ShimmerScale * 1.6 + 4.1, t * _ShimmerSpeed * 0.8));
                float  shimOff = (shim * 0.6 + shim2 * 0.4 - 0.5) * _ShimmerAmount;
                float  cxS     = abs((IN.uv.x - 0.5) + shimOff);

                // Outer soft beam
                float  beamMask = 1.0 - smoothstep(_BeamWidth - _BeamSoftness,
                                                    _BeamWidth + _BeamSoftness, cxS);

                // Bright core
                float  coreMask = 1.0 - smoothstep(_CoreWidth - _CoreSoftness,
                                                    _CoreWidth + _CoreSoftness, cx);

                // Height fades
                float  topFade    = pow(1.0 - hn, _TopFade);
                float  botFade    = smoothstep(0.0, _BottomFade, hn);
                float  heightMask = topFade * botFade;

                // Scrolling bands travel upward (UV.y increases upward)
                float  bandPos = hn * _BandCount - t * _BandSpeed;
                float  bands   = pow(abs(sin(bandPos * 3.14159265)), _BandSharpness);

                // Pulse
                float  pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed * 6.2831);

                // Compose
                float3 beamCol  = _Color.rgb * (1.0 + bands * _BandIntensity);
                float  beamA    = beamMask * heightMask * _BeamIntensity * pulse;

                float3 coreCol  = _ColorCore.rgb;
                float  coreA    = coreMask * heightMask * _CoreIntensity * pulse;

                float3 col  = beamCol * beamA + coreCol * coreA;
                float  alpha = saturate(beamA + coreA);

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
