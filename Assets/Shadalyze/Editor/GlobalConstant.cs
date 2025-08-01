using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shadalyze.Editor
{
    public static class GlobalConstant
    {
        public static string TempPath = $"{Path.GetDirectoryName(Application.dataPath)}/Temp/Shadalyze/";

        [MenuItem("Shadalyze/Test String")]
        public static void TestString()
        {
            Debug.Log(TempPath);
            Directory.CreateDirectory(TempPath);
        }
    }
}
