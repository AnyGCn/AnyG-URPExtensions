using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class RenderHZB : ScriptableRendererFeature
    {
        [SerializeField] [HideInInspector] [Reload("Runtime/Extensions/HZBOcclusion/DepthPyramidKernels.compute")]
        private ComputeShader m_Shader;

        private RenderHZBPass m_RenderPass = null;

        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
#endif
            // Create the pass...
            if (m_RenderPass == null)
                m_RenderPass = new RenderHZBPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.requiresDepthTexture)
                return;

            if (!renderingData.cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(
                    out var additionalCameraData))
                return;

            Assert.IsNotNull(m_Shader);
            bool shouldAdd = m_RenderPass.Setup(ref renderer, ref m_Shader, ref renderingData,
                additionalCameraData.hzbOcclusionData);
            if (shouldAdd)
                renderer.EnqueuePass(m_RenderPass);
        }

        internal class RenderHZBPass : ScriptableRenderPass
        {
            private ScriptableRenderer m_Renderer;
            private ComputeShader m_Shader;
            private HZBOcclusionData m_OcclusionData;
            private ProfilingSampler m_ProfilingSampler = new ProfilingSampler(nameof(RenderHZBPass));

            public bool Setup(ref ScriptableRenderer renderer, ref ComputeShader shader,
                ref RenderingData renderingData, HZBOcclusionData hzbOcclusionData)
            {
                m_Renderer = renderer;
                m_Shader = shader;
                m_OcclusionData = hzbOcclusionData;
                m_OcclusionData.Setup(new int2(renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height));
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                return true;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                using (new ProfilingScope(renderingData.commandBuffer, m_ProfilingSampler))
                {
                    m_OcclusionData.RenderAndRequestReadbackAsync(renderingData.commandBuffer, m_Shader, 0,
                        m_Renderer.cameraDepthTargetHandle, ref renderingData.cameraData);
                }
            }
        }
    }
}