using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shadalyze.Editor.Manager;
using Shadalyze.Editor.Parser;
using Shadalyze.Editor.Wrapper;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shadalyze.Editor.Data
{
    public struct ShaderCompileRequest
    {
        public readonly Shader ShaderObject;
        public readonly int SubshaderIndex;
        public readonly int PassIndex;
        public readonly string[] ShaderKeywords;
        public string PassName;
        private string m_SHA256;
        private string m_VertCode;
        private string m_FragCode;

        public ShaderCompileRequest(Shader shaderObject, int subshaderIndex, int passIndex, string passName, string[] shaderKeywords)
        {
            ShaderObject = shaderObject;
            SubshaderIndex = subshaderIndex;
            PassIndex = passIndex;
            ShaderKeywords = shaderKeywords;
            PassName = passName;
            m_SHA256 = null;
            m_VertCode = null;
            m_FragCode = null;
        }

        public ShaderCompileRequest(Shader shaderObject, ShaderSnippetData snippet, ShaderCompilerData variant) : this(
            shaderObject, (int)snippet.pass.SubshaderIndex, (int)snippet.pass.PassIndex, snippet.passName,
            variant.shaderKeywordSet.GetShaderKeywords().Select(sk => sk.name).ToArray())
        {

        }

        private static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");
        
        public static void GetShaderCompileData(ShaderVariantCollection.ShaderVariant variant, [NotNull] List<ShaderCompileRequest> dataList)
        {
            Shader shader = variant.shader;
            var shaderData = ShaderUtil.GetShaderData(shader);
            for (int subshaderIndex = 0; subshaderIndex < shaderData.SubshaderCount; ++subshaderIndex)
            {
                var subshader = shaderData.GetSubshader(subshaderIndex);
                if (subshader == null)
                    continue;
                for(int passIndex = 0; passIndex < subshader.PassCount; ++passIndex)
                {
                    var pass = subshader.GetPass(passIndex);
                    if (pass == null || !ShadalyzeGlobalSettings.Instance.lightModeWhiteList.Contains(pass.FindTagValue(LightMode)))
                        continue;
                    dataList.Add(new ShaderCompileRequest(shader, subshaderIndex, passIndex, pass.Name, variant.keywords));
                }
            }
        }
        
        public static void GetShaderCompileData(List<ShaderVariantCollection.ShaderVariant> variant, [NotNull] List<ShaderCompileRequest> dataList)
        {
            foreach (var shaderVariant in variant)
            {
                GetShaderCompileData(shaderVariant, dataList);
            }
        }
        
        public static void GetShaderCompileData(ShaderVariantCollection collection, [NotNull] List<ShaderCompileRequest> dataList)
        {
            GetShaderCompileData(ShaderUtilWrapper.GetShaderVariantsFromCollections(collection), dataList);
        }
        
        /// <summary>
        /// Compile the shader of GLES3x and dump file to disk for analysis.
        /// </summary>
        /// <returns> success flag. </returns>
        public bool Compile()
        {
            if (!string.IsNullOrEmpty(m_VertCode) && !string.IsNullOrEmpty(m_FragCode))
                return true;
            
            var shaderData = ShaderUtil.GetShaderData(ShaderObject);
            var subshader = shaderData.GetSubshader(SubshaderIndex);
            var pass = subshader.GetPass(PassIndex);
            if (pass == null)
            {
                Debug.Log($"Failed to get Shader {ShaderObject.name} subshader {SubshaderIndex} pass {PassIndex}");
                return false;
            }
            
            BuiltinShaderDefine[] keywordsForBuildTarget = ShaderUtil.GetShaderPlatformKeywordsForBuildTarget(ShaderCompilerPlatform.GLES3x, BuildTarget.Android, GraphicsTier.Tier3);
            var variantCompileInfo = pass.CompileVariant(ShaderType.Vertex, ShaderKeywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android, keywordsForBuildTarget);
            if (!variantCompileInfo.Success)
            {
                Debug.LogError("Failed to compile shader variant");

                foreach (var message in variantCompileInfo.Messages)
                    Debug.LogError($"{message.severity}: {message.message}. {message.messageDetails}");

                return false;
            }
            
            // TODO: There is no need to compute sha256 here properly, the most cost function is compiling variant but there are no good way to deduplication before compiling it.
            m_SHA256 = ShaderCompileDataManager.GetSHA256(variantCompileInfo.ShaderData);
            if (ShaderCompileDataManager.IsShaderCompileCodeInCache(m_SHA256)) return true;
            bool success = UnityShaderCompileDataParser.ParseShader(variantCompileInfo.ShaderData, out var vertCode, out var fragCode);
            if (success)
            {
                string path = $"{ShadalyzeGlobalSettings.CompileCodePath}/{m_SHA256}";
                ShaderCompileDataManager.DumpToFile(path + ".vert", vertCode, vertCode.Length * sizeof(char));
                ShaderCompileDataManager.DumpToFile(path + ".frag", fragCode, fragCode.Length * sizeof(char));
            }
            
            m_VertCode = vertCode;
            m_FragCode = fragCode;
            return success;
        }

        /// <summary>
        /// Get the analysis report file path of the shader.
        /// </summary>
        /// <returns> file path of the analysis report, return null if analysis is failed. </returns>
        public string Analyze()
        {
            if (MaliOfflineCompilerWrapper.Analyze($"{ShadalyzeGlobalSettings.CompileCodePath}/{m_SHA256}.vert",
                    out var vertexAnalysisReport, out var _) &&
                MaliOfflineCompilerWrapper.Analyze($"{ShadalyzeGlobalSettings.CompileCodePath}/{m_SHA256}.frag",
                    out var fragAnalysisReport, out var _))
            {
                string result = default;

                result += $"Shader: {ShaderObject.name}\n";

                result += $"PassName: {PassName}\n";
                
                result += $"Keywords: {String.Join(' ', ShaderKeywords)}\n\n";
                
                result += "///////////////////////////////////////\n" +
                          "//////// Vertex Analysis Report ///////\n" +
                          "///////////////////////////////////////\n";
                
                result += vertexAnalysisReport;
                
                result += "\n\n";
                
                result += "///////////////////////////////////////\n" +
                          "/////// Fragment Analysis Report //////\n" +
                          "///////////////////////////////////////\n";
                
                result += fragAnalysisReport;
                
                result += "\n\n";
                
                string filePath = $"{ShadalyzeGlobalSettings.CompileCodePath}/{ShaderObject.name.Replace('/', '-')}-{PassName}-{m_SHA256}.txt";
                ShaderCompileDataManager.DumpToFile(filePath, result, result.Length);
                return filePath;
            }
            
            return null;
        }
    }
}
