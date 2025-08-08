using System;
using System.Collections.Generic;
using System.IO;
using Shadalyze.Editor.Wrapper;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Shadalyze.Editor
{
    internal enum ShaderAnalysisLevel
    {
        Disabled,
        OnlySRPShaders,
        All
    }
    
    internal class ShadalyzeGlobalSettings : ScriptableObject
    {
        public static readonly string ProjectPath = Path.GetDirectoryName(Application.dataPath);
        public static readonly string CompileCodePath = $"{ProjectPath}/Temp/Shadalyze/CompiledCode/";
        public static readonly string AnalyzeResultPath = $"{ProjectPath}/Temp/Shadalyze/AnalyzeResult/";
        private static string PackageRelativePath;
        public static string SettingsPath { get; private set; }
        public static string DefaultMaliocExePath  { get; private set; }
        
        private static ShadalyzeGlobalSettings _instance = null;
        
        public static ShadalyzeGlobalSettings Instance
        {
            get
            {
                if (!_instance)
                {
                    PackageRelativePath = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"t:Script {nameof(ShadalyzeUtil)}")[0]).Replace('/', '\\'));
                    SettingsPath = $"{PackageRelativePath}/{nameof(ShadalyzeGlobalSettings)}.asset";
                    DefaultMaliocExePath = $"{ProjectPath}\\{PackageRelativePath}\\mali_offline_compiler~\\malioc.exe";
                    _instance = AssetDatabase.LoadAssetAtPath<ShadalyzeGlobalSettings>(SettingsPath);
                    Directory.CreateDirectory(CompileCodePath);
                    Directory.CreateDirectory(AnalyzeResultPath);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<ShadalyzeGlobalSettings>();
                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                    }
                    
                    _instance.Init();
                }
                
                return _instance;
            }
        }

        public static void Initialize()
        {
            var instance = ShadalyzeGlobalSettings.Instance;
            instance.Init();
        }
        
        private void Init()
        {
            lightModeWhiteList = new HashSet<ShaderTagId>();
            lightModeWhiteList.Add(new ShaderTagId("Always"));
            foreach (var lightMode in analysisLightMode)
            {
                lightModeWhiteList.Add(new ShaderTagId(lightMode));
            }
        }
        
        public MaliDeviceType BaselineDevice => baselineDevice;
        
        public ShaderAnalysisLevel ShaderAnalysisLevel => shaderAnalysisLevel;
        
        public string MaliocExePath => String.IsNullOrEmpty(customMaliocExePath) ? DefaultMaliocExePath : customMaliocExePath;

        internal HashSet<ShaderTagId> lightModeWhiteList;
        
        [FormerlySerializedAs("analysisDevice")]
        [SerializeField]
        [Tooltip("Controls which shaders will be analyzed in shader build process.")]
        private MaliDeviceType baselineDevice = MaliDeviceType.Immortalis_G715;
        
        [SerializeField]
        [Tooltip("Controls which shaders will be analyzed in shader build process.")]
        private ShaderAnalysisLevel shaderAnalysisLevel = ShaderAnalysisLevel.Disabled;
        
        [SerializeField]
        [Tooltip("If not empty, use the malioc exe of this path to compile shader.")]
        private string customMaliocExePath = String.Empty;

        [SerializeField]
        [Tooltip("Choose the light mode to analyze. (The pass without light mode tag (assumed that light mode is always) would be always included)")]
        private List<string> analysisLightMode = new List<string>() { "UniversalForward", "UniversalGBuffer" };
    }
}