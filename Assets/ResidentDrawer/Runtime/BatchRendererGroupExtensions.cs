using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ResidentDrawer
{
    internal static class BatchRendererGroupExtensions
    {
        internal static unsafe void RegisterMeshes(this BatchRendererGroup brg, ReadOnlySpan<int> meshID, Span<BatchMeshID> batchMeshID)
        {
            for (var index = 0; index < meshID.Length; index++)
            {
                var id = meshID[index];
                batchMeshID[index] = brg.RegisterMesh(id);
            }
        }
        
        internal static unsafe void RegisterMaterials(this BatchRendererGroup brg, ReadOnlySpan<int> materialID, Span<BatchMaterialID> batchMaterialID)
        {
            for (var index = 0; index < materialID.Length; index++)
            {
                var id = materialID[index];
                batchMaterialID[index] = brg.RegisterMaterial(id);
            }
        }
    }
}
