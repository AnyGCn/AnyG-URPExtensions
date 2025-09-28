using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BrgRenderSystem;
using UnityEngine.Rendering;

public class ResidentDrawTest : MonoBehaviour
{
    private ResidentDrawer instance;

    private Dictionary<LODGroup, int> lodGroupIDMap = new Dictionary<LODGroup, int>();

    private Dictionary<MeshRenderer, int> meshRendererIDMap = new Dictionary<MeshRenderer, int>();
    // Start is called before the first frame update
    
    void Start()
    {
        ResidentDrawer.ReinitializeIfNeeded();
        instance = ResidentDrawer.instance;
        
        LODGroup[] lodGroup = transform.GetComponentsInChildren<LODGroup>();
        for (var index = 0; index < lodGroup.Length; index++)
            AddLODGroup(lodGroup[index]);

        MeshRenderer[] meshRenderers = transform.GetComponentsInChildren<MeshRenderer>();
        for (var index = 0; index < meshRenderers.Length; index++)
            if (meshRenderers[index].enabled)
                AddMeshRenderer(meshRenderers[index], -1, 0);
    }

    // Update is called once per frame
    void Update()
    {

    }

    void AddLODGroup(LODGroup lodGroup)
    {
        if (lodGroupIDMap.ContainsKey(lodGroup))
            return;

        if (lodGroup.lodCount > 8 || lodGroup.lodCount == 0)
            return;

        int lodGroupId = lodGroupIDMap.Count;
        lodGroupIDMap.Add(lodGroup, lodGroupId);

        LODGroupItem item = new LODGroupItem(lodGroup.lodCount)
        {
            lodGroupID = lodGroupId,
            lastLODIsBillboard = false,
            fadeMode = lodGroup.fadeMode,
            worldSpaceSize = lodGroup.size,
            worldSpaceReferencePoint = lodGroup.transform.position
        };
        int renderersCount = 0;
        var ds = lodGroup.GetLODs();
        for (var index = 0; index < ds.Length; index++)
        {
            var lod = ds[index];
            int lodRenderersCount = 0;
            foreach (var render in lod.renderers)
            {
                if (render is not MeshRenderer meshRenderer)
                    continue;

                if (AddMeshRenderer(meshRenderer, lodGroupId, (byte)(1 << index)))
                {
                    meshRenderer.enabled = false;
                    lodRenderersCount++;
                }
            }

            item.SetLodRenderersCount(index, lodRenderersCount);
            item.SetFadeTransitionWidth(index, lod.fadeTransitionWidth);
            item.SetScreenRelativeTransitionHeight(index, lod.screenRelativeTransitionHeight);
            renderersCount += lodRenderersCount;
        }

        item.renderersCount = (short)renderersCount;
        instance.RegisterLodGroup(ref item);
    }

    bool AddMeshRenderer(MeshRenderer meshRenderer, int lodGroupId, byte lodMask)
    {
        if (meshRendererIDMap.ContainsKey(meshRenderer))
        {
            Debug.LogWarning("MeshRenderer has already been added", meshRenderer);
            return false;
        }
        
        if (!meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return false;
        
        Mesh mesh = meshFilter.sharedMesh;
        Material[] materials = meshRenderer.sharedMaterials;
        if (mesh.subMeshCount != materials.Length)
            return false;

        RendererGroupItem item = new RendererGroupItem(materials.Length)
        {
            lodGroupID = lodGroupId,
            localBounds = meshRenderer.localBounds,
            mesh = mesh,
            gameObjectLayer = (short)meshRenderer.gameObject.layer,
            renderingLayerMask = meshRenderer.renderingLayerMask,
            // lightmapIndex = meshRenderer.lightmapIndex,
            // lightmapScaleOffset = meshRenderer.lightmapScaleOffset,
            packedRendererData = new GPUDrivenPackedRendererData(meshRenderer.receiveShadows,
                meshRenderer.staticShadowCaster, lodMask, meshRenderer.shadowCastingMode, LightProbeUsage.Off,
                MotionVectorGenerationMode.ForceNoMotion, false, false, false, false, false),
            localToWorldMatrix = meshRenderer.localToWorldMatrix,
            // item.prevLocalToWorldMatrix = meshRenderer.localToWorldMatrix,
        };
        
        for (var index = 0; index < materials.Length; index++)
            item.SetMaterial(index, materials[index]);
        
        int rendererGroupId = meshRendererIDMap.Count;
        item.rendererGroupID = rendererGroupId;
        meshRendererIDMap.Add(meshRenderer, rendererGroupId);
        instance.RegisterRendererGroup(ref item);
        return true;
    }

    void OnDestroy()
    {
        ResidentDrawer.CleanUp();
    }
}
