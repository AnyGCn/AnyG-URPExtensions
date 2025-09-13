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
        private const float k_InvalidDiffParam = 0.05f;
        
        private const string k_CachedShadowMapTextureName = "_CachedMainLightShadowmapTexture";
        private static readonly float[] k_WorkloadLimit = new float[(int)CachedCSMQuality.Ultra + 1] { 0.4f, 0.5f, 0.6f, 0.7f };
        private static readonly float[] k_CascadeWorkload = new float[k_MaxCascades] { 0.14f, 0.21f, 0.28f, 0.32f };
        private static readonly float[] k_CascadeImportance = new float[k_MaxCascades] { 3.7f, 1.9f, 1.0f, 1.0f };
        
        // Temporary container
        private static readonly float[] s_CascadeImportanceList = new float[k_MaxCascades];
        private static readonly int[] s_CascadeIndexPriorList = new int[k_MaxCascades];
        
        private int m_Width;
        private int m_Height;
        private int m_CascadeCount;
        private RTHandle m_CachedCascadeShadowMap;

        private readonly int[] m_CascadeUpdateFrameCount = new int[k_MaxCascades];
        private readonly ShadowSliceData[] m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
        
        public bool forceUpdate { get; set; }
        public int minCachedLevel { get; set; }
        public CachedCSMQuality quality { get; set; }
        
        public int cascadeUpdateMask { get; private set; }
        public int shadowResolution { get; private set; }
        public int renderTargetWidth { get; private set; }
        public int renderTargetHeight { get; private set; }

        public void Init(int width, int height, int cascadeCount)
        {
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
        }

        public bool CalculateCascadeSlices(ref CullingResults cullResults, ref ShadowData shadowData,
            int shadowLightIndex, float shadowNearPlane, Vector4[] cascadeSplitDistance,
            ShadowSliceData[] shadowSliceData)
        {
            for (int cascadeIndex = 0; cascadeIndex < m_CascadeCount; ++cascadeIndex)
                s_CascadeImportanceList[cascadeIndex] = k_CascadeImportance[cascadeIndex] * Mathf.Max(m_CascadeUpdateFrameCount[cascadeIndex] - Time.frameCount, 1);

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
                if (forceUpdate || 
                    (jobWorkload < k_WorkloadLimit[(int)quality] &&  
                     (cascadeIndex < minCachedLevel || 
                      Vector3.Distance(sphereCurr, sphereSave) > k_InvalidDiffParam * sphereSave.w)))
                {
                    cascadeUpdateMask |= 1 << cascadeIndex;
                    jobWorkload += k_CascadeWorkload[cascadeIndex];
                    // Avoid flickering in the Frame Debugger caused by changes in shadow rendering states.
                    if (!FrameDebugger.enabled)
                    {
                        m_CascadeSlices[cascadeIndex] = sliceData;
                        m_CascadeUpdateFrameCount[cascadeIndex] = Time.frameCount;
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
            bool allocated = ShadowUtils.ShadowRTReAllocateIfNeeded(ref m_CachedCascadeShadowMap, m_Width, m_Height,
                k_ShadowmapBufferBits, name: k_CachedShadowMapTextureName);
            if (allocated) forceUpdate = true;
            return allocated;
        }

        public void DeallocateTargets()
        {
            m_CachedCascadeShadowMap?.Release();
            m_CachedCascadeShadowMap = null;
        }
    }
}
