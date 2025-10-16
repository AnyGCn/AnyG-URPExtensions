namespace UnityEngine.Rendering.Universal
{
    public enum CachedCSMQuality
    {
        Performance,
        Balanced,
        Quality,
        Ultra,
    }
    
    public class CachedCSMPersistentData
    {
        // Constant
        public const int k_MaxCascades = 4;
        public const int k_ShadowmapBufferBits = 16;
        private const float k_ForceUpdateDiffParam = 0.50f;
        private const float k_InvalidDiffParam = 0.05f;
        
        private const string k_CachedShadowMapTextureName = "_CachedMainLightShadowmapTexture";
        private static readonly float[] k_WorkloadLimit = new float[(int)CachedCSMQuality.Ultra + 1] { 0.4f, 0.5f, 0.6f, 0.7f };
        private static readonly float[] k_CascadeWorkload = new float[k_MaxCascades] { 0.14f, 0.21f, 0.28f, 0.32f };
        private static readonly float[] k_CascadeImportance = new float[k_MaxCascades] { 3.7f, 1.9f, 1.0f, 0.99f };
        
        // Temporary container
        private static readonly float[] s_CascadeImportanceList = new float[k_MaxCascades];
        private static readonly int[] s_CascadeIndexPriorList = new int[k_MaxCascades];
        
        private int m_Width;
        private int m_Height;
        private int m_CascadeCount;
        private RTHandle m_CachedCascadeShadowMap;

        private readonly int[] m_CascadeUpdateFrameCount = new int[k_MaxCascades];
        private readonly ShadowSliceData[] m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
        
        public bool forceUpdate { get; set; } = true;
        public int minCachedLevel { get; private set; } = 1;
        public CachedCSMQuality quality { get; private set; } = CachedCSMQuality.Balanced;
        public int cascadeUpdateMask { get; private set; }
        public int shadowResolution { get; private set; }
        public int renderTargetWidth { get; private set; }
        public int renderTargetHeight { get; private set; }
        public RTHandle cachedCascadeShadowMap => m_CachedCascadeShadowMap;

        public bool Init(ref ShadowData shadowData)
        {
            if (!shadowData.cachedMainlightShadowEnabled)
            {
                DeallocateTargets();
                return false;
            }
            
            int width = shadowData.mainLightShadowmapWidth;
            int height = shadowData.mainLightShadowmapHeight;
            int cascadeCount = shadowData.mainLightShadowCascadesCount;
            minCachedLevel = shadowData.cachedMainLightShadowMinStaticLevel;
            quality = shadowData.cachedMainlightShadowQuality;
            if (m_Width != width || m_Height != height || cascadeCount != m_CascadeCount || m_CachedCascadeShadowMap == null)
            {
                m_Width = width;
                m_Height = height;
                m_CascadeCount = cascadeCount;
                
                shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(m_Width, m_Height, m_CascadeCount);
                renderTargetWidth = m_Width;
                renderTargetHeight = (m_CascadeCount == 2) ? m_Height >> 1 : m_Height;

                for (int i = 0; i < k_MaxCascades; ++i)
                {
                    m_CascadeUpdateFrameCount[i] = 0;
                    m_CascadeSlices[i].Clear();
                }

                DeallocateTargets();
            }

            return true;
        }

        public bool CalculateCascadeSlices(ref CullingResults cullResults, ref ShadowData shadowData,
            int shadowLightIndex, float shadowNearPlane, Vector4[] cascadeSplitDistance,
            ShadowSliceData[] shadowSliceData)
        {
            for (int cascadeIndex = 0; cascadeIndex < m_CascadeCount; ++cascadeIndex)
                s_CascadeImportanceList[cascadeIndex] = k_CascadeImportance[cascadeIndex] * Mathf.Max(Time.frameCount - m_CascadeUpdateFrameCount[cascadeIndex], 1);

            for (int cascadeIndex = 0; cascadeIndex < m_CascadeCount; ++cascadeIndex)
            {
                int prior = 0;
                for (int compareCascadeIndex = 0; compareCascadeIndex < m_CascadeCount; ++compareCascadeIndex)
                    if (cascadeIndex != compareCascadeIndex && s_CascadeImportanceList[cascadeIndex] < s_CascadeImportanceList[compareCascadeIndex])
                        ++prior;

                s_CascadeIndexPriorList[prior] = cascadeIndex;
            }

            cascadeUpdateMask = 0;
            float jobWorkload = 0;
            for (int prior = 0; prior < m_CascadeCount; ++prior)
            {
                int cascadeIndex = s_CascadeIndexPriorList[prior];
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref cullResults, ref shadowData,
                    shadowLightIndex, cascadeIndex, renderTargetWidth, renderTargetHeight, shadowResolution, shadowNearPlane,
                    out var sphereCurr, out var sliceData);
                
                if (!success)
                    return false;
                
                Vector4 sphereSave = m_CascadeSlices[cascadeIndex].splitData.cullingSphere;
                float diff = Vector3.Distance(sphereCurr, sphereSave);
                if (forceUpdate || diff > k_ForceUpdateDiffParam * sphereSave.w ||
                    (jobWorkload + k_CascadeWorkload[cascadeIndex] < k_WorkloadLimit[(int)quality] &&  
                     (cascadeIndex < minCachedLevel || 
                      diff > k_InvalidDiffParam * sphereSave.w)))
                {
                    cascadeUpdateMask |= 1 << cascadeIndex;
                    jobWorkload += k_CascadeWorkload[cascadeIndex];
                    m_CascadeUpdateFrameCount[cascadeIndex] = Time.frameCount;
                    // Avoid flickering in the Frame Debugger caused by changes in shadow rendering states.
                    if (!FrameDebugger.enabled)
                    {
                        m_CascadeSlices[cascadeIndex] = sliceData;
                    }
                }
                else
                {
                    sliceData = m_CascadeSlices[cascadeIndex];
                }
                
                cascadeSplitDistance[cascadeIndex] = sliceData.splitData.cullingSphere;
                shadowSliceData[cascadeIndex] = sliceData;
            }

            forceUpdate = false;
            return true;
        }

        public bool AllocateTargets()
        {
            return ShadowUtils.ShadowRTReAllocateIfNeeded(ref m_CachedCascadeShadowMap, m_Width, m_Height,
                k_ShadowmapBufferBits, name: k_CachedShadowMapTextureName);
        }

        public void DeallocateTargets()
        {
            m_CachedCascadeShadowMap?.Release();
            m_CachedCascadeShadowMap = null;
            forceUpdate = true;
        }
    }
}
