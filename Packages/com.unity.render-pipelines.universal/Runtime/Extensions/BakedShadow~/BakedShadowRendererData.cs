namespace UnityEngine.Rendering.Universal.Extensions.BakedShadowMap
{
    using System;

    [Serializable, ReloadGroup, ExcludeFromPreset, CreateAssetMenu(fileName = "BakedShadowRenderer.asset", menuName = "Rendering/Cached Shadow Renderer", order = 1000000)]
    public class BakedShadowRendererData : ScriptableRendererData
    {
        protected override ScriptableRenderer Create()
        {
            return new BakedShadowRenderer(this);
        }
    }
}
