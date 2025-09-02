namespace UnityEngine.Rendering.Universal.Extensions.BakedShadowMap
{
    public class BakedShadowRenderer : ScriptableRenderer
    {
        private BakedShadowCasterPass m_ShadowCastPass;

        public BakedShadowRenderer(BakedShadowRendererData data) : base(data)
        {
            m_ShadowCastPass = new BakedShadowCasterPass(RenderPassEvent.BeforeRenderingOpaques);
        }

        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_ShadowCastPass.Setup(ref renderingData))
                EnqueuePass(m_ShadowCastPass);
        }

        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData)
        {
            cameraData.maxShadowDistance *= 2;
            cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.ShadowCasters | CullingOptions.DisablePerObjectCulling;;
            cullingParameters.maximumVisibleLights = 1;
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }
    }
}
