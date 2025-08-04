using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shadalyze.Editor
{
    public static class GlobalConstant
    {
        public static string TempPath = $"{Path.GetDirectoryName(Application.dataPath)}/Temp/Shadalyze/";
    }
}
