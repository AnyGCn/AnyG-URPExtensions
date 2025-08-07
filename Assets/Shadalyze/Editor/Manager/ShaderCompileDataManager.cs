using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shadalyze.Editor.Data;
using UnityEngine;

namespace Shadalyze.Editor.Manager
{
    /// <summary>
    /// Cache compiled and analyzed shader data
    /// </summary>
    public static class ShaderCompileDataManager
    {
        private static SHA256 encoderSHA256 = SHA256.Create();
        
        private static void DumpToFile(string fileName, string content, int bufferSize)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            using var fileStream = new FileStream($"{fileName}", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
            using StreamWriter writer = new StreamWriter(fileStream);
            writer.Write(content);
        }

        internal static string GetSHA256(string data)
        {
            return GetSHA256(Encoding.UTF8.GetBytes(data));
        }
        
        internal static string GetSHA256(byte[] data)
        {
            return BitConverter.ToString(encoderSHA256.ComputeHash(data)).Replace("-","");
        }

        internal static bool IsShaderCompileCodeInCache(string sha256)
        {
            string fileName = $"{ShadalyzeGlobalSettings.CompileCodePath}/{sha256}";
            return File.Exists(fileName + ".vert") && File.Exists(fileName + ".frag");
        }
        
        internal static void DumpShaderCompileCodeToCache(string sha256, string vertCode, string fragCode)
        {
            string fileName = $"{ShadalyzeGlobalSettings.CompileCodePath}/{sha256}";
            DumpToFile(fileName + ".vert", vertCode, vertCode.Length * sizeof(char));
            DumpToFile(fileName + ".frag", fragCode, fragCode.Length * sizeof(char));
        }
    }
}
