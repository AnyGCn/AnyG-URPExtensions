using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

public class CachedShadowRenderer : ScriptableRenderer
{
    private ShaderTagId[] shadowCastShaderTag = new ShaderTagId[]
    {
        new ShaderTagId("ShadowCaster"),
    };
        
    private DrawObjectsPass m_ShadowCastPass;
    
    public CachedShadowRenderer(CachedShadowRendererData data) : base(data)
    {
        m_ShadowCastPass = new DrawObjectsPass("Cached Shadow Caster", shadowCastShaderTag, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.all, -1, StencilState.defaultValue, 0);
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;
        if (camera.targetTexture == null)
        {
            Debug.LogError("Cached Shadow Renderer: Camera target texture is null");
            return;
        }
        
        EnqueuePass(m_ShadowCastPass);
    }

    public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData)
    {
        cullingParameters.cullingOptions = CullingOptions.None;
    }
}
