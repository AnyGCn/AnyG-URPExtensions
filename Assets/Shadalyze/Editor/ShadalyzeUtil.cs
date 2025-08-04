using System.Collections.Generic;
using Shadalyze.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace Shadalyze.Editor
{
    public static class ShadalyzeUtil
    {
        [MenuItem("Assets/Shadalyze/Compile Shader", false)]
        private static void CompileShaderVariantMenuCommand()
        {
            if (Selection.activeObject is Shader shader)
            { 
                EditShaderVariantWindow.Show(new EditShaderVariantWindow.PopupData
                {
                    shader = shader,
                    collection = new ShaderVariantCollection()
                });
            }
            else if (Selection.activeObject is Material material)
            {
                shader = material.shader;
                if (shader == null)
                {
                    Debug.LogError("Material has no shader assigned.");
                    return;
                }

                ShaderCompileData.GetShaderCompileData(new ShaderVariantCollection.ShaderVariant(shader,
                    UnityEngine.Rendering.PassType.ScriptableRenderPipeline,
                    material.shaderKeywords), new List<ShaderCompileData>());
            }
            else if (Selection.activeObject is ShaderVariantCollection svc)
            {
                ShaderCompileData.GetShaderCompileData(svc, new List<ShaderCompileData>());
            }
            else
            {
                Debug.LogError("Please select a Shader or Material to compile.");
                return;
            }
        }
        
        [MenuItem("Assets/Shadalyze/Compile Shader", true)]
        private static bool ValidateCompileShaderVariantMenuCommand()
        {
            return Selection.activeObject as Shader || Selection.activeObject as Material || Selection.activeObject as ShaderVariantCollection;
        }
    }
}
