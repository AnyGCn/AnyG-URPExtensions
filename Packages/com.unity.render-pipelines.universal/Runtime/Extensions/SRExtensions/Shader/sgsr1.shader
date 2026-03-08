Shader "Hidden/Universal Render Pipeline/Extensions/SGSR1"
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
            Name "SGSR"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

            #pragma vertex Vert
            #pragma fragment FragSGSR
            #pragma target 4.5

            #define SGSR_MOBILE

            half4 SGSRRH(float2 p)
            {
                half4 res = GATHER_RED_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p);
                return res;
            }

            half4 SGSRGH(float2 p)
            {
                half4 res = GATHER_GREEN_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p);
                return res;
            }

            half4 SGSRBH(float2 p)
            {
                half4 res = GATHER_BLUE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p);
                return res;
            }

            half4 SGSRAH(float2 p)
            {
                half4 res = GATHER_ALPHA_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p);
                return res;
            }

            half4 SGSRRGBH(float2 p)
            {
                half4 res = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, p, 0);
                return res;
            }

            half4 SGSRH(float2 p, uint channel)
            {
                if (channel == 0)
                    return SGSRRH(p);
                if (channel == 1)
                    return SGSRGH(p);
                if (channel == 2)
                    return SGSRBH(p);
                return SGSRAH(p);
            }

            #include "sgsr1_mobile.hlsl"
            // =====================================================================================
            // 
            // SNAPDRAGON GAME SUPER RESOLUTION
            // 
            // =====================================================================================
            half4 SnapdragonGameSuperResolution(float2 uv)
            {
                half4 OutColor = half4(0, 0, 0, 1);
                SgsrYuvH(OutColor, uv, _ScreenSize.zwxy);
                return OutColor;
            }

            half4 FragSGSR(Varyings input) : SV_TARGET
            {
                half4 color = SnapdragonGameSuperResolution(input.texcoord);
                return color;
            }
            ENDHLSL
        }
    }
}