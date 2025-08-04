using System;
using System.Diagnostics;

namespace Shadalyze.Editor.Wrapper
{
    /// <summary>
    /// Wrapper for Mali Offline Compiler.
    /// </summary>
    public static class MaliOfflineCompilerWrapper
    {
        public static void Compile(string fileName)
        {
            bool runShell = true;
            ProcessStartInfo ps = new ProcessStartInfo(GlobalVariable.DefaultMaliocExePath, fileName)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true,
            };
            
            using (Process p = new Process())
            {
                ps.UseShellExecute = runShell;
                if (!runShell)
                {
                    ps.RedirectStandardOutput = true;
                    ps.RedirectStandardError = true;
                    ps.StandardOutputEncoding = System.Text.Encoding.ASCII;
                }
                p.StartInfo = ps;
                p.Start();
                p.WaitForExit();
                if (!runShell)
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(output))
                    {
                        UnityEngine.Debug.Log(string.Format("{0} Output: {1}", DateTime.Now, output));
                    }

                    string errors = p.StandardError.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(errors))
                    {
                        UnityEngine.Debug.Log(string.Format("{0} Output: {1}", DateTime.Now, errors));
                    }
                }
            }
        }
    }
}
