Shader "Liquid/Directional/PositionMarker"
{
    Properties
    {
        _Color          ("Beam Color",          Color)          = (0.4, 0.7, 1.0, 1.0)
        _ColorCore      ("Core Color (Bright)", Color)          = (0.8, 1.0, 1.0, 1.0)

        // Beam shape
        _BeamRadius     ("Beam Radius",         Range(0.01, 2.0)) = 0.18
        _BeamHeight     ("Beam Height",         Range(0.5, 30.0)) = 8.0
        _BeamSoftness   ("Beam Edge Softness",  Range(0.001, 1.0))= 0.12
        _CoreRadius     ("Core Radius",         Range(0.001, 1.0))= 0.04
        _CoreSoftness   ("Core Softness",       Range(0.001, 0.5))= 0.03

        // Beam brightness / fade
        _BeamIntensity  ("Beam Intensity",      Range(0, 8))    = 3.0
        _CoreIntensity  ("Core Intensity",      Range(0, 12))   = 6.0
        _TopFade        ("Top Fade Sharpness",  Range(0.5, 8))  = 2.0
        _BottomFade     ("Bottom Fade",         Range(0.01, 2)) = 0.3

        // Scrolling energy bands
        _BandCount      ("Band Count",          Range(0, 20))   = 6.0
        _BandSharpness  ("Band Sharpness",      Range(1, 30))   = 10.0
        _BandSpeed      ("Band Scroll Speed",   Range(0, 10))   = 2.5
        _BandIntensity  ("Band Intensity",      Range(0, 2))    = 0.6

        // Pulse
        _PulseSpeed     ("Pulse Speed",         Range(0, 5))    = 1.2
        _PulseAmount    ("Pulse Amount",        Range(0, 0.5))  = 0.15

        // Particle shimmer (noise on beam edge)
        _ShimmerScale   ("Shimmer Scale",       Range(0, 20))   = 6.0
        _ShimmerSpeed   ("Shimmer Speed",       Range(0, 5))    = 1.5
        _ShimmerAmount  ("Shimmer Amount",      Range(0, 0.5))  = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+1"
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
                float  _BeamRadius;
                float  _BeamHeight;
                float  _BeamSoftness;
                float  _CoreRadius;
                float  _CoreSoftness;
                float  _BeamIntensity;
                float  _CoreIntensity;
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
                float3 positionWS : TEXCOORD0;
                float3 originWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            // Hash + smooth noise for shimmer
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
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                // Object origin in world space (base of the beam)
                OUT.originWS   = TransformObjectToWorld(float3(0, 0, 0));
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                float3 origin = IN.originWS;

                // Horizontal distance from beam axis
                float2 horiz   = IN.positionWS.xz - origin.xz;
                float  radDist = length(horiz);

                // Height above object origin (0 = ground, BeamHeight = top)
                float  h       = IN.positionWS.y - origin.y;

                // Clip anything below ground or above beam height
                clip(h);
                clip(_BeamHeight - h);

                // Normalised height 0..1
                float  hn = saturate(h / _BeamHeight);

                // --- Shimmer: wiggle the effective radius slightly with noise ---
                float shimNoise = VNoise(float2(hn * _ShimmerScale, t * _ShimmerSpeed));
                float shimNoise2= VNoise(float2(hn * _ShimmerScale * 1.7 + 5.3, t * _ShimmerSpeed * 0.7));
                float shimmer   = (shimNoise * 0.6 + shimNoise2 * 0.4) * _ShimmerAmount;

                float effectiveRadius = _BeamRadius * (1.0 + shimmer);
                float effectiveCore   = _CoreRadius;

                // --- Outer beam (soft gaussian-like glow) ---
                float beamMask = 1.0 - smoothstep(effectiveRadius - _BeamSoftness,
                                                   effectiveRadius + _BeamSoftness, radDist);

                // --- Bright core line ---
                float coreMask = 1.0 - smoothstep(effectiveCore - _CoreSoftness,
                                                   effectiveCore + _CoreSoftness, radDist);

                // --- Height fades: bottom flare, top fade ---
                float topFade    = pow(1.0 - hn, _TopFade);
                float bottomFade = smoothstep(0.0, _BottomFade, h);
                float heightMask = topFade * bottomFade;

                // --- Scrolling energy bands (rings travelling upward) ---
                float bandPos  = h * _BandCount - t * _BandSpeed;
                float bands    = pow(abs(sin(bandPos * 3.14159265)), _BandSharpness);

                // --- Pulse ---
                float pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed * 6.2831);

                // --- Compose ---
                // Outer beam colour
                float3 beamCol  = _Color.rgb * (1.0 + bands * _BandIntensity);
                float  beamAlpha= beamMask * heightMask * _BeamIntensity * pulse;

                // Core bright column
                float3 coreCol  = _ColorCore.rgb;
                float  coreAlpha= coreMask * heightMask * _CoreIntensity * pulse;

                // Blend core over beam
                float3 col   = beamCol * beamAlpha + coreCol * coreAlpha;
                float  alpha = saturate(beamAlpha + coreAlpha);

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
