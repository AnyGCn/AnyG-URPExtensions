//============================================================================================================
//
//
//                  Copyright (c) 2024, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================

float4                 clipToPrevClip[4];
float2                 renderSize;
float2                 outputSize;
float2                 renderSizeRcp;
float2                 outputSizeRcp;
float2                 jitterOffset;
float2                 scaleRatio;
float                  cameraFovAngleHor;
float                  minLerpContribution;
float                  reset;
uint                   bSameCamera;

#if UNITY_REVERSED_Z
#define NEAREST_DEPTH(a, b) max(a, b)
#define DEPTH_SEPARATION_BASE(depth) depth
#define HAS_VALID_DEPTH(depth) ((depth) > 1.0e-05)
#else
#define NEAREST_DEPTH(a, b) min(a, b)
#define DEPTH_SEPARATION_BASE(depth) (1.0 - (depth))
#define HAS_VALID_DEPTH(depth) ((depth) < 1.0 - 1.0e-05)
#endif

float2 decodeVelocityFromTexture(float2 ev) {
    const float inv_div = 1.0f / (0.499f * 0.5f);
    float2 dv;
    dv.xy = ev.xy * inv_div - 32767.0f / 65535.0f * inv_div;
    //dv.z = uintBitsToFloat((uint(round(ev.z * 65535.0f)) << 16) | uint(round(ev.w * 65535.0f)));
    return dv;
}

half SnapdragonGameSuperResolutionConvertPass(float2 texCoord)
{
    uint2 InputPos = uint2(texCoord * renderSize);
    float2 gatherCoord = texCoord - 0.5 * renderSizeRcp;

    
    // texture gather to find nearest depth
    //      a  b  c  d
    //      e  f  g  h
    //      i  j  k  l
    //      m  n  o  p
    //btmLeft mnji
    //btmRight oplk
    //topLeft  efba
    //topRight ghdc

    float4 btmLeft = GATHER_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, gatherCoord);
    float2 v10 = float2(renderSizeRcp.x * 2.0f, 0.0);
    float4 btmRight = GATHER_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, (gatherCoord+v10));
    float2 v12 = float2(0.0, renderSizeRcp.y * 2.0f);
	float4 topLeft = GATHER_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, (gatherCoord+v12));
	float2 v14 = float2(renderSizeRcp.x * 2.0f, renderSizeRcp.y * 2.0f);
	float4 topRight = GATHER_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, (gatherCoord+v14));
	float centerDepth = NEAREST_DEPTH(NEAREST_DEPTH(NEAREST_DEPTH(btmLeft.z, btmRight.w), topLeft.y), topRight.x);
	float btmLeft4 = NEAREST_DEPTH(NEAREST_DEPTH(NEAREST_DEPTH(btmLeft.y, btmLeft.x), btmLeft.z), btmLeft.w);
	float btmLeftMax9 = NEAREST_DEPTH(topLeft.x, NEAREST_DEPTH(NEAREST_DEPTH(centerDepth, btmLeft4), btmRight.x));

    float depthclip = 0.0;
    if (HAS_VALID_DEPTH(centerDepth))
    {
        float btmRight4 = NEAREST_DEPTH(NEAREST_DEPTH(NEAREST_DEPTH(btmRight.y, btmRight.x), btmRight.z), btmRight.w);
        float topLeft4 = NEAREST_DEPTH(NEAREST_DEPTH(NEAREST_DEPTH(topLeft.y, topLeft.x), topLeft.z), topLeft.w);
        float topRight4 = NEAREST_DEPTH(NEAREST_DEPTH(NEAREST_DEPTH(topRight.y, topRight.x), topRight.z), topRight.w);

        float Wdepth = 0.0;
        float Ksep = 1.37e-05f;
        float Kfov = cameraFovAngleHor;
        float diagonal_length = length(renderSize);
        float Ksep_Kfov_diagonal = Ksep * Kfov * diagonal_length;

		float Depthsep = Ksep_Kfov_diagonal * DEPTH_SEPARATION_BASE(centerDepth);
		float EPSILON = 1.19e-07f;
		Wdepth += clamp((Depthsep / (abs(centerDepth - btmLeft4) + EPSILON)), 0.0, 1.0);
		Wdepth += clamp((Depthsep / (abs(centerDepth - btmRight4) + EPSILON)), 0.0, 1.0);
		Wdepth += clamp((Depthsep / (abs(centerDepth - topLeft4) + EPSILON)), 0.0, 1.0);
		Wdepth += clamp((Depthsep / (abs(centerDepth - topRight4) + EPSILON)), 0.0, 1.0);
        depthclip = clamp(1.0f - Wdepth * 0.25, 0.0, 1.0);
    }

	return depthclip;
	// no need to decode velocity, just use depth clip.
    //refer to ue/fsr2 PostProcessFFX_FSR2ConvertVelocity.usf, and using nearest depth for dilated motion

//     float4 EncodedVelocity = texelFetch(InputVelocity, int2(InputPos), 0);
//
//     float2 motion;
//     if (EncodedVelocity.x > 0.0)
//     {
//         motion = decodeVelocityFromTexture(EncodedVelocity.xy);
//     }
//     else
//     {
// #ifdef REQUEST_NDC_Y_UP
//         float2 ScreenPos = float2(2.0f * texCoord.x - 1.0f, 1.0f - 2.0f * texCoord.y);
// #else
//         float2 ScreenPos = float2(2.0f * texCoord - 1.0f);
// #endif
//         float3 Position = float3(ScreenPos, btmLeftMax9);    //this_clip
//         float4 PreClip = clipToPrevClip[3] + ((clipToPrevClip[2] * Position.z) + ((clipToPrevClip[1] * ScreenPos.y) + (clipToPrevClip[0] * ScreenPos.x)));
//         float2 PreScreen = PreClip.xy / PreClip.w;
//         motion = Position.xy - PreScreen;
//     }
//     MotionDepthClipAlphaBuffer = vec4(motion, depthclip, 0.0);
}
