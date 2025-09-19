using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace ResidentDrawer
{
    internal struct RenderersBatchersContextDesc
    {
        public int instanceNum;
        public bool supportDitheringCrossFade;
        public bool enableBoundingSpheresInstanceData;
        public float smallMeshScreenPercentage;
        public bool enableCullerDebugStats;

        public static RenderersBatchersContextDesc NewDefault()
        {
            return new RenderersBatchersContextDesc()
            {
                instanceNum = 1024,
            };
        }
    }
    
    internal class RenderersBatchersContext : IDisposable
    {
        public RenderersParameters renderersParameters
        {
            get { return m_RenderersParameters; }
        }

        public GraphicsBuffer gpuInstanceDataBuffer
        {
            get { return m_InstanceDataBuffer.gpuBuffer; }
        }

        public int activeLodGroupCount
        {
            get { return m_LODGroupDataPool.activeLodGroupCount; }
        }

        public NativeArray<GPUInstanceComponentDesc>.ReadOnly defaultDescriptions
        {
            get { return m_InstanceDataBuffer.descriptions.AsReadOnly(); }
        }

        public NativeArray<MetadataValue> defaultMetadata
        {
            get { return m_InstanceDataBuffer.defaultMetadata; }
        }

        public NativeList<LODGroupCullingData> lodGroupCullingData
        {
            get { return m_LODGroupDataPool.lodGroupCullingData; }
        }

        public int instanceDataBufferVersion
        {
            get { return m_InstanceDataBuffer.version; }
        }

        public int instanceDataBufferLayoutVersion
        {
            get { return m_InstanceDataBuffer.layoutVersion; }
        }

        public int crossfadedRendererCount
        {
            get { return m_LODGroupDataPool.crossfadedRendererCount; }
        }

        public SphericalHarmonicsL2 cachedAmbientProbe
        {
            get { return m_CachedAmbientProbe; }
        }

        public bool hasBoundingSpheres
        {
            get { return m_InstanceDataSystem.hasBoundingSpheres; }
        }

        public CPUInstanceData.ReadOnly instanceData
        {
            get { return m_InstanceDataSystem.instanceData; }
        }

        public CPUSharedInstanceData.ReadOnly sharedInstanceData
        {
            get { return m_InstanceDataSystem.sharedInstanceData; }
        }

        public GPUInstanceDataBuffer.ReadOnly instanceDataBuffer
        {
            get { return m_InstanceDataBuffer.AsReadOnly(); }
        }

        public NativeArray<InstanceHandle> aliveInstances
        {
            get { return m_InstanceDataSystem.aliveInstances; }
        }

        public float smallMeshScreenPercentage
        {
            get { return m_SmallMeshScreenPercentage; }
        }

        private InstanceDataSystem m_InstanceDataSystem;

        private LODGroupDataPool m_LODGroupDataPool;

        internal GPUInstanceDataBuffer m_InstanceDataBuffer;
        private RenderersParameters m_RenderersParameters;

        internal CommandBuffer m_CmdBuffer;

        private SphericalHarmonicsL2 m_CachedAmbientProbe;

        private float m_SmallMeshScreenPercentage;

        private DebugRendererBatcherStats m_DebugStats;

        internal DebugRendererBatcherStats debugStats
        {
            get => m_DebugStats;
        }

        public RenderersBatchersContext(in RenderersBatchersContextDesc desc)
        {
            RenderersParameters.Flags rendererParametersFlags = RenderersParameters.Flags.None;
            if (desc.enableBoundingSpheresInstanceData)
                rendererParametersFlags |= RenderersParameters.Flags.UseBoundingSphereParameter;

            m_InstanceDataBuffer =
                RenderersParameters.CreateInstanceDataBuffer(rendererParametersFlags, desc.instanceNum);
            m_RenderersParameters = new RenderersParameters(m_InstanceDataBuffer);
            m_LODGroupDataPool = new LODGroupDataPool(desc.instanceNum, desc.supportDitheringCrossFade);

            m_CmdBuffer = new CommandBuffer();
            m_CmdBuffer.name = "GPUCullingCommands";

            m_CachedAmbientProbe = RenderSettings.ambientProbe;

            m_InstanceDataSystem = new InstanceDataSystem(desc.instanceNum, desc.enableBoundingSpheresInstanceData);
            m_SmallMeshScreenPercentage = desc.smallMeshScreenPercentage;

            m_DebugStats = desc.enableCullerDebugStats ? new DebugRendererBatcherStats() : null;
        }

        public void Dispose()
        {
            m_InstanceDataSystem.Dispose();

            m_CmdBuffer.Release();

            m_LODGroupDataPool.Dispose();
            m_InstanceDataBuffer.Dispose();

            m_DebugStats?.Dispose();
            m_DebugStats = null;
        }
        
        public void UpdatePerFrameInstanceVisibility(in ParallelBitArray compactedVisibilityMasks)
        {
            m_InstanceDataSystem.UpdatePerFrameInstanceVisibility(compactedVisibilityMasks);
        }

        public void UpdateLODGroupData(LODGroupInputData.ReadOnly asReadOnly)
        {
            m_LODGroupDataPool.UpdateLODGroupData(asReadOnly);
        }

        public JobHandle ScheduleQueryRendererGroupInstancesJob(NativeArray<int>.ReadOnly rendererGroupIDs, NativeArray<InstanceHandle> instances)
        {
            return m_InstanceDataSystem.ScheduleQueryRendererGroupInstancesJob(rendererGroupIDs, instances);
        }
        
        public JobHandle ScheduleQueryRendererGroupInstancesJob(NativeArray<int>.ReadOnly rendererGroupIDs, NativeList<InstanceHandle> instances)
        {
            return m_InstanceDataSystem.ScheduleQueryRendererGroupInstancesJob(rendererGroupIDs, instances);
        }

        public void FreeRendererGroupInstances(NativeArray<int>.ReadOnly rendererGroupsID)
        {
            m_InstanceDataSystem.FreeRendererGroupInstances(rendererGroupsID);
        }

        public void ReallocateAndGetInstances(RendererGroupInputData.ReadOnly rendererData, NativeArray<InstanceHandle> instances)
        {
            // m_InstanceDataSystem.ReallocateAndGetInstances(rendererData, instances);

            EnsureInstanceBufferCapacity();
        }
        
        private void EnsureInstanceBufferCapacity()
        {
            const int kMeshRendererGrowNum = 1024;
            const int kSpeedTreeGrowNum = 256;

            int maxCPUMeshRendererNum = m_InstanceDataSystem.GetMaxInstancesOfType();

            int maxGPUMeshRendererInstances = m_InstanceDataBuffer.instanceCount;

            bool needToGrow = false;

            if(maxCPUMeshRendererNum > maxGPUMeshRendererInstances)
            {
                needToGrow = true;
                maxGPUMeshRendererInstances = maxCPUMeshRendererNum + kMeshRendererGrowNum;
            }

            if (needToGrow)
                GrowInstanceBuffer(maxGPUMeshRendererInstances);
        }
        
        public void GrowInstanceBuffer(int instanceNumInfo)
        {
            // CPU Copy
            // using (var grower = new GPUInstanceDataBufferGrower(m_InstanceDataBuffer, instanceNumInfo))
            // {
            //     var newInstanceDataBuffer = grower.SubmitToGpu(ref m_GrowerResources);
            //
            //     if (newInstanceDataBuffer != m_InstanceDataBuffer)
            //     {
            //         if (m_InstanceDataBuffer != null)
            //             m_InstanceDataBuffer.Dispose();
            //
            //         m_InstanceDataBuffer = newInstanceDataBuffer;
            //     }
            // }

            m_RenderersParameters = new RenderersParameters(m_InstanceDataBuffer);
        }
    }
}