using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shadalyze.Editor
{
    public static class GlobalVariable
    {
        public static readonly string ProjectPath = Path.GetDirectoryName(Application.dataPath);
        public static readonly string PackageRelativePath = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"t:Script {nameof(ShadalyzeUtil)}")[0]).Replace('/', '\\'));
        public static readonly string DefaultMaliocExePath = $"{ProjectPath}\\{PackageRelativePath}\\mali_offline_compiler~\\malioc.exe";
        public static readonly string CompileCodePath = $"{ProjectPath}/Temp/Shadalyze/CompiledCode/";
        public static readonly string AnalyzeJsonPath = $"{ProjectPath}/Temp/Shadalyze/AnalyzeResult/";
        
        public static string CustomMaliocExePath = null;
        
        [MenuItem("Shadalyze/Test String")]
        public static void TestString()
        {
            Debug.Log(DefaultMaliocExePath);
        }
    }
}
