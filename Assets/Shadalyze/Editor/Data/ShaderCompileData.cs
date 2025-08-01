using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        public readonly string[] ShaderKeywords;
        public string PassName;
        private string m_SHA256;
        private string m_VertCode;
        private string m_FragCode;

        public string vertexCode => m_VertCode;
        public string fragmentCode => m_FragCode;
        
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

            var variantCompileInfo = pass.CompileVariant(ShaderType.Vertex, ShaderKeywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android);
            if (!variantCompileInfo.Success)
            {
                Debug.LogError("Failed to compile shader variant");

                foreach (var message in variantCompileInfo.Messages)
                    Debug.LogError($"{message.severity}: {message.message}. {message.messageDetails}");

                return false;
            }
            
            bool success = ShaderCompileDataParser.ParseShader(variantCompileInfo.ShaderData, out m_VertCode, out m_FragCode);
            if (success)
            {
                using (var sha256 = SHA256.Create())
                {
                    m_SHA256 = BitConverter.ToString(sha256.ComputeHash(variantCompileInfo.ShaderData));
                } 
            }
            
            return success;
        }
        
        // 持久化数据
        public void SaveData()
        {
            string directoryPath = $"{GlobalConstant.TempPath}/{ShaderObject.name.Replace('/', '-')}-sub{SubshaderIndex}-pass{PassIndex}-{PassName}";
            Directory.CreateDirectory(directoryPath);
            FileStream vertStream = File.Create($"{directoryPath}/{m_SHA256}.vert", m_VertCode.Length * sizeof(char));
            using (StreamWriter writer = new StreamWriter(vertStream))
            {
                writer.Write(m_VertCode);
            }
            vertStream.Close();
            FileStream fragStream = File.Create($"{directoryPath}/{m_SHA256}.frag", m_FragCode.Length * sizeof(char));
            using (StreamWriter writer = new StreamWriter(fragStream))
            {
                writer.Write(m_FragCode);
            }
            fragStream.Close();
        }
    }
}
