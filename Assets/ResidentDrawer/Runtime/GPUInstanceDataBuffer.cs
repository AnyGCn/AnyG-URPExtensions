using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ResidentDrawer
{
    internal struct GPUInstanceComponentDesc
    {
        public int propertyID;
        public int byteSize;
        public bool isOverriden;
        public bool isPerInstance;

        public GPUInstanceComponentDesc(int inPropertyID, int inByteSize, bool inIsOverriden, bool inPerInstance)
        {
            propertyID = inPropertyID;
            byteSize = inByteSize;
            isOverriden = inIsOverriden;
            isPerInstance = inPerInstance;
        }
    }

    internal struct GPUInstanceDataBufferBuilder : IDisposable
    {
        private NativeList<GPUInstanceComponentDesc> m_Components;

        private MetadataValue CreateMetadataValue(int nameID, int gpuAddress, bool isOverridden)
        {
            const uint kIsOverriddenBit = 0x80000000;
            return new MetadataValue
            {
                NameID = nameID,
                Value = (uint)gpuAddress | (isOverridden ? kIsOverriddenBit : 0),
            };
        }

        public void AddComponent<T>(int propertyID, bool isOverriden, bool isPerInstance) where T : unmanaged
        {
            AddComponent(propertyID, isOverriden, UnsafeUtility.SizeOf<T>(), isPerInstance);
        }

        public void AddComponent(int propertyID, bool isOverriden, int byteSize, bool isPerInstance)
        {
            if (!m_Components.IsCreated)
                m_Components = new NativeList<GPUInstanceComponentDesc>(8, Allocator.Temp);

            m_Components.Add(new GPUInstanceComponentDesc(propertyID, byteSize, isOverriden, isPerInstance));
        }

        public unsafe GPUInstanceDataBuffer Build(int maxInstanceCount)
        {
            int perInstanceComponentCounts = 0;
            var perInstanceComponentIndices = new NativeArray<int>(m_Components.Length, Allocator.Temp);
            var componentAddresses = new NativeArray<int>(m_Components.Length, Allocator.Temp);
            var componentByteSizes = new NativeArray<int>(m_Components.Length, Allocator.Temp);
            var componentInstanceIndexRanges = new NativeArray<Vector2Int>(m_Components.Length, Allocator.Temp);

            GPUInstanceDataBuffer newBuffer = new GPUInstanceDataBuffer();
            newBuffer.instanceCount = maxInstanceCount;
            newBuffer.layoutVersion = GPUInstanceDataBuffer.NextVersion();
            newBuffer.version = 0;
            newBuffer.defaultMetadata = new NativeArray<MetadataValue>(m_Components.Length, Allocator.Persistent);
            newBuffer.descriptions = new NativeArray<GPUInstanceComponentDesc>(m_Components.Length, Allocator.Persistent);
            newBuffer.nameToMetadataMap = new NativeParallelHashMap<int, int>(m_Components.Length, Allocator.Persistent);
            newBuffer.gpuBufferComponentAddress = new NativeArray<int>(m_Components.Length, Allocator.Persistent);

            //Initial offset, must be 0, 0, 0, 0.
            int vec4Size = UnsafeUtility.SizeOf<Vector4>();
            int byteOffset = 4 * vec4Size;

            for (int c = 0; c < m_Components.Length; ++c)
            {
                var componentDesc = m_Components[c];
                newBuffer.descriptions[c] = componentDesc;

                int instancesBegin = 0;
                int instancesEnd = instancesBegin + newBuffer.instanceCount;
                int instancesNum = componentDesc.isPerInstance ? instancesEnd - instancesBegin : 1;
                Assert.IsTrue(instancesNum >= 0);

                componentInstanceIndexRanges[c] = new Vector2Int(instancesBegin, instancesBegin + instancesNum);

                int componentGPUAddress = byteOffset - instancesBegin * componentDesc.byteSize;
                Assert.IsTrue(componentGPUAddress >= 0, "GPUInstanceDataBufferBuilder: GPU address is negative. This is not supported for now. See kIsOverriddenBit." +
                    "In general, if there is only one root InstanceType (MeshRenderer in our case) with a component that is larger or equal in size than any component in a derived InstanceType." +
                    "And the number of parent gpu instances are always larger or equal to the number of derived type gpu instances. Than GPU address cannot become negative.");

                newBuffer.gpuBufferComponentAddress[c] = componentGPUAddress;
                newBuffer.defaultMetadata[c] = CreateMetadataValue(componentDesc.propertyID, componentGPUAddress, componentDesc.isOverriden);

                componentAddresses[c] = componentGPUAddress;
                componentByteSizes[c] = componentDesc.byteSize;

                int componentByteSize = componentDesc.byteSize * instancesNum;
                byteOffset += componentByteSize;

                bool addedToMap = newBuffer.nameToMetadataMap.TryAdd(componentDesc.propertyID, c); 
                Assert.IsTrue(addedToMap, "Repetitive metadata element added to object.");

                if (componentDesc.isPerInstance)
                {
                    perInstanceComponentIndices[perInstanceComponentCounts] = c;
                    perInstanceComponentCounts++;
                }
            }

            int stride = GPUInstanceDataBuffer.IsUBO ? 16 : 4;
            newBuffer.byteSize = byteOffset;
            newBuffer.gpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, newBuffer.byteSize / stride, stride);
            newBuffer.gpuBuffer.SetData(new NativeArray<Vector4>(4, Allocator.Temp), 0, 0, 4);
            newBuffer.validComponentsIndicesGpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, perInstanceComponentCounts, 4);
            newBuffer.validComponentsIndicesGpuBuffer.SetData(perInstanceComponentIndices, 0, 0, perInstanceComponentCounts);
            newBuffer.componentAddressesGpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_Components.Length, 4);
            newBuffer.componentAddressesGpuBuffer.SetData(componentAddresses, 0, 0, m_Components.Length);
            newBuffer.componentInstanceIndexRangesGpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_Components.Length, 8);
            newBuffer.componentInstanceIndexRangesGpuBuffer.SetData(componentInstanceIndexRanges, 0, 0, m_Components.Length);
            newBuffer.componentByteCountsGpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_Components.Length, 4);
            newBuffer.componentByteCountsGpuBuffer.SetData(componentByteSizes, 0, 0, m_Components.Length);
            newBuffer.perInstanceComponentCount = perInstanceComponentCounts;

            perInstanceComponentIndices.Dispose();
            componentAddresses.Dispose();
            componentByteSizes.Dispose();

            return newBuffer;
        }

        public void Dispose()
        {
            if (m_Components.IsCreated)
                m_Components.Dispose();
        }
    }
        
    internal class GPUInstanceDataBuffer : IDisposable
    {
        public static readonly bool IsUBO = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
        private static int s_NextLayoutVersion = 0;
        public static int NextVersion() { return ++s_NextLayoutVersion; }
        
        public int instanceCount;
        public int byteSize;
        public int perInstanceComponentCount;
        public int version;
        public int layoutVersion;
        public GraphicsBuffer gpuBuffer;
        public GraphicsBuffer validComponentsIndicesGpuBuffer;
        public GraphicsBuffer componentAddressesGpuBuffer;
        public GraphicsBuffer componentInstanceIndexRangesGpuBuffer;
        public GraphicsBuffer componentByteCountsGpuBuffer;
        public NativeArray<GPUInstanceComponentDesc> descriptions;
        public NativeArray<MetadataValue> defaultMetadata;
        public NativeArray<int> gpuBufferComponentAddress;
        public NativeParallelHashMap<int, int> nameToMetadataMap;
        
        public bool valid => descriptions.IsCreated;

        private static GPUInstanceIndex CPUInstanceToGPUInstance(InstanceHandle instance)
        {
#if DEBUG
            Assert.IsTrue(instance.valid);
#endif

            if (!instance.valid)
                return GPUInstanceIndex.Invalid;

            int gpuInstanceIndex = instance.instanceIndex;

            return new GPUInstanceIndex { index = gpuInstanceIndex };
        }
        
        public int GetPropertyIndex(int propertyID, bool assertOnFail = true)
        {
            if (nameToMetadataMap.TryGetValue(propertyID, out int componentIndex))
            {
                return componentIndex;
            }

            if (assertOnFail)
                Assert.IsTrue(false, "Count not find gpu address for parameter specified: " + propertyID);
            return -1;
        }
        
        public int GetGpuAddress(string strName, bool assertOnFail = true)
        {
            int componentIndex = GetPropertyIndex(Shader.PropertyToID(strName), false);
            if (assertOnFail && componentIndex == -1)
                Assert.IsTrue(false, "Count not find gpu address for parameter specified: " + strName);

            return componentIndex != -1 ? gpuBufferComponentAddress[componentIndex] : -1;
        }

        public int GetGpuAddress(int propertyID, bool assertOnFail = true)
        {
            int componentIndex = GetPropertyIndex(propertyID, assertOnFail);
            return componentIndex != -1 ? gpuBufferComponentAddress[componentIndex] : -1;
        }
        
        public unsafe InstanceHandle GPUInstanceToCPUInstance(GPUInstanceIndex gpuInstanceIndex)
        {
            Assert.IsTrue(gpuInstanceIndex.index < instanceCount);
            return InstanceHandle.Create(gpuInstanceIndex.index);
        }

        public void CPUInstanceArrayToGPUInstanceArray(NativeArray<InstanceHandle> instances, NativeArray<GPUInstanceIndex> gpuInstanceIndices)
        {
            Assert.AreEqual(instances.Length, gpuInstanceIndices.Length);

            Profiler.BeginSample("CPUInstanceArrayToGPUInstanceArray");

            new ConvertCPUInstancesToGPUInstancesJob { instances = instances, gpuInstanceIndices = gpuInstanceIndices }
                .Schedule(instances.Length, ConvertCPUInstancesToGPUInstancesJob.k_BatchSize).Complete();

            Profiler.EndSample();
        }
        
        public void Dispose()
        {
            if (descriptions.IsCreated)
                descriptions.Dispose();

            if (defaultMetadata.IsCreated)
                defaultMetadata.Dispose();

            if (gpuBufferComponentAddress.IsCreated)
                gpuBufferComponentAddress.Dispose();

            if (nameToMetadataMap.IsCreated)
                nameToMetadataMap.Dispose();

            if (gpuBuffer != null)
                gpuBuffer.Release();

            if (validComponentsIndicesGpuBuffer != null)
                validComponentsIndicesGpuBuffer.Release();

            if (componentAddressesGpuBuffer != null)
                componentAddressesGpuBuffer.Release();

            if (componentInstanceIndexRangesGpuBuffer != null)
                componentInstanceIndexRangesGpuBuffer.Release();

            if (componentByteCountsGpuBuffer != null)
                componentByteCountsGpuBuffer.Release();
        }
        
        public ReadOnly AsReadOnly()
        {
            return new ReadOnly(this);
        }
        
        internal readonly struct ReadOnly
        {

            public ReadOnly(GPUInstanceDataBuffer buffer)
            {
                
            }

            public GPUInstanceIndex CPUInstanceToGPUInstance(InstanceHandle instance)
            {
                return GPUInstanceDataBuffer.CPUInstanceToGPUInstance(instance);
            }

            public void CPUInstanceArrayToGPUInstanceArray(NativeArray<InstanceHandle> instances, NativeArray<GPUInstanceIndex> gpuInstanceIndices)
            {
                Assert.AreEqual(instances.Length, gpuInstanceIndices.Length);

                Profiler.BeginSample("CPUInstanceArrayToGPUInstanceArray");

                new ConvertCPUInstancesToGPUInstancesJob { instances = instances, gpuInstanceIndices = gpuInstanceIndices }
                    .Schedule(instances.Length, ConvertCPUInstancesToGPUInstancesJob.k_BatchSize).Complete();

                Profiler.EndSample();
            }
        }
        
        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        struct ConvertCPUInstancesToGPUInstancesJob : IJobParallelFor
        {
            public const int k_BatchSize = 512;

            [ReadOnly] public NativeArray<InstanceHandle> instances;

            [WriteOnly] public NativeArray<GPUInstanceIndex> gpuInstanceIndices;

            public void Execute(int index)
            {
                gpuInstanceIndices[index] = CPUInstanceToGPUInstance(instances[index]);
            }
        }
    }
}
