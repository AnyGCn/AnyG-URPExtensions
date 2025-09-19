using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace ResidentDrawer
{
    public struct LODGroupItem
    {
        public const int k_MaxLODLevelsCount = 8;
        public short renderersCount;
        public bool lastLODIsBillboard;
        public LODFadeMode fadeMode;
        public Vector3 worldSpaceReferencePoint;
        public float worldSpaceSize;
        public readonly List<short> lodRenderersCount;
        public readonly List<float> lodScreenRelativeTransitionHeight;
        public readonly List<float> lodFadeTransitionWidth;

        private LODGroupItem(int lodCount)
        {
            renderersCount = 0;
            lastLODIsBillboard = false;
            fadeMode = LODFadeMode.None;
            worldSpaceReferencePoint = Vector3.zero;
            worldSpaceSize = 0;
            lodRenderersCount = new List<short>();
            lodScreenRelativeTransitionHeight = new List<float>();
            lodFadeTransitionWidth = new List<float>();
        }
        
        private static LODGroupItem s_ReuseElement = new LODGroupItem(k_MaxLODLevelsCount);

        public static LODGroupItem Create()
        {
            s_ReuseElement.renderersCount = 0;
            s_ReuseElement.lastLODIsBillboard = false;
            s_ReuseElement.fadeMode = LODFadeMode.None;
            s_ReuseElement.worldSpaceReferencePoint = Vector3.zero;
            s_ReuseElement.worldSpaceSize = 0;
            s_ReuseElement.lodRenderersCount.Clear();
            s_ReuseElement.lodScreenRelativeTransitionHeight.Clear();
            s_ReuseElement.lodFadeTransitionWidth.Clear();
            return s_ReuseElement;
        }
    }
    
    public struct RendererGroupItem
    {
        public int meshIndex;
        public GPUDrivenPackedRendererData packedRendererData;
        public int lodGroupID;
        public int gameObjectLayer;
        
        // material properties
        public readonly List<int> materialIndex;
        public readonly List<GPUDrivenPackedMaterialData> packedMaterialData;

        public Bounds localBounds;
        
        // support lightmap?
        public int lightmapIndex;
        public Vector4 lightmapScaleOffset;
        public uint renderingLayerMask;

        // submesh properties (for indirect draw? we do not support indirect draw now.)
        public short subMeshStartIndex;        // for static batch render?
        public readonly List<SubMeshDescriptor> subMeshDesc;
        
        // we only support implicit Instance Indices Mode Now.
        public Matrix4x4 localToWorldMatrix;
        public Matrix4x4 prevLocalToWorldMatrix;   // support motion vector?
        // public NativeArray<int> instancesOffset;
        // public NativeArray<int> instancesCount;
        // public NativeArray<int> rendererGroupIndex;

        // unsupported for editor scene culling mask
        // public NativeArray<GPUDrivenRendererEditorData> editorData;
        
        // unity6 property
        // int rendererPriority;
        // public NativeArray<int> materialFilterFlags;
        private RendererGroupItem(int materialsCount)
        {
            meshIndex = -1;
            packedRendererData = new GPUDrivenPackedRendererData();
            lodGroupID = -1;
            gameObjectLayer = 0;
            localBounds = new Bounds();
            lightmapIndex = -1;
            lightmapScaleOffset = Vector4.zero;
            renderingLayerMask = 0;
            subMeshStartIndex = 0;
            localToWorldMatrix = Matrix4x4.identity;
            prevLocalToWorldMatrix = Matrix4x4.identity;
            materialIndex = new List<int>();
            subMeshDesc = new List<SubMeshDescriptor>();
            packedMaterialData = new List<GPUDrivenPackedMaterialData>();
        }
        
        private static RendererGroupItem s_ReuseElement = new RendererGroupItem(0);

        public static RendererGroupItem Create()
        {
            s_ReuseElement.meshIndex = -1;
            s_ReuseElement.packedRendererData = new GPUDrivenPackedRendererData();
            s_ReuseElement.lodGroupID = -1;
            s_ReuseElement.gameObjectLayer = 0;
            s_ReuseElement.localBounds = new Bounds();
            s_ReuseElement.lightmapIndex = -1;
            s_ReuseElement.lightmapScaleOffset = Vector4.zero;
            s_ReuseElement.renderingLayerMask = 0;
            s_ReuseElement.subMeshStartIndex = 0;
            s_ReuseElement.localToWorldMatrix = Matrix4x4.identity;
            s_ReuseElement.prevLocalToWorldMatrix = Matrix4x4.identity;
            s_ReuseElement.materialIndex.Clear();
            s_ReuseElement.subMeshDesc.Clear();
            s_ReuseElement.packedMaterialData.Clear();
            return s_ReuseElement;
        }
    }
    
    // Do not add and remove in one frame.
    internal class SceneDataProcessor : IDisposable
    {
        private InstanceAllocator lodGroupInstanceAllocator;
        private InstanceAllocator rendererInstanceAllocator;

        private ParallelBitArray lodGroupItemsMask;
        private ParallelBitArray rendererGroupItemsMask;

        public LODGroupInputData lodGroupInputData;
        public RendererGroupInputData rendererInputData;

        public SceneDataProcessor()
        {
            lodGroupInstanceAllocator.Initialize();
            rendererInstanceAllocator.Initialize();
            
            lodGroupItemsMask = new ParallelBitArray(1024, Allocator.Persistent);
            rendererGroupItemsMask = new ParallelBitArray(1024, Allocator.Persistent);
            
            lodGroupInputData.Initialize(1024);
            rendererInputData.Initialize(1024);
        }
        
        public int RegisterLodGroup(ref LODGroupItem lodGroup)
        {
            int lodGroupID = lodGroupInstanceAllocator.AllocateInstance();
            if (lodGroupID >= lodGroupItemsMask.Length)
                lodGroupItemsMask.Resize(lodGroupItemsMask.Length * 2);
            lodGroupItemsMask.Set(lodGroupID, true);
            return lodGroupID;
        }

        public int RegisterRendererGroup(ref RendererGroupItem renderer)
        {
            int rendererGroupID = rendererInstanceAllocator.AllocateInstance();
            if (rendererGroupID >= rendererGroupItemsMask.Length)
                rendererGroupItemsMask.Resize(rendererGroupItemsMask.Length * 2);
            rendererGroupItemsMask.Set(rendererGroupID, true);
            return rendererGroupID;
        }

        public unsafe void UpdateLodGroup(int lodGroupID, ref LODGroupItem lodGroup)
        {
            Assert.IsTrue(lodGroupItemsMask.Length > lodGroupID && lodGroupItemsMask.Get(lodGroupID));
            lodGroupInputData.lodGroupID.Add(lodGroupID);
            lodGroupInputData.fadeMode.Add(lodGroup.fadeMode);
            lodGroupInputData.worldSpaceReferencePoint.Add(lodGroup.worldSpaceReferencePoint);
            lodGroupInputData.worldSpaceSize.Add(lodGroup.worldSpaceSize);
            lodGroupInputData.lastLODIsBillboard.Add(lodGroup.lastLODIsBillboard);
            int lodCount = lodGroup.lodRenderersCount.Count;
            int lodOffset = lodGroupInputData.renderersCount.Length;
            lodGroupInputData.lodCount.Add(lodCount);
            lodGroupInputData.lodOffset.Add(lodOffset);
            lodGroupInputData.renderersCount.Add(lodGroup.renderersCount);
            for (int i = 0; i < lodCount; ++i)
            {
                lodGroupInputData.lodRenderersCount.Add(lodGroup.lodRenderersCount[i]);
                lodGroupInputData.lodScreenRelativeTransitionHeight.Add(lodGroup.lodScreenRelativeTransitionHeight[i]);
                lodGroupInputData.lodFadeTransitionWidth.Add(lodGroup.lodFadeTransitionWidth[i]);
            }
        }
        
        public void UpdateRendererGroup(int rendererGroupID, ref RendererGroupItem renderer)
        {
            Assert.IsTrue(rendererGroupItemsMask.Length > rendererGroupID && rendererGroupItemsMask.Get(rendererGroupID));
            Assert.IsTrue(renderer.lodGroupID < 0 || lodGroupItemsMask.Get(renderer.lodGroupID));
            rendererInputData.rendererGroupID.Add(rendererGroupID);
            rendererInputData.gameObjectLayer.Add(renderer.gameObjectLayer);
            rendererInputData.localBounds.Add(renderer.localBounds);
            rendererInputData.lightmapIndex.Add(renderer.lightmapIndex);
            rendererInputData.lightmapScaleOffset.Add(renderer.lightmapScaleOffset);
            rendererInputData.renderingLayerMask.Add(renderer.renderingLayerMask);
            rendererInputData.localToWorldMatrix.Add(renderer.localToWorldMatrix);
            rendererInputData.prevLocalToWorldMatrix.Add(renderer.prevLocalToWorldMatrix);
            rendererInputData.meshIndex.Add(renderer.meshIndex);
            rendererInputData.packedRendererData.Add(renderer.packedRendererData);
            rendererInputData.subMeshStartIndex.Add(renderer.subMeshStartIndex);
            
            int materialsCount = renderer.subMeshDesc.Count;
            rendererInputData.materialsCount.Add((short)renderer.materialIndex.Count);
            rendererInputData.materialsOffset.Add(rendererInputData.materialIndex.Length);
            for (int i = 0; i < materialsCount; ++i)
            {
                rendererInputData.materialIndex.Add(renderer.materialIndex[i]);
                rendererInputData.packedMaterialData.Add(renderer.packedMaterialData[i]);
            }
            
            int subMeshCount = renderer.subMeshDesc.Count;
            rendererInputData.subMeshCount.Add((short)renderer.subMeshDesc.Count);
            rendererInputData.subMeshDescOffset.Add(rendererInputData.subMeshDesc.Length);
            for (int i = 0; i < subMeshCount; ++i)
                rendererInputData.subMeshDesc.Add(renderer.subMeshDesc[i]);
        }
        
        public void UnregisterLodGroup(int lodGroupID)
        {
            Assert.IsTrue(lodGroupItemsMask.Length > lodGroupID && lodGroupItemsMask.Get(lodGroupID));
            lodGroupInputData.invalidLODGroupID.Add(lodGroupID);
            lodGroupInstanceAllocator.FreeInstance(lodGroupID);
            lodGroupItemsMask.Set(lodGroupID, false);
        }
        
        public void UnregisterRendererGroup(int rendererGroupID)
        {
            Assert.IsTrue(rendererGroupItemsMask.Length > rendererGroupID && rendererGroupItemsMask.Get(rendererGroupID));
            rendererInputData.invalidRendererGroupID.Add(rendererGroupID);
            rendererInstanceAllocator.FreeInstance(rendererGroupID);
            rendererGroupItemsMask.Set(rendererGroupID, false);
        }

        public void Clear()
        {
            lodGroupInputData.Clear();
            rendererInputData.Clear();
        }
        
        public void Dispose()
        {
            lodGroupInstanceAllocator.Dispose();
            rendererInstanceAllocator.Dispose();
            lodGroupItemsMask.Dispose();
            rendererGroupItemsMask.Dispose();
            lodGroupInputData.Dispose();
            rendererInputData.Dispose();
        }
    }
}
