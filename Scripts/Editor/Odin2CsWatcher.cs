using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OdinInterop.Editor
{
    /// <summary>
    /// Watches .odin files in _odinInterop/Source/ (root only, not subdirectories)
    /// and runs odin2cs to regenerate C# bindings on change.
    /// </summary>
    internal class Odin2CsWatcher : AssetPostprocessor
    {
        private static readonly string ODIN_SOURCE_DIR = Path.GetFullPath(Path.Combine(Application.dataPath, "_odinInterop", "Source"));
        private static readonly string ODIN2CS_PATH = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Packages", "com.herohiralal.odininterop",
            "Scripts", "Editor", ".odin2cs", "odin2cs"));
        private static readonly string ODIN2CS_OUTPUT = Path.GetFullPath(Path.Combine(Application.dataPath, "Odin", "Generated"));

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
                Debug.LogWarning($"[Odin2Cs] odin2cs binary not found at {ODIN2CS_PATH}");
                return;
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
    }
}
