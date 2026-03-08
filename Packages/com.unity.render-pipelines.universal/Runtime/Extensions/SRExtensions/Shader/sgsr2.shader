Shader "Hidden/Universal Render Pipeline/Extensions/SGSR2"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SGSR2 Convert"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment FragConvert
            #pragma target 4.5

            #include "sgsr2_convert.hlsl"

            half FragConvert(Varyings input) : SV_TARGET
            {
                return SnapdragonGameSuperResolutionConvertPass(input.texcoord);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SGSR2 Upscale"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

            #pragma vertex Vert
            #pragma fragment FragSGSR
            #pragma target 4.5

            #include "sgsr2_upscale.hlsl"

            half4 FragSGSR(Varyings input) : SV_TARGET
            {
                half4 color = SnapdragonGameSuperResolutionUpscalePass(input.texcoord);
                return color;
            }
            ENDHLSL
        }
    }
}