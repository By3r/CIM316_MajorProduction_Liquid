Shader "Hidden/Haze/Composite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "HazeComposite"

            HLSLPROGRAM
            #include "FroxelFogCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment TRICUBIC_SAMPLING

            TEXTURE3D(_ScatterBuffer);
            TEXTURE2D(_GLOBAL_BloomTexture);
            float _HazeBloomIntensity;
            float _VolumeNearClipPlane;
            float _VolumeFarClipPlane;
            float4x4 _FroxelVolumeVP;
            float _HazeBlurRadius;
            float _IGNStrength;

            half4 frag (Varyings input) : SV_Target
            {
                uint bufferWidth;
                uint bufferHeight;
                uint bufferDepth;
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if UNITY_REVERSED_Z
                real depth = SampleSceneDepth(input.texcoord);
#else
                real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(input.texcoord));
#endif
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                _ScatterBuffer.GetDimensions(bufferWidth, bufferHeight, bufferDepth);
                float3 uvw = WorldToUV(worldPos, _VolumeNearClipPlane, _VolumeFarClipPlane, lerp(_FroxelVolumeVP, UNITY_MATRIX_V, unity_OrthoParams.w), bufferDepth, unity_OrthoParams.w);
                uvw.xyz += IGN(input.texcoord.x * _BlitTexture_TexelSize.z, input.texcoord.y * _BlitTexture_TexelSize.w, _Time.y * unity_DeltaTime.w) * 0.01 * _IGNStrength;
#ifdef TRICUBIC_SAMPLING
                float4 scatterBuffer = SampleTexture3DBicubic(_ScatterBuffer, uvw, float3(bufferWidth, bufferHeight, bufferDepth));
#else
                float4 scatterBuffer = SAMPLE_TEXTURE3D_LOD(_ScatterBuffer, sampler_TrilinearClamp, uvw, 0);
#endif
                float4 bloomTex = SAMPLE_TEXTURE2D(_GLOBAL_BloomTexture, sampler_LinearClamp, input.texcoord);
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                color = lerp(color, half4(bloomTex.rgb,1) * (1 / _HazeBlurRadius), _HazeBloomIntensity * (1.0 - scatterBuffer.a));
                color.rgb = color.rgb * lerp(scatterBuffer.a, 1, _HazeBloomIntensity) + scatterBuffer.rgb;
                return float4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}