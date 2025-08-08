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
            ShadalyzeGlobalSettings.Initialize();
            if (Selection.activeObject is Shader shader)
            {
                EditShaderVariantWindow.Show(shader, null);
            }
            else if (Selection.activeObject is Material material)
            {
                shader = material.shader;
                if (shader != null)
                    EditShaderVariantWindow.Show(shader, material.shaderKeywords);
            }
            else if (Selection.activeObject is ShaderVariantCollection svc)
            {
                var compileRequests = new List<ShaderCompileRequest>();
                ShaderCompileRequest.GetShaderCompileData(svc, compileRequests);
                foreach (var compileRequest in compileRequests)
                {
                    compileRequest.Compile();
                    string reportPath = compileRequest.Analyze();
                    if (!string.IsNullOrEmpty(reportPath))
                        Application.OpenURL("file://" + reportPath);
                }
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
