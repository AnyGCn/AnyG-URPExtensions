using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(Light))]
[ExecuteInEditMode]
public partial class CachedShadowCamera : MonoBehaviour
{
    public Transform shadowCenter;
    public float shadowDistance = 400;
    public int shadowMapTexelSize = 4096;
    
    private Camera shadowCastCamera;
    private Light shadowCastLight;
    private RenderTexture cachedShadowMap;
    
    void OnEnable()
    {
        if (shadowCenter == null)
        {
            Debug.LogError("Shadow Center is null");
            this.enabled = false;
            return;
        }

        cachedShadowMap = new RenderTexture(shadowMapTexelSize, shadowMapTexelSize, 16, RenderTextureFormat.Shadowmap);

        shadowCastCamera = GetComponent<Camera>();
        shadowCastLight = GetComponent<Light>();
        transform.position = shadowCenter.position - transform.forward * shadowDistance;
        shadowCastCamera.aspect = 1;
        shadowCastCamera.orthographic = true;
        shadowCastCamera.orthographicSize = shadowDistance;
        shadowCastCamera.farClipPlane = 2 * shadowDistance;
        shadowCastCamera.targetTexture = cachedShadowMap;
        shadowCastCamera.targetDisplay = 1;
        shadowCastCamera.allowHDR = false;
        shadowCastCamera.allowMSAA = false;
        shadowCastCamera.allowDynamicResolution = false;
        shadowCastCamera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
        
        UniversalAdditionalCameraData data = shadowCastCamera.GetUniversalAdditionalCameraData();
        data.antialiasing = AntialiasingMode.None;
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresColorTexture = false;
        data.requiresDepthTexture = false;
        data.allowHDROutput = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(shadowCenter.position, shadowDistance);
    }

    private void OnDisable()
    {
        shadowCastCamera.targetTexture = null;
        CoreUtils.Destroy(cachedShadowMap);
    }
}
