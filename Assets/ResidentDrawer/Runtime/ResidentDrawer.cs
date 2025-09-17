using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ResidentDrawer
{
    public class ResidentDrawer
    {
        private BatchRendererGroup m_BatchRendererGroup;
        private GPUInstanceDataBuffer m_GPUInstanceDataBuffer;

        private NativeParallelHashMap<int, BatchMeshID> m_BatchMeshHash;
        private NativeParallelHashMap<int, BatchMaterialID> m_BatchMaterialHash;

        public ResidentDrawer(int maxInstanceCount)
        {
            m_BatchRendererGroup = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            m_GPUInstanceDataBuffer = RenderersParameters.CreateInstanceDataBuffer(RenderersParameters.Flags.None, maxInstanceCount);
        }
        
        public void Dispose()
        {
            
        }

        private void PostPostLateUpdate()
        {
            Profiler.BeginSample("ResidentDrawer.ProcessChangeData");
            Profiler.EndSample();
            
            
        }
        
        public JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            return default;
        }
    }
}
