using System.Linq;
using Shadalyze.Editor.Parser;
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
        public readonly GraphicsTier Tier;
        public readonly string[] ShaderKeywords;
        public string PassName;
        private string m_VertCode;
        private string m_FragCode;

        public string vertexCode => m_VertCode;
        public string fragmentCode => m_FragCode;
        
        public ShaderCompileData(Shader shaderObject, int subshaderIndex, int passIndex, GraphicsTier tier, string[] shaderKeywords)
        {
            ShaderObject = shaderObject;
            SubshaderIndex = subshaderIndex;
            PassIndex = passIndex;
            Tier = tier;
            ShaderKeywords = shaderKeywords;
            PassName = null;
            m_VertCode = null;
            m_FragCode = null;
        }
        
        public ShaderCompileData(Shader shaderObject, ShaderSnippetData snippet, ShaderCompilerData variant) : this(
            shaderObject, (int)snippet.pass.SubshaderIndex, (int)snippet.pass.PassIndex,
            variant.graphicsTier, variant.shaderKeywordSet.GetShaderKeywords().Select(sk => sk.name).ToArray())
        {
            
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

            var variantCompileInfo = pass.CompileVariant(ShaderType.Vertex, ShaderKeywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android, Tier);
            if (!variantCompileInfo.Success)
            {
                Debug.LogError("Failed to compile shader variant");

                foreach (var message in variantCompileInfo.Messages)
                    Debug.LogError($"{message.severity}: {message.message}. {message.messageDetails}");

                return false;
            }
            
            return ShaderCompileDataParser.ParseShader(variantCompileInfo.ShaderData, out m_VertCode, out m_FragCode);
        }
    }
}
