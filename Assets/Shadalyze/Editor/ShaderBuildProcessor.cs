using System.Collections.Generic;
using Shadalyze.Editor.Data;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;

namespace Shadalyze.Editor
{
    public class ShaderBuildProcessor : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public static List<ShaderCompileRequest> shaderCompilerDataList = new List<ShaderCompileRequest>();
        
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (ShadalyzeGlobalSettings.Instance.ShaderAnalysisLevel == ShaderAnalysisLevel.Disabled) return;
            ShadalyzeGlobalSettings.Initialize();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (ShadalyzeGlobalSettings.Instance.ShaderAnalysisLevel == ShaderAnalysisLevel.Disabled) return;
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> dataList)
        {
            if (ShadalyzeGlobalSettings.Instance.ShaderAnalysisLevel == ShaderAnalysisLevel.Disabled) return;
        }
    }
}
