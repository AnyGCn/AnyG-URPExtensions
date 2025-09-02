using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CachedShadowCasterPass : ScriptableRenderPass
{
    /// <summary>
    /// Creates a new <c>MainLightShadowCasterPass</c> instance.
    /// </summary>
    /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
    /// <seealso cref="RenderPassEvent"/>
    public CachedShadowCasterPass(RenderPassEvent evt)
    {
        base.profilingSampler = new ProfilingSampler(nameof(CachedShadowCasterPass));
        renderPassEvent = evt;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {

    }
}
