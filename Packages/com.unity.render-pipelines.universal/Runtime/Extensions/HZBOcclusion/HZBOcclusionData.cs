using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
    public class HZBOcclusionFrameData : IDisposable
    {
        private bool m_Disposed;
        private NativeArray<float> m_HZBuffer;
        
        public NativeArray<float> hzBuffer => m_HZBuffer;
        public int lastRenderFrameCount { get; private set; } = -1;
        public bool onFlight { get; private set; } = false;
        public bool valid => !onFlight && lastRenderFrameCount != -1;
        public Matrix4x4 viewProjMatrix { get; private set; }
        public Vector3 viewOriginWorldSpace { get; private set; }
        public Vector3 facingDirWorldSpace { get; private set; }
        public Vector3 radialDirWorldSpace { get; private set; }

        public HZBOcclusionFrameData(int2 textureSize)
        {
            m_HZBuffer = new NativeArray<float>(textureSize.x * textureSize.y, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        bool UpdateFrameInfo(ref CameraData cameraData)
        {
#if UNITY_EDITOR
            // pause would lead to readback stuck.
            if (EditorApplication.isPaused)
                return false;
#endif
            
            lastRenderFrameCount = Time.frameCount;
            var viewMatrix = cameraData.GetViewMatrix();
            var viewMatrixInverse = viewMatrix.inverse;
            var projMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
            viewMatrix.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            viewProjMatrix = projMatrix * viewMatrix;   // without translation
            viewOriginWorldSpace = viewMatrixInverse.GetColumn(3);
            facingDirWorldSpace = ((Vector3)viewMatrixInverse.GetColumn(2)).normalized;
            radialDirWorldSpace = ((Vector3)(viewMatrixInverse.GetColumn(0) + viewMatrixInverse.GetColumn(1))).normalized;
            onFlight = true;
            return true;
        }
        
        public void RequestAsyncReadback(CommandBuffer cmd, ComputeBuffer computeBuffer, ref CameraData cameraData)
        {
            if (!UpdateFrameInfo(ref cameraData)) return;
            cmd.RequestAsyncReadbackIntoNativeArray(ref m_HZBuffer, computeBuffer, OnRequestCallback);
        }
        
        public void RequestAsyncReadback(CommandBuffer cmd, RenderTexture renderTexture, ref CameraData cameraData)
        {
            if (!UpdateFrameInfo(ref cameraData)) return;
            cmd.RequestAsyncReadbackIntoNativeArray(ref m_HZBuffer, renderTexture, OnRequestCallback);
        }
        
        void OnRequestCallback(AsyncGPUReadbackRequest request)
        {
            if (m_Disposed)
            {
                m_HZBuffer.Dispose();
                return;
            }

            onFlight = false;
            if (!request.done || request.hasError)
            {
                Assert.IsTrue(m_HZBuffer.IsCreated);
                lastRenderFrameCount = -1;
            }
        }

        public void SetDirty()
        {
            lastRenderFrameCount = -1;
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;
            
            m_Disposed = true;
            // if we are on flight, we need to wait for the readback to complete and dispose it in callback function.
            if (disposing && !onFlight)
                m_HZBuffer.Dispose();
        }
    }
    
    public class HZBOcclusionData : IDisposable
    {
        // Some android devices read back texture too slow on render thread, so we use compute buffer to read back texture.
        public static readonly bool k_UseComputeBuffer = true;
        public static Dictionary<int, HZBOcclusionData> OcclusionData = new Dictionary<int, HZBOcclusionData>();
        
        public readonly static int k_MaxReadbackBufferCount = Application.isMobilePlatform ? 4 : 3;
        public const int k_MaxOccluderMips = 7;
        private const int k_MaxDepthPixel = 1 << k_MaxOccluderMips;

        private int m_CameraInstanceID = 0;
        private RTHandle m_OccluderDepthPyramid;
        private ComputeBuffer m_DepthPyramidBuffer;
        private int2 m_DepthBufferSize;

        public int2 totalSize { get; private set; }
        public int2 topMipSize { get; private set; }
        public int firstDepthMipIndex { get; private set; }
        public int depthMips { get; private set; }
        
        public int4[] occluderMipBounds { get; private set; } = new int4[k_MaxOccluderMips];

        private Queue<HZBOcclusionFrameData> m_RingBuffer = new Queue<HZBOcclusionFrameData>();
        
        internal HZBOcclusionData()
        {
        }

        public HZBOcclusionFrameData GetLatestFrameData()
        {
            HZBOcclusionFrameData result = null;
            foreach (var frameData in m_RingBuffer)
            {
                if (!frameData.valid)
                    break;
                
                if (result == null || result.lastRenderFrameCount < frameData.lastRenderFrameCount)
                    result = frameData;
            }
            
            return result;
        }
        
        internal void Setup(int2 depthBufferSize)
        {
            if (math.all(m_DepthBufferSize == depthBufferSize))
                return;
            
            ClearData();
            m_DepthBufferSize = depthBufferSize;
            firstDepthMipIndex = 0;
            topMipSize = depthBufferSize;
            depthMips = k_MaxOccluderMips;
            while (firstDepthMipIndex < 4 && (topMipSize.x >= k_MaxDepthPixel || topMipSize.y >= k_MaxDepthPixel))
            {
                firstDepthMipIndex++;
                int occluderPixelSize = 1 << firstDepthMipIndex;
                topMipSize = (depthBufferSize + (occluderPixelSize - 1) * 1) / occluderPixelSize;
            }
            
            totalSize = 0;
            int2 mipOffset = 0;
            int2 mipSize = topMipSize;

            for (int mipIndex = 0; mipIndex < depthMips; ++mipIndex)
            {
                occluderMipBounds[mipIndex] = new int4(mipOffset.x, mipOffset.y, mipSize.x, mipSize.y);
                totalSize = new int2(Mathf.Max(totalSize.x, mipOffset.x + mipSize.x), Mathf.Max(totalSize.y, mipOffset.y + mipSize.y));
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

            if (k_UseComputeBuffer)
            {
                m_DepthPyramidBuffer = new ComputeBuffer(totalSize.x * totalSize.y, sizeof(float), ComputeBufferType.Structured);
            }
            else
            {
                RenderTextureDescriptor textureDesc =
                    new RenderTextureDescriptor(totalSize.x, totalSize.y, RenderTextureFormat.RFloat, 0, 1)
                    {
                        enableRandomWrite = true,
                    };

                RenderingUtils.ReAllocateIfNeeded(ref m_OccluderDepthPyramid, textureDesc,
                    FilterMode.Point, TextureWrapMode.Clamp);
            }

            for (int i = 0; i < k_MaxReadbackBufferCount; ++i)
                m_RingBuffer.Enqueue(new HZBOcclusionFrameData(totalSize));
        }

        private static class ShaderIDs
        {
            public static readonly int _SrcDepth = Shader.PropertyToID(nameof(_SrcDepth));
            public static readonly int _DstDepthBuffer = Shader.PropertyToID(nameof(_DstDepthBuffer));
            public static readonly int _DstDepthTexture = Shader.PropertyToID(nameof(_DstDepthTexture));
            public static readonly int _MipCount = Shader.PropertyToID(nameof(_MipCount)); 
            public static readonly int _DepthPyramidSize = Shader.PropertyToID(nameof(_DepthPyramidSize));
            public static readonly int[] _MipOffsetAndSize = new int[5]; 

            static ShaderIDs()
            {
                for (int i = 0; i < _MipOffsetAndSize.Length; ++i)
                    _MipOffsetAndSize[i] = Shader.PropertyToID($"_MipOffsetAndSize{i}");
            }
        }
        
        internal bool RenderAndRequestReadbackAsync(CommandBuffer cmd, ComputeShader shader, int kernalIndex, RTHandle cameraDepthTargetHandle, ref CameraData cameraData)
        {
            if (!m_RingBuffer.TryPeek(out var frameData) || frameData.onFlight)
                return false;

            m_RingBuffer.Dequeue();
            var bufferKeyword = new LocalKeyword(shader, "USE_BUFFER");
            var srcKeyword = new LocalKeyword(shader, "USE_SRC");
            var srcIsMsaaKeyword = new LocalKeyword(shader, "SRC_IS_MSAA");

            bool srcIsMsaa = cameraDepthTargetHandle?.isMSAAEnabled ?? false;
            int mipCount = firstDepthMipIndex + depthMips;
            cmd.SetComputeVectorParam(shader, ShaderIDs._DepthPyramidSize, new Vector4(totalSize.x, totalSize.y, 0, 0));
            for (int mipIndexBase = 0; mipIndexBase < mipCount - 1; mipIndexBase += 4)
            {
                if (k_UseComputeBuffer)
                    cmd.SetComputeBufferParam(shader, kernalIndex, ShaderIDs._DstDepthBuffer, m_DepthPyramidBuffer);
                else
                    cmd.SetComputeTextureParam(shader, kernalIndex, ShaderIDs._DstDepthTexture, m_OccluderDepthPyramid);

                bool useSrc = (mipIndexBase == 0);
                cmd.SetKeyword(shader, srcKeyword, useSrc);
                cmd.SetKeyword(shader, srcIsMsaaKeyword, useSrc && srcIsMsaa);
                cmd.SetKeyword(shader, bufferKeyword, k_UseComputeBuffer);
                if (useSrc)
                    cmd.SetComputeTextureParam(shader, kernalIndex, ShaderIDs._SrcDepth, cameraDepthTargetHandle);

                cmd.SetComputeIntParams(shader, ShaderIDs._MipCount, Mathf.Min(mipCount - 1 - mipIndexBase, 4));
                
                int2 srcSize = 0;
                for (int i = 0; i < 5; ++i)
                {
                    int2 offset = 0;
                    int2 size = 0;
                    int mipIndex = mipIndexBase + i;
                    if (mipIndex == 0)
                    {
                        size = m_DepthBufferSize;
                    }
                    else
                    {
                        int occMipIndex = mipIndex - firstDepthMipIndex;
                        if (0 <= occMipIndex && occMipIndex < depthMips)
                        {
                            offset = occluderMipBounds[occMipIndex].xy;
                            size = occluderMipBounds[occMipIndex].zw;
                        }
                    }
                    if (i == 0)
                        srcSize = size;
                    
                    cmd.SetComputeVectorParam(shader, ShaderIDs._MipOffsetAndSize[i], new Vector4(offset.x, offset.y, size.x, size.y));
                }
                
                cmd.DispatchCompute(shader, kernalIndex, (srcSize.x + 15) / 16, (srcSize.y + 15) / 16, 1);
            }
            
            if (k_UseComputeBuffer)
                frameData.RequestAsyncReadback(cmd, m_DepthPyramidBuffer, ref cameraData);
            else
                frameData.RequestAsyncReadback(cmd, m_OccluderDepthPyramid, ref cameraData);

            m_RingBuffer.Enqueue(frameData);
            Assert.IsTrue(m_CameraInstanceID == 0 || m_CameraInstanceID == cameraData.camera.GetInstanceID());
            m_CameraInstanceID = cameraData.camera.GetInstanceID();
            OcclusionData.TryAdd(m_CameraInstanceID, this);
            return true;
        }
        
        internal void ClearData()
        {
            foreach (var data in m_RingBuffer)
                data.Dispose();

            m_RingBuffer.Clear();
            m_OccluderDepthPyramid?.Release();
            m_OccluderDepthPyramid = null;
            m_DepthPyramidBuffer?.Dispose();
            OcclusionData.Remove(m_CameraInstanceID);
            m_CameraInstanceID = 0;
        }
        
        public void Dispose()
        {
            ClearData();
        }
    }
}
