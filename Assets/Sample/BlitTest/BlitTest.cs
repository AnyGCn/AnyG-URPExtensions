using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BlitTest : MonoBehaviour
{
    private CommandBuffer m_Cmd;
    private Renderer m_Renderer;
    public Texture blitTexture;
    public RenderTexture testAtlas;
    public RenderTexture testArray;
    public bool loadAction { get; set; } = true;
    public BlitType blitType;
    
    public enum BlitType
    {
        CopyToAtlas,
        CopyToArray,
        BlitToAtlas,
        BlitToArray,
        BlitToTexture,
    }
    
    public void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
        m_Cmd = new CommandBuffer();
        m_Cmd.name = "BlitTest";
        Application.targetFrameRate = 30;
    }
    
    public void CopyToAtlas()
    {
        blitType = BlitType.CopyToAtlas;
        m_Renderer.sharedMaterial.SetFloat("_UseTextureArrayToggle", 0);
        m_Renderer.sharedMaterial.DisableKeyword("_USE_TEXTURE_ARRAY");
        m_Cmd.Clear();
        m_Cmd.CopyTexture(blitTexture, 0, 0, 0, 0, blitTexture.width, blitTexture.height, testAtlas, 0, 0, 0, 0);
    }

    public void CopyToArray()
    {
        blitType = BlitType.CopyToArray;
        m_Renderer.sharedMaterial.SetFloat("_UseTextureArrayToggle", 1);
        m_Renderer.sharedMaterial.EnableKeyword("_USE_TEXTURE_ARRAY");
        m_Cmd.Clear();
        m_Cmd.CopyTexture(blitTexture, 0, 0, 0, 0, blitTexture.width, blitTexture.height, testArray, 0, 0, 0, 0);
    }

    public void BlitToTexture()
    {
        blitType = BlitType.BlitToTexture;
        m_Renderer.sharedMaterial.SetFloat("_UseTextureArrayToggle", 0);
        m_Renderer.sharedMaterial.DisableKeyword("_USE_TEXTURE_ARRAY");
        m_Cmd.Clear();
        m_Cmd.SetRenderTarget(new RenderTargetIdentifier(testAtlas, 0, CubemapFace.Unknown, 0),
            loadAction ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);
        Blitter.BlitQuad(m_Cmd, blitTexture, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0), 0, false);
    }
    
    public void BlitToAtlas()
    {
        blitType = BlitType.BlitToAtlas;
        m_Renderer.sharedMaterial.SetFloat("_UseTextureArrayToggle", 0);
        m_Renderer.sharedMaterial.DisableKeyword("_USE_TEXTURE_ARRAY");
        m_Cmd.Clear();
        Rect viewportRect = new Rect(0, 0, blitTexture.width, blitTexture.height);
        m_Cmd.SetRenderTarget(new RenderTargetIdentifier(testAtlas, 0, CubemapFace.Unknown, 0),
            loadAction ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);
        m_Cmd.SetViewport(viewportRect);
        m_Cmd.EnableScissorRect(viewportRect);
        Blitter.BlitQuad(m_Cmd, blitTexture, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0), 0, false);
        m_Cmd.DisableScissorRect();
    }

    public void BlitToArray()
    {
        blitType = BlitType.BlitToArray;
        m_Renderer.sharedMaterial.SetFloat("_UseTextureArrayToggle", 1);
        m_Renderer.sharedMaterial.EnableKeyword("_USE_TEXTURE_ARRAY");
        m_Cmd.Clear();
        m_Cmd.SetRenderTarget(new RenderTargetIdentifier(testArray, 0, CubemapFace.Unknown, 0),
            loadAction ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);
        Blitter.BlitQuad(m_Cmd, blitTexture, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0), 0, false);
    }
    
    public void Update()
    {
        switch (blitType)
        {
            case BlitType.CopyToAtlas:
                CopyToAtlas();
                break;
            case BlitType.CopyToArray:
                CopyToArray();
                break;
            case BlitType.BlitToAtlas:
                BlitToAtlas();
                break;
            case BlitType.BlitToArray:
                BlitToArray();
                break;
            case BlitType.BlitToTexture:
                BlitToTexture();
                break;
        }

        Graphics.ExecuteCommandBuffer(m_Cmd);
    }
    
    public void OnDestroy()
    {
        m_Cmd.Dispose();
    }
}
