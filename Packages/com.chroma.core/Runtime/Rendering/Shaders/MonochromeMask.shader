// Override shader that writes the Secondary mask; pass index matches MonochromeMaskMode.
Shader "Hidden/Chroma/MonochromeMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        int _ChromaSecondaryRenderingLayer;
        float _ChromaDepthTolerance;

        // Returns 1 if the drawn renderer is on the Secondary rendering layer, else 0.
        half IsSecondaryRenderer()
        {
            return (GetMeshRenderingLayer() & (uint)_ChromaSecondaryRenderingLayer) != 0 ? 1.0 : 0.0;
        }
        ENDHLSL

        // World3D: marks visible Secondary pixels, occluded by the camera depth texture.
        Pass
        {
            Name "Mask3D"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Converts raw device depth to eye depth for perspective and orthographic cameras.
            float ChromaEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w == 0.0) return LinearEyeDepth(rawDepth, _ZBufferParams);
                #if UNITY_REVERSED_Z
                rawDepth = 1.0 - rawDepth;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float sceneDepth = ChromaEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)));
                float fragmentDepth = ChromaEyeDepth(input.positionCS.z);
                clip(sceneDepth * (1.0 + _ChromaDepthTolerance) - fragmentDepth);
                return 1.0;
            }
            ENDHLSL
        }

        // World2D: every sprite writes 1 or 0 in sort order, weighted by its alpha.
        Pass
        {
            Name "Mask2D"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half alpha : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.alpha = input.color.a * unity_SpriteColor.a;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = input.alpha * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(IsSecondaryRenderer() * alpha, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }

        // Overlay: writes UI coverage so covered pixels are remapped to primary.
        Pass
        {
            Name "MaskOverlay"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _ClipRect;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half alpha : COLOR;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.alpha = input.color.a;
                output.uv = input.uv;
                output.positionOS = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = input.alpha * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                #if UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.positionOS) * step(input.positionOS, _ClipRect.zw);
                alpha *= inside.x * inside.y;
                #endif
                return half4(alpha, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
}
