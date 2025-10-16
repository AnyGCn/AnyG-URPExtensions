using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DepthPyramidManager
{
    
}
public class RenderHZB : ScriptableRendererFeature
{
    [SerializeField]
    [HideInInspector]
    [Reload("Runtime/Extensions/HZBOcclusion/DepthPyramidKernels.compute")]
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
        
        bool shouldAdd = m_RenderPass.Setup(ref renderer, ref m_Shader, ref renderingData);
        if (shouldAdd)
            renderer.EnqueuePass(m_RenderPass);
    }

    protected override void Dispose(bool disposing)
    {
        m_RenderPass?.Dispose();
        m_RenderPass = null;
    }
    
    public static Dictionary<int, NativeArray<float>> depthPyramid = new Dictionary<int, NativeArray<float>>();
    
    internal class RenderHZBPass : ScriptableRenderPass
    {
        public const int k_MaxOccluderMips = 7;
        private const int k_MaxDepthPixel = 1 << k_MaxOccluderMips;

        private int m_FirstDepthMipIndex;
        private int m_DepthMips;
        
        private RenderTextureDescriptor m_TextureDesc = new RenderTextureDescriptor(0, 0, RenderTextureFormat.RFloat, 0, 1)
        {
            enableRandomWrite = true,
        };
        
        private ScriptableRenderer m_Renderer;
        private ComputeShader m_Shader;
        private RTHandle m_OccluderDepthPyramid;
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler(nameof(RenderHZBPass));
        private int4[] occluderMipBounds = new int4[k_MaxOccluderMips];
        private NativeArray<float> m_OccluderDepthBuffer;
        private int lastOccluderFrameCount = -1;
        
        public bool Setup(ref ScriptableRenderer renderer, ref ComputeShader shader, ref RenderingData renderingData)
        {
            m_Renderer = renderer;
            m_Shader = shader;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            UpdateMipBounds(new Vector2Int(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height));
            return true;
        }
        
        private void UpdateMipBounds(Vector2Int depthBufferSize)
        {
            m_FirstDepthMipIndex = 0;
            int occluderPixelSize = 1;
            Vector2Int topMipSize = depthBufferSize;
            while (m_FirstDepthMipIndex < 4 && (topMipSize.x >= k_MaxDepthPixel || topMipSize.y >= k_MaxDepthPixel))
            {
                m_FirstDepthMipIndex++;
                occluderPixelSize = 1 << m_FirstDepthMipIndex;
                topMipSize = (depthBufferSize + (occluderPixelSize - 1) * Vector2Int.one) / occluderPixelSize;
            }
            
            Vector2Int totalSize = Vector2Int.zero;
            Vector2Int mipOffset = Vector2Int.zero;
            Vector2Int mipSize = topMipSize;

            for (int mipIndex = 0; mipIndex < k_MaxOccluderMips; ++mipIndex)
            {
                occluderMipBounds[mipIndex] = new int4(mipOffset.x, mipOffset.y, mipSize.x, mipSize.y);

                totalSize.x = Mathf.Max(totalSize.x, mipOffset.x + mipSize.x);
                totalSize.y = Mathf.Max(totalSize.y, mipOffset.y + mipSize.y);

                if (mipIndex == 0)
                {
                    mipOffset.x = 0;
                    mipOffset.y += mipSize.y;
                }
                else
                {
                    mipOffset.x += mipSize.x;
                }
                mipSize.x = (mipSize.x + 1) / 2;
                mipSize.y = (mipSize.y + 1) / 2;
            }

            m_TextureDesc.width = totalSize.x;
            m_TextureDesc.height = totalSize.y;
            if (RenderingUtils.ReAllocateIfNeeded(ref m_OccluderDepthPyramid, m_TextureDesc, FilterMode.Point,
                    TextureWrapMode.Clamp))
            {
                if (m_OccluderDepthBuffer.IsCreated)
                    m_OccluderDepthBuffer.Dispose();

                m_OccluderDepthBuffer = new NativeArray<float>(totalSize.x * totalSize.y, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }
        
        private static class ShaderIDs
        {
            public static readonly int _SrcDepth = Shader.PropertyToID(nameof(_SrcDepth));
            public static readonly int _DstDepth = Shader.PropertyToID(nameof(_DstDepth));
            public static readonly int _MipCount = Shader.PropertyToID(nameof(_MipCount)); 
            public static readonly int[] _MipOffsetAndSize = new int[5]; 

            static ShaderIDs()
            {
                for (int i = 0; i < _MipOffsetAndSize.Length; ++i)
                    _MipOffsetAndSize[i] = Shader.PropertyToID($"_MipOffsetAndSize{i}");
            }
        }
        
        // private OccluderDepthPyramidConstants SetupFarDepthPyramidConstants()
        // {
            // OccluderDepthPyramidConstants cb = new OccluderDepthPyramidConstants();

            // // write globals
            // cb._OccluderMipLayoutSizeX = (uint)occluderMipLayoutSize.x;
            // cb._OccluderMipLayoutSizeY = (uint)occluderMipLayoutSize.y;
            //
            // // write per-subview data
            // ref readonly OccluderSubviewUpdate update = ref occluderSubviewUpdates[updateIndex];
            //
            // int subviewIndex = update.subviewIndex;
            // subviewData[subviewIndex] = OccluderDerivedData.FromParameters(update);
            // subviewValidMask |= 1 << update.subviewIndex;
            //
            // Matrix4x4 viewProjMatrix
            //     = update.gpuProjMatrix
            //       * update.viewMatrix
            //       * Matrix4x4.Translate(-update.viewOffsetWorldSpace);
            // Matrix4x4 invViewProjMatrix = viewProjMatrix.inverse;
            //
            // unsafe
            // {
            //     for (int j = 0; j < 16; ++j)
            //         cb._InvViewProjMatrix[16 * updateIndex + j] = invViewProjMatrix[j];
            //
            //     cb._SrcOffset[4 * updateIndex + 0] = (uint)update.depthOffset.x;
            //     cb._SrcOffset[4 * updateIndex + 1] = (uint)update.depthOffset.y;
            //     cb._SrcOffset[4 * updateIndex + 2] = 0;
            //     cb._SrcOffset[4 * updateIndex + 3] = 0;
            // }
            //
            // cb._SrcSliceIndices |= (((uint)update.depthSliceIndex & 0xf) << (4 * updateIndex));
            // cb._DstSubviewIndices |= ((uint)subviewIndex << (4 * updateIndex));

            // return cb;
        // }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = renderingData.commandBuffer;
            
            var srcKeyword = new LocalKeyword(m_Shader, "USE_SRC");

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                int mipCount = m_FirstDepthMipIndex + k_MaxOccluderMips;
                for (int mipIndexBase = 0; mipIndexBase < mipCount - 1; mipIndexBase += 4)
                {
                    cmd.SetComputeTextureParam(m_Shader, 0, ShaderIDs._DstDepth, m_OccluderDepthPyramid);

                    bool useSrc = (mipIndexBase == 0);
                    cmd.SetKeyword(m_Shader, srcKeyword, useSrc);
                    if (useSrc)
                        cmd.SetComputeTextureParam(m_Shader, 0, ShaderIDs._SrcDepth, m_Renderer.cameraDepthTargetHandle);

                    cmd.SetComputeIntParams(m_Shader, ShaderIDs._MipCount, Mathf.Min(mipCount - 1 - mipIndexBase, 4));
                    
                    int2 srcSize = 0;
                    for (int i = 0; i < 5; ++i)
                    {
                        int2 offset = 0;
                        int2 size = 0;
                        int mipIndex = mipIndexBase + i;
                        if (mipIndex == 0)
                        {
                            size = new int2(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height);
                        }
                        else
                        {
                            int occMipIndex = mipIndex - m_FirstDepthMipIndex;
                            if (0 <= occMipIndex && occMipIndex < k_MaxOccluderMips)
                            {
                                offset = occluderMipBounds[occMipIndex].xy;
                                size = occluderMipBounds[occMipIndex].zw;
                            }
                        }
                        if (i == 0)
                            srcSize = size;
                        
                        cmd.SetComputeVectorParam(m_Shader, ShaderIDs._MipOffsetAndSize[i], new Vector4(offset.x, offset.y, size.x, size.y));
                    }
                    
                    // cmd.SetComputeConstantBufferParam(m_Shader, ShaderIDs.OccluderDepthPyramidConstants, constantBuffer, 0, constantBuffer.stride);
                    cmd.DispatchCompute(m_Shader, 0, (srcSize.x + 15) / 16, (srcSize.y + 15) / 16, 1);
                    cmd.RequestAsyncReadback(m_OccluderDepthPyramid, OnReadbackCallback);
                }
            }
        }

        private void OnReadbackCallback(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                NativeArray<float> readbackData = request.GetData<float>();
                if (m_OccluderDepthBuffer.IsCreated && m_OccluderDepthBuffer.Length == readbackData.Length)
                {
                    m_OccluderDepthBuffer.CopyFrom(readbackData);
                    lastOccluderFrameCount = Time.frameCount;
                }
            }
        }

        public void Dispose()
        {
            m_OccluderDepthBuffer.Dispose();
            m_OccluderDepthPyramid?.Release();
            m_OccluderDepthPyramid = null;
        }
    }
}
