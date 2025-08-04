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
    public struct ShaderCompileData
    {
        public readonly Shader ShaderObject;
        public readonly int SubshaderIndex;
        public readonly int PassIndex;
        public readonly string[] ShaderKeywords;
        public string PassName;
        private string m_SHA256;
        private string m_VertCode;
        private string m_FragCode;

        public string sha256 => m_SHA256;
        public string vertCode => m_VertCode;
        public string fragCode => m_FragCode;
        
        public ShaderCompileData(Shader shaderObject, int subshaderIndex, int passIndex, string[] shaderKeywords)
        {
            ShaderObject = shaderObject;
            SubshaderIndex = subshaderIndex;
            PassIndex = passIndex;
            ShaderKeywords = shaderKeywords;
            PassName = null;
            m_SHA256 = null;
            m_VertCode = null;
            m_FragCode = null;
            TryGetCompiledCode();
        }

        public ShaderCompileData(Shader shaderObject, ShaderSnippetData snippet, ShaderCompilerData variant) : this(
            shaderObject, (int)snippet.pass.SubshaderIndex, (int)snippet.pass.PassIndex,
            variant.shaderKeywordSet.GetShaderKeywords().Select(sk => sk.name).ToArray())
        {

        }
        
        public static void GetShaderCompileData(ShaderVariantCollection.ShaderVariant variant, [NotNull] List<ShaderCompileData> dataList)
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
                    if (pass == null)
                        continue;
                    dataList.Add(new ShaderCompileData(shader, subshaderIndex, passIndex, variant.keywords));
                }
            }
        }
        
        public static void GetShaderCompileData(List<ShaderVariantCollection.ShaderVariant> variant, [NotNull] List<ShaderCompileData> dataList)
        {
            foreach (var shaderVariant in variant)
            {
                GetShaderCompileData(shaderVariant, dataList);
            }
        }
        
        public static void GetShaderCompileData(ShaderVariantCollection collection, [NotNull] List<ShaderCompileData> dataList)
        {
            GetShaderCompileData(ShaderUtilWrapper.GetShaderVariantsFromCollections(collection), dataList);
        }
        
        public bool TryGetCompiledCode()
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
            
            PassName = pass.Name;
            BuiltinShaderDefine[] keywordsForBuildTarget = ShaderUtil.GetShaderPlatformKeywordsForBuildTarget(ShaderCompilerPlatform.GLES3x, BuildTarget.Android, GraphicsTier.Tier3);
            var variantCompileInfo = pass.CompileVariant(ShaderType.Vertex, ShaderKeywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android, keywordsForBuildTarget);
            if (!variantCompileInfo.Success)
            {
                Debug.LogError("Failed to compile shader variant");

                foreach (var message in variantCompileInfo.Messages)
                    Debug.LogError($"{message.severity}: {message.message}. {message.messageDetails}");

                return false;
            }
            
            m_SHA256 = ShaderCompileDataManager.GetSHA256(variantCompileInfo.ShaderData);
            if (ShaderCompileDataManager.IsShaderCompileCodeInCache(sha256)) return true;
            bool success = UnityShaderCompileDataParser.ParseShader(variantCompileInfo.ShaderData, out m_VertCode, out m_FragCode);
            if (success)
                ShaderCompileDataManager.DumpShaderCompileCodeToCache(sha256, vertCode, fragCode);
            
            return success;
        }
    }
}
