using System;
using System.Diagnostics;

namespace Shadalyze.Editor.Wrapper
{
    /// <summary>
    /// Wrapper for Mali Offline Compiler.
    /// </summary>
    public static class MaliOfflineCompilerWrapper
    {
        public static bool Analyze(string fileName, out string output, out string errors)
        {
            using Process p = new Process();
            p.StartInfo = new ProcessStartInfo(ShadalyzeGlobalSettings.Instance.MaliocExePath, fileName)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.ASCII,
            };
            
            p.Start();
            p.WaitForExit();
            output = p.StandardOutput.ReadToEnd().Trim();
            errors = p.StandardError.ReadToEnd().Trim();
            return String.IsNullOrEmpty(errors);
        }
    }
}
