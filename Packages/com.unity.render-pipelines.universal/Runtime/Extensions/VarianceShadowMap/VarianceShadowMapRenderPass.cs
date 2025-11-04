using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    internal class VarianceShadowMapRenderPass
    {
        private Material m_Material;
        private RTHandle m_VarianceShadowMap;

        public VarianceShadowMapRenderPass(Material copyColorMaterial = null)
        {
            m_Material = copyColorMaterial;
        }
        
        public bool Setup(int renderTargetWidth, int renderTargetHeight)
        {
            var descriptor = new RenderTextureDescriptor(renderTargetWidth, renderTargetHeight, RenderTextureFormat.RGFloat)
            {
                useMipMap = false,
                autoGenerateMips = false,
            };
            
            RenderingUtils.ReAllocateIfNeeded(ref m_VarianceShadowMap, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp);
            return true;
        }

        public void Render(CommandBuffer cmd, RTHandle mainlightShadowmap, int cascadeCount, ShadowSliceData[] shadowSlices)
        {
            cmd.SetRenderTarget(m_VarianceShadowMap);
            Vector2 rtSize = new Vector2(m_VarianceShadowMap.rt.width, m_VarianceShadowMap.rt.height);
            for (int i = 0; i < cascadeCount; ++i)
            {
                var shadowSliceData = shadowSlices[i];
                Vector2 pos = new Vector2(shadowSliceData.offsetX, shadowSliceData.offsetY);
                Vector2 size = new Vector2(shadowSliceData.resolution, shadowSliceData.resolution);
                cmd.SetViewport(new Rect(pos, size));
                pos /= rtSize;
                size /= rtSize;
                cmd.SetGlobalVector("_BlitTexture_ST", new Vector4(rtSize.x, rtSize.y, 1.0f / rtSize.x, 1.0f / rtSize.y));
                Blitter.BlitTexture(cmd, mainlightShadowmap.nameID, new Vector4(size.x, size.y, pos.x, pos.y), m_Material, 25);
            }
            
            cmd.SetGlobalTexture("_VarianceShadowmapTexture", m_VarianceShadowMap.nameID);
        }
        
        public void Dispose()
        {
            m_VarianceShadowMap?.Release();
        }
    }
}
