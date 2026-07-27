using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OdinInterop.Editor
{
    /// <summary>
    /// Watches .odin files in UnityOdin/Source/ (root only, not subdirectories
    /// such as .exports/ or .imports/) and runs odin2cs to regenerate C# bindings
    /// on change. Only .odin files placed directly in the root of the Source
    /// directory are processed — generated files in subdirectories are ignored.
    /// </summary>
    internal class Odin2CsWatcher : AssetPostprocessor
    {
        private static readonly string ODIN_SOURCE_DIR = Path.GetFullPath(Path.Combine(Application.dataPath, "UnityOdin", "Source"));
        private static readonly string ODIN2CS_DIR = Path.GetFullPath(Path.Combine(
            Application.dataPath, "UnityOdin",
            "Scripts", "Editor", ".odin2cs"));
        private static readonly string ODIN2CS_PATH = Path.Combine(ODIN2CS_DIR, "odin2cs");
        private static readonly string ODIN2CS_OUTPUT = Path.GetFullPath(Path.Combine(Application.dataPath, "UnityOdin", "Generated"));

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var needsRegen = false;

            foreach (var path in importedAssets)
                if (IsWatchedOdinFile(path)) { needsRegen = true; break; }

            if (!needsRegen)
                foreach (var path in deletedAssets)
                    if (IsWatchedOdinFile(path)) { needsRegen = true; break; }

            if (!needsRegen)
                foreach (var path in movedAssets)
                    if (IsWatchedOdinFile(path)) { needsRegen = true; break; }

            if (needsRegen)
            {
                RunOdin2Cs();
            }
        }

        private static bool IsWatchedOdinFile(string assetPath)
        {
            if (!assetPath.EndsWith(".odin")) return false;

            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath)).Replace('\\', '/');
            var sourceDir = ODIN_SOURCE_DIR.Replace('\\', '/');

            if (!fullPath.StartsWith(sourceDir + "/")) return false;

            // Only root files, not exports/ or imports/
            var relative = fullPath.Substring(sourceDir.Length + 1);
            return !relative.Contains("/");
        }

        private static void RunOdin2Cs()
        {
            if (!File.Exists(ODIN2CS_PATH))
            {
                if (!TryBuildOdin2Cs())
                {
                    Debug.LogWarning($"[Odin2Cs] odin2cs binary not found at {ODIN2CS_PATH} and auto-build failed. " +
                                     "Install the Odin compiler (https://odin-lang.org/docs/install/) and run the build script at " +
                                     $"{Path.Combine(ODIN2CS_DIR, "build.sh")}");
                    return;
                }
            }

            Directory.CreateDirectory(ODIN2CS_OUTPUT);

            // Snapshot existing generated files before regeneration
            var prevFiles = new Dictionary<string, string>();
            if (Directory.Exists(ODIN2CS_OUTPUT))
            {
                foreach (var f in Directory.GetFiles(ODIN2CS_OUTPUT, "*.g.cs"))
                    prevFiles[f] = File.ReadAllText(f);
            }

            var psi = new ProcessStartInfo(ODIN2CS_PATH)
            {
                Arguments = $"\"{ODIN_SOURCE_DIR}\" \"{ODIN2CS_OUTPUT}\"",
                WorkingDirectory = ODIN_SOURCE_DIR,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                    Debug.Log($"[Odin2Cs] {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    Debug.LogWarning($"[Odin2Cs] {stderr.Trim()}");

                // Check if any generated C# files actually changed
                var csharpChanged = false;
                foreach (var f in Directory.GetFiles(ODIN2CS_OUTPUT, "*.g.cs"))
                {
                    var current = File.ReadAllText(f);
                    if (!prevFiles.TryGetValue(f, out var prev) || prev != current)
                    {
                        csharpChanged = true;
                        break;
                    }
                }
                // Also check for deleted files
                foreach (var prev in prevFiles)
                {
                    if (!File.Exists(prev.Key))
                    {
                        csharpChanged = true;
                        break;
                    }
                }

                if (csharpChanged)
                {
                    AssetDatabase.Refresh(); // triggers C# recompilation → HotReload via [InitializeOnLoadMethod]
                }
                else
                {
                    OdinCompiler.HotReload(); // no C# changes, just rebuild Odin library
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Odin2Cs] Failed to run: {e.Message}");
            }
        }

        private static bool TryBuildOdin2Cs()
        {
            // Try to find the Odin compiler on PATH
            var odinPath = FindOdinCompiler();
            if (string.IsNullOrEmpty(odinPath))
            {
                Debug.LogWarning("[Odin2Cs] Odin compiler not found on PATH. Cannot auto-build odin2cs.");
                return false;
            }

            Debug.Log($"[Odin2Cs] Auto-building odin2cs using {odinPath}...");

            var psi = new ProcessStartInfo(odinPath)
            {
                Arguments = $"build \"{ODIN2CS_DIR}\" -out:\"{ODIN2CS_PATH}\" -o:size",
                WorkingDirectory = ODIN2CS_DIR,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[Odin2Cs] Auto-build failed with exit code {process.ExitCode}:\n{stderr}");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(stdout))
                    Debug.Log($"[Odin2Cs] {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    Debug.Log($"[Odin2Cs] {stderr.Trim()}");

                Debug.Log("[Odin2Cs] Auto-build succeeded.");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Odin2Cs] Auto-build failed: {e.Message}");
                return false;
            }
        }

        private static string FindOdinCompiler()
        {
            // Check common Odin compiler names on PATH
            var candidates = new[] { "odin", "odin.exe" };

            foreach (var candidate in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.WaitForExit(1000);
                        if (process.ExitCode == 0)
                            return candidate;
                    }
                }
                catch
                {
                    // Not found, try next
                }
            }

            return null;
        }
    }
}
