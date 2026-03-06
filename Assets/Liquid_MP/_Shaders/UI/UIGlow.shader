Shader "Liquid/UI/Glow"
{
    // UI shader with HDR emission support.
    // Based on Unity's UI/Default but outputs values > 1.0
    // so URP Bloom picks them up on a World Space Canvas.
    //
    // Usage:
    //   1. Create a material with this shader.
    //   2. Set _GlowColor to an HDR color (intensity > 1).
    //   3. Assign the material to UI Image components via the Material slot.
    //   4. Bloom will pick up the bright pixels and create a glow.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _GlowColor ("Glow Color", Color) = (0,0,0,0)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 0

        // UI stencil / masking support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIGlow"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 worldPos   : TEXCOORD1;
                half4  mask       : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GlowColor;
                half _GlowIntensity;
                float4 _MainTex_ST;
                float4 _ClipRect;
                float _UIMaskSoftnessX;
                float _UIMaskSoftnessY;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 worldPos = mul(UNITY_MATRIX_M, input.positionOS);
                output.worldPos = worldPos;
                output.positionCS = mul(UNITY_MATRIX_VP, worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;

                // UI masking
                float2 pixelSize = output.positionCS.w;
                pixelSize /= abs(float2(_ScreenParams.x * UNITY_MATRIX_P[0][0],
                                         _ScreenParams.y * UNITY_MATRIX_P[1][1]));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (worldPos.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                output.mask = half4(worldPos.xy * 2.0 - clampedRect.xy - clampedRect.zw,
                                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample texture and apply vertex color + tint
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texColor * input.color;

                // Add HDR glow emission (driven by texture alpha so glow follows the shape)
                half3 glow = _GlowColor.rgb * _GlowIntensity * color.a;
                color.rgb += glow;

                // UI clip rect masking
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // Premultiply alpha (standard UI blending)
                color.rgb *= color.a;

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}
