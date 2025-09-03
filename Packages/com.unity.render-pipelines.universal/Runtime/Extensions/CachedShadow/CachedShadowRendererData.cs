namespace UnityEngine.Rendering.Universal.Extensions.CachedShadowMap
{
    using System;

    [Serializable, ReloadGroup, ExcludeFromPreset, CreateAssetMenu(fileName = "CachedShadowRenderer.asset", menuName = "Rendering/Cached Shadow Renderer", order = 1000000)]
    public class CachedShadowRendererData : ScriptableRendererData
    {
        protected override ScriptableRenderer Create()
        {
            return new CachedShadowRenderer(this);
        }
    }
}
