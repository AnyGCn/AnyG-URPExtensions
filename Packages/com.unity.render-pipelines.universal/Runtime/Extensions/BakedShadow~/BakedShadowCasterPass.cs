namespace UnityEngine.Rendering.Universal.Extensions.BakedShadowMap
{
    public class BakedShadowCasterPass : ScriptableRenderPass
    {
        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Baked Shadowmap");
        
        private static class ShaderPropertyID
        {
            public static readonly int _CachedShadowMapTexture = Shader.PropertyToID("_CachedShadowMapTexture");
            public static readonly int _CachedWorldToShadow = Shader.PropertyToID("_CachedWorldToShadow");
        }

        private bool m_CreateEmptyShadowmap;
        private Vector4 m_CascadeSplitDistances;
        private ShadowSliceData m_CascadeSlices;
        private Matrix4x4 m_MainLightShadowMatrices;

        /// <summary>
        /// Creates a new <c>MainLightShadowCasterPass</c> instance.
        /// </summary>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <seealso cref="RenderPassEvent"/>
        public BakedShadowCasterPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(BakedShadowCasterPass));
            renderPassEvent = evt;
        }

        /// <summary>
        /// Sets up the pass.
        /// </summary>
        /// <param name="renderingData"></param>
        /// <returns>True if the pass should be enqueued, otherwise false.</returns>
        /// <seealso cref="RenderingData"/>
        public bool Setup(ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            if (cameraData.targetTexture == null)
            {
                Debug.LogError("Cached Shadow Renderer: Camera target texture is null");
                return false;
            }
            
            if (!renderingData.shadowData.mainLightShadowsEnabled)
                return false;
            
#if UNITY_EDITOR
            if (CoreUtils.IsSceneLightingDisabled(renderingData.cameraData.camera))
                return false;
#endif
            
            using var profScope = new ProfilingScope(null, m_ProfilingSetupSampler);
            
            if (!renderingData.shadowData.supportsMainLightShadows)
                return SetupForEmptyRendering(ref renderingData);
            
            Clear();
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return SetupForEmptyRendering(ref renderingData);
            
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (light.shadows == LightShadows.None)
                return SetupForEmptyRendering(ref renderingData);
            
            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }
            
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out _))
                return SetupForEmptyRendering(ref renderingData);
            
            int shadowResolution = cameraData.cameraTargetDescriptor.width;
            bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                shadowLightIndex, 0, shadowResolution, shadowResolution, shadowResolution, light.shadowNearPlane,
                out m_CascadeSplitDistances, out m_CascadeSlices);
            
            if (!success)
                return SetupForEmptyRendering(ref renderingData);
            
            m_CreateEmptyShadowmap = false;
            useNativeRenderPass = true;
            return true;
        }
        
        bool SetupForEmptyRendering(ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.renderer.stripShadowsOffVariants)
                return false;

            m_CreateEmptyShadowmap = true;
            useNativeRenderPass = false;

            return true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CreateEmptyShadowmap)
                return;

            RenderMainLightCascadeShadowmap(ref context, ref renderingData);
        }
        
        void Clear()
        {
            m_MainLightShadowMatrices = Matrix4x4.identity;
            m_CascadeSplitDistances = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            m_CascadeSlices.Clear();
        }

        void RenderMainLightCascadeShadowmap(ref ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cullResults = renderingData.cullResults;
            var lightData = renderingData.lightData;
            var shadowData = renderingData.shadowData;

            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

            var cmd = renderingData.commandBuffer;
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.MainLightShadow)))
            {
                // Need to start by setting the Camera position and worldToCamera Matrix as that is not set for passes executed before normal rendering
                ShadowUtils.SetCameraPosition(cmd, renderingData.cameraData.worldSpaceCameraPos);

                // Need set the worldToCamera Matrix as that is not set for passes executed before normal rendering,
                // otherwise shadows will behave incorrectly when Scene and Game windows are open at the same time (UUM-63267).
                ShadowUtils.SetWorldToCameraAndCameraToWorldMatrices(cmd, renderingData.cameraData.GetViewMatrix());

                var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex, BatchCullingProjectionType.Orthographic);
                settings.useRenderingLayerMaskTest = UniversalRenderPipeline.asset.useRenderingLayers;

                settings.splitData = m_CascadeSlices.splitData;

                Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref renderingData.shadowData, m_CascadeSlices.projectionMatrix, m_CascadeSlices.resolution);
                ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);
                ShadowUtils.RenderShadowSlice(cmd, ref context, ref m_CascadeSlices, ref settings, m_CascadeSlices.projectionMatrix, m_CascadeSlices.viewMatrix);

                renderingData.shadowData.isKeywordSoftShadowsEnabled = shadowLight.light.shadows == LightShadows.Soft && renderingData.shadowData.supportsSoftShadows;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, renderingData.shadowData.mainLightShadowCascadesCount == 1);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, renderingData.shadowData.mainLightShadowCascadesCount > 1);
                ShadowUtils.SetSoftShadowQualityShaderKeywords(cmd, ref renderingData.shadowData);

                m_MainLightShadowMatrices = m_CascadeSlices.shadowTransform;
                Shader.SetGlobalTexture(ShaderPropertyID._CachedShadowMapTexture, renderingData.cameraData.targetTexture);
                Shader.SetGlobalMatrix(ShaderPropertyID._CachedWorldToShadow, m_MainLightShadowMatrices);
            }
        }
    }
}

