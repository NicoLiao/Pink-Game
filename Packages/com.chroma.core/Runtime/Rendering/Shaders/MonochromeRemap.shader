// Full-screen remap of the camera color to the game's monochrome palette.
Shader "Chroma/MonochromeRemap"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.chroma.core/Runtime/Rendering/Shaders/Monochrome.hlsl"

        TEXTURE2D(_ChromaSecondaryMask);
        ENDHLSL

        // World: primary palette faded by _ChromaFade, secondary where the mask is set.
        Pass
        {
            Name "Remap"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                half secondary = SAMPLE_TEXTURE2D(_ChromaSecondaryMask, sampler_LinearClamp, input.texcoord).r;
                half3 primaryColor = ChromaRemap(source.rgb, _ChromaPrimaryHS.xy, _ChromaFade.x);
                half3 secondaryColor = ChromaRemap(source.rgb, _ChromaSecondaryHS.xy, 0);
                return half4(lerp(primaryColor, secondaryColor, secondary), source.a);
            }
            ENDHLSL
        }

        // Overlay: faded primary palette where UI covers, untouched elsewhere.
        Pass
        {
            Name "RemapOverlay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                half coverage = SAMPLE_TEXTURE2D(_ChromaSecondaryMask, sampler_LinearClamp, input.texcoord).r;
                half3 primaryColor = ChromaRemap(source.rgb, _ChromaPrimaryHS.xy, _ChromaFade.x);
                return half4(lerp(source.rgb, primaryColor, coverage), source.a);
            }
            ENDHLSL
        }
    }
}
