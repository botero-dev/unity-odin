using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Reflection;
using System.Linq;
using UnityEditorInternal;
using Unity.Collections.LowLevel.Unsafe;

namespace OdinInterop.Editor
{
    internal static class InteropGenerator
    {
        internal static readonly string ODIN_INTEROP_OUT_DIR = Path.GetFullPath(Path.Combine(Application.dataPath, "_odinInterop", "Source"));
        internal static readonly string ODIN_INTEROP_EXPORTS_DIR = Path.GetFullPath(Path.Combine(ODIN_INTEROP_OUT_DIR, "exports"));
        internal static readonly string ODIN_INTEROP_IMPORTS_DIR = Path.GetFullPath(Path.Combine(ODIN_INTEROP_OUT_DIR, "imports"));

        private static HashSet<Type> s_ExportedTypes = new HashSet<Type>(256); // to create in odin

        internal static void GenerateInteropCode()
        {
            s_ExportedTypes.Clear();
            s_HandledTypes.Clear();

            // create a clean odn out dir
            {
                // ensure dirs exist
                if (!Directory.Exists(ODIN_INTEROP_EXPORTS_DIR))
                    Directory.CreateDirectory(ODIN_INTEROP_EXPORTS_DIR);
                if (!Directory.Exists(ODIN_INTEROP_IMPORTS_DIR))
                    Directory.CreateDirectory(ODIN_INTEROP_IMPORTS_DIR);

                // clean old-style files from root (from before subdirectory split)
                foreach (var file in Directory.GetFiles(ODIN_INTEROP_OUT_DIR, "export_*.odin", SearchOption.TopDirectoryOnly))
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(ODIN_INTEROP_OUT_DIR, "import_*.odin", SearchOption.TopDirectoryOnly))
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(ODIN_INTEROP_OUT_DIR, "odntrop_internal_*.odin", SearchOption.TopDirectoryOnly))
                    File.Delete(file);

                // clean new-style subdirectories
                foreach (var file in Directory.GetFiles(ODIN_INTEROP_EXPORTS_DIR, "*.odin", SearchOption.TopDirectoryOnly))
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(ODIN_INTEROP_IMPORTS_DIR, "*.odin", SearchOption.TopDirectoryOnly))
                    File.Delete(file);
            }

            // hand-coded internal files -> exports/ (except CtxSetup -> imports/)
            {
                var p = Path.GetFullPath("Packages/com.herohiralal.odininterop/");
                p = Path.Combine(p, "Scripts", "Editor", "Generator", ".embedded");
                foreach (var f in Directory.GetFiles(p, "*.odin", SearchOption.TopDirectoryOnly))
                {
                    var tgtFileName = Path.GetFileName(f);
                    if (tgtFileName == "stubs.odin") continue; // only for satisfying the lsp

                    var tgtDir = ODIN_INTEROP_EXPORTS_DIR;
                    var tgtFile = Path.GetFullPath(Path.Combine(tgtDir, tgtFileName));
                    File.Copy(f, tgtFile, overwrite: true);
                }
            }

            // export the layers and tags
            {
                var p = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, "unity_layersandtags.odin"));
                s_StrBld.Clear();

                s_StrBld
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine();

                s_StrBld
                    .AppendIndent()
                    .AppendLine("GameObjectLayer :: enum u8 {");

                s_StrBldIndent++;
                for (var i = 0; i < 32; i++)
                {
                    var layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrWhiteSpace(layerName))
                        layerName = $"Layer_{i}";

                    // sanitize layer name
                    for (int c = 0; c < layerName.Length; c++)
                    {
                        var ch = layerName[c];
                        if (!char.IsLetterOrDigit(ch) && ch != '_')
                        {
                            layerName = layerName.Replace(ch, '_');
                        }
                    }

                    s_StrBld
                        .AppendIndent()
                        .Append(layerName)
                        .Append(" = ")
                        .Append(i)
                        .AppendLine(",");
                }
                s_StrBldIndent--;
                s_StrBld
                    .AppendIndent()
                    .AppendLine("}")
                    .AppendLine();

                s_StrBld
                    .AppendIndent()
                    .AppendLine("GameObjectLayerMask :: distinct bit_set[GameObjectLayer;i32]")
                    .AppendLine();

                var tags = InternalEditorUtility.tags;
                foreach (var tag in tags)
                {
                    var t = tag;
                    // sanitize tag name
                    for (int c = 0; c < t.Length; c++)
                    {
                        var ch = t[c];
                        if (!char.IsLetterOrDigit(ch) && ch != '_')
                        {
                            t = t.Replace(ch, '_');
                        }
                    }

                    s_StrBld
                        .AppendIndent()
                        .AppendLine($"GAME_OBJECT_TAG_{t} :: \"{tag}\"");
                }

                File.WriteAllText(p, s_StrBld.ToString());
            }

            // collect export and import types separately
            var exportTypes = new HashSet<Type>();
            var importTypes = new HashSet<Type>();

            foreach (var t in TypeCache.GetTypesWithAttribute<OdinExportAttribute>())
            {
                exportTypes.Add(t);
                importTypes.Remove(t); // Export takes precedence
            }

            foreach (var t in TypeCache.GetTypesWithAttribute<OdinImportAttribute>())
            {
                if (!exportTypes.Contains(t))
                    importTypes.Add(t);
            }

            // generate export bindings
            foreach (var t in exportTypes)
            {
                var attr = t.GetCustomAttribute<OdinExportAttribute>();
                GenerateExportOdinCode(t, attr?.odinSrcAppend ?? "");
            }

            // generate import bindings
            foreach (var t in importTypes)
            {
                var attr = t.GetCustomAttribute<OdinImportAttribute>();
                GenerateImportOdinCode(t, attr?.odinSrcAppend ?? "");
            }

            // export types
            {
                s_StrBld.Clear();
                var tgtFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"exported_types.odin"));
                s_StrBld
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine();

                while (s_ExportedTypes.Count > 0) // doing it recursively, the functions themselves might collect more types to export
                {
                    var copy = s_ExportedTypes.ToArray();
                    s_ExportedTypes.Clear();
                    foreach (var t in copy)
                        s_StrBld.AppendOdnTypeDef(t);
                }

                File.WriteAllText(tgtFile, s_StrBld.ToString());
            }
        }

        private static StringBuilder s_StrBld = new StringBuilder(16384);
        private static int s_StrBldIndent = 0;
        private static StringBuilder AppendIndent(this StringBuilder sb)
        {
            for (int i = 0; i < s_StrBldIndent; i++)
            {
                sb.Append('\t');
            }

            return sb;
        }

        private static Dictionary<Type, string> s_SourcePathCache = new Dictionary<Type, string>();

        private static string GetCSharpSourcePath(Type t)
        {
            if (s_SourcePathCache.TryGetValue(t, out var cached))
                return cached;

            var className = t.Name;
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var searchDirs = new[] { Application.dataPath, Path.Combine(projectRoot, "Packages") };

            foreach (var searchDir in searchDirs)
            {
                if (!Directory.Exists(searchDir)) continue;
                var files = Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    if (content.Contains($"class {className}") || content.Contains($"struct {className}"))
                    {
                        var absolutePath = Path.GetFullPath(file).Replace('\\', '/');
                        s_SourcePathCache[t] = absolutePath;
                        return absolutePath;
                    }
                }
            }

            s_SourcePathCache[t] = null;
            return null;
        }

        private static Dictionary<string, int> s_MethodLinesCache = new Dictionary<string, int>();

        private static int GetMethodLineNumber(string sourcePath, string methodName)
        {
            if (string.IsNullOrEmpty(sourcePath))
                return 0;

            var key = $"{sourcePath}::{methodName}";
            if (s_MethodLinesCache.TryGetValue(key, out var cached))
                return cached;

            var lines = File.ReadAllLines(sourcePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.Contains($" {methodName}(") || trimmed.Contains($" {methodName}<"))
                {
                    var lineNumber = i + 1;
                    s_MethodLinesCache[key] = lineNumber;
                    return lineNumber;
                }
            }

            s_MethodLinesCache[key] = 0;
            return 0;
        }

        // Prefix type names that were exported by Unity (in s_ExportedTypes) with "exports."
        private static string QualifyExportType(string typeName, Type csharpType)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            if (typeName.Contains('.')) return typeName;            // already qualified (e.g. runtime.Allocator)
            if (csharpType != null && s_ExportedTypes.Contains(csharpType))
                return $"exports.{typeName}";
            return typeName;
        }

        private static void GenerateExportOdinCode(Type t, string odinSrcAppend)
        {
            var tyName = t.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = tyName == "OdinInterop_EngineBindings" ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var className = t.Name;
            var instName = $"_{className}";

            var exportedFns = t.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.Name.StartsWith("odntrop_"))
                .ToArray();

            if (exportedFns.Length == 0)
                return;

            Debug.Log($"[Odin Interop] Generating export bindings for {t.FullName}: {exportedFns.Length} exported functions");

            var tgtFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"export_{tyName}_impl.odin"));

            s_StrBld
                .Clear()
                .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                .AppendLine("#+vet !tabs !unused !style")
                .AppendLine("package exports")
                .AppendLine()
                .AppendLine("@require import \"base:runtime\"")
                .AppendLine();

            foreach (var exportedFn in exportedFns)
            {
                // delegate signature type
                {
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("@(private = \"file\")")
                        .AppendIndent()
                        .Append($"odntrop_del_{tyName}_{exportedFn.Name} :: #type proc \"c\" (");

                    if (!exportedFn.IsStatic)
                    {
                        s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(t);
                        s_StrBld.Append(", ");
                    }

                    var parms = exportedFn.GetParameters();
                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name).Append(": ").AppendOdnTypeName(p.ParameterType);
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.Append(")");
                    if (exportedFn.ReturnType != typeof(void))
                        s_StrBld.Append(" -> ").AppendOdnTypeName(exportedFn.ReturnType);

                    s_StrBld.AppendLine().AppendLine();
                }

                // delegate global var
                {
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("@(private = \"file\")")
                        .AppendIndent()
                        .AppendLine($"odntrop_dydel_{tyName}_{exportedFn.Name}: odntrop_del_{tyName}_{exportedFn.Name} = nil")
                        .AppendLine();
                }

                // delegate setter (called by C# to register the implementation)
                {
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("@(export, private = \"file\")")
                        .AppendIndent()
                        .AppendLine($"odntrop_export_setter_{tyName}_{exportedFn.Name} :: proc (value: odntrop_del_{tyName}_{exportedFn.Name}) {{");

                    s_StrBldIndent++;
                    s_StrBld
                        .AppendIndent()
                        .AppendLine($"odntrop_dydel_{tyName}_{exportedFn.Name} = value");
                    s_StrBldIndent--;
                    s_StrBld.AppendIndent().AppendLine("}").AppendLine();
                }

                // user-facing Odin wrapper function
                {
                    s_StrBld
                        .AppendIndent()
                        .Append($"{cleanTyName}{underScoreIfCleanTyName}{exportedFn.Name}_impl :: proc(");

                    if (!exportedFn.IsStatic)
                    {
                        s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(t);
                        s_StrBld.Append(", ");
                    }

                    var parms = exportedFn.GetParameters();
                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name).Append(": ").AppendOdnTypeName(p.ParameterType);
                        if (p.HasDefaultValue)
                        {
                            static bool HandleDefaultValue<T>(StringBuilder sb, ParameterInfo p)
                            {
                                if (p.ParameterType != typeof(T))
                                    return false;

                                var val = (T)p.DefaultValue;
                                var str = val.ToString().ToLowerInvariant();
                                if (str == "-infinity")
                                    str = "f32(0hFF80_0000)";
                                else if (str == "infinity" || str == "+infinity")
                                    str = "f32(0h7F80_0000)";

                                sb.Append(" = ").Append(str);
                                return true;
                            }

                            if (p.ParameterType == typeof(Allocator))
                            {
                                s_StrBld.Append(" = context.allocator");
                            }
                            else if (false ||
                                    HandleDefaultValue<bool>(s_StrBld, p) ||
                                    HandleDefaultValue<byte>(s_StrBld, p) ||
                                    HandleDefaultValue<sbyte>(s_StrBld, p) ||
                                    HandleDefaultValue<ushort>(s_StrBld, p) ||
                                    HandleDefaultValue<short>(s_StrBld, p) ||
                                    HandleDefaultValue<int>(s_StrBld, p) ||
                                    HandleDefaultValue<uint>(s_StrBld, p) ||
                                    HandleDefaultValue<long>(s_StrBld, p) ||
                                    HandleDefaultValue<ulong>(s_StrBld, p) ||
                                    HandleDefaultValue<float>(s_StrBld, p) ||
                                    HandleDefaultValue<double>(s_StrBld, p) ||
                                    false)
                            {
                            }
                            else if (p.ParameterType == typeof(Quaternion))
                            {
                                s_StrBld.Append(" = quaternion128(1)");
                            }
                            else if (p.ParameterType.IsEnum)
                            {
                                s_StrBld.Append(" = .").Append(p.DefaultValue.ToString());
                            }
                            else
                            {
                                s_StrBld.Append(" = {}");
                            }
                        }
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.Append(")");
                    if (exportedFn.ReturnType != typeof(void))
                        s_StrBld.Append(" -> ").AppendOdnTypeName(exportedFn.ReturnType);

                    s_StrBld.AppendLine(" {");
                    s_StrBldIndent++;
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("odntrop_internal_tempCtx := G_OdnTrop_Internal_Ctx")
                        .AppendIndent()
                        .AppendLine("G_OdnTrop_Internal_Ctx = context")
                        .AppendIndent()
                        .AppendLine("defer G_OdnTrop_Internal_Ctx = odntrop_internal_tempCtx");

                    if (exportedFn.ReturnType != typeof(void))
                    {
                        s_StrBld
                            .AppendIndent()
                            .Append("odntrop_internal_RetValXXX: ")
                            .AppendOdnTypeName(exportedFn.ReturnType)
                            .AppendLine();
                    }

                    s_StrBld
                        .AppendIndent()
                        .AppendLine($"if odntrop_dydel_{tyName}_{exportedFn.Name} != nil {{");

                    s_StrBldIndent++;

                    s_StrBld.AppendIndent();
                    if (exportedFn.ReturnType != typeof(void))
                        s_StrBld.Append("odntrop_internal_RetValXXX = ");
                    s_StrBld.Append($"odntrop_dydel_{tyName}_{exportedFn.Name}(");

                    if (!exportedFn.IsStatic)
                    {
                        s_StrBld.Append(instName);
                        s_StrBld.Append(", ");
                    }

                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name);
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.AppendLine(")");
                    s_StrBldIndent--;
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("}");

                    if (exportedFn.ReturnType != typeof(void))
                    {
                        s_StrBld
                            .AppendIndent()
                            .AppendLine("return odntrop_internal_RetValXXX");
                    }

                    s_StrBldIndent--;
                    s_StrBld.AppendIndent().AppendLine("}").AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(odinSrcAppend))
                s_StrBld.AppendLine(odinSrcAppend);

            File.WriteAllText(tgtFile, s_StrBld.ToString());

            // Generate decl file — forwarding wrappers to _impl
            {
                var declFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"export_{tyName}.odin"));
                s_StrBld
                    .Clear()
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine()
                    .AppendLine("@require import \"base:runtime\"")
                    .AppendLine();

                var declSrcPath = GetCSharpSourcePath(t);
                if (declSrcPath != null)
                    s_StrBld.AppendLine($"// Source: file://{declSrcPath}").AppendLine();

                foreach (var exportedFn in exportedFns)
                {
                    var declFnLine = GetMethodLineNumber(declSrcPath, exportedFn.Name);
                    if (declFnLine > 0)
                        s_StrBld.AppendIndent().AppendLine($"// Source: file://{declSrcPath}#L{declFnLine}");

                    var parms = exportedFn.GetParameters();

                    s_StrBld
                        .AppendIndent()
                        .Append($"{cleanTyName}{underScoreIfCleanTyName}{exportedFn.Name} :: #force_inline proc(");

                    if (!exportedFn.IsStatic)
                    {
                        s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(t);
                        s_StrBld.Append(", ");
                    }

                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name).Append(": ").AppendOdnTypeName(p.ParameterType);
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.Append(")");
                    if (exportedFn.ReturnType != typeof(void))
                        s_StrBld.Append(" -> ").AppendOdnTypeName(exportedFn.ReturnType);

                    s_StrBld.AppendLine(" {");
                    s_StrBldIndent++;

                    s_StrBld.AppendIndent();
                    if (exportedFn.ReturnType != typeof(void))
                        s_StrBld.Append("return ");
                    s_StrBld.Append($"{cleanTyName}{underScoreIfCleanTyName}{exportedFn.Name}_impl(");

                    if (!exportedFn.IsStatic)
                    {
                        s_StrBld.Append(instName);
                        s_StrBld.Append(", ");
                    }

                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name);
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.AppendLine(")");

                    s_StrBldIndent--;
                    s_StrBld.AppendIndent().AppendLine("}").AppendLine();
                }

                File.WriteAllText(declFile, s_StrBld.ToString());
            }
        }

        private static void GenerateImportOdinCode(Type t, string odinSrcAppend)
        {
            var tyName = t.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = (tyName == "OdinInterop_EngineBindings" || tyName == "OdinInterop_EngineBindingsImports") ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var className = t.Name;
            var instName = $"_{className}";

            var importedFns = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.Name.StartsWith("odntrop_"))
                .ToArray();

            if (importedFns.Length == 0)
                return;

            Debug.Log($"[Odin Interop] Generating import bindings for {t.FullName}: {importedFns.Length} imported functions");

            var tgtFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_IMPORTS_DIR, $"import_{tyName}.odin"));

            s_StrBld
                .Clear()
                .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                .AppendLine("#+vet !tabs !unused !style")
                .AppendLine("package imports")
                .AppendLine()
                .AppendLine("import src \"..\"")
                .AppendLine("import \"../exports\"")
                .AppendLine("@require import \"base:runtime\"")
                .AppendLine();

            foreach (var importedFn in importedFns)
            {
                if (!importedFn.IsStatic)
                    Debug.LogWarning($"parsing nonstatic imported {tyName} {importedFn}");

                // Read original Odin type info (generated by Odin2Cs)
                var foreignDecl = importedFn.GetCustomAttribute<ForeignDeclAttribute>();

                // Resolve Odin type name for a param or return, preferring original Odin type
                string OdinParamType(int i) =>
                    QualifyExportType(foreignDecl?.ParamTypes != null && i < foreignDecl.ParamTypes.Length
                        ? foreignDecl.ParamTypes[i]
                        : importedFn.GetParameters()[i].ParameterType.AppendOdnTypeNameToString(),
                        importedFn.GetParameters()[i].ParameterType);

                string OdinReturnType() =>
                    QualifyExportType(!string.IsNullOrEmpty(foreignDecl?.ReturnType)
                        ? foreignDecl.ReturnType
                        : (importedFn.ReturnType != typeof(void) ? importedFn.ReturnType.AppendOdnTypeNameToString() : null),
                        importedFn.ReturnType != typeof(void) ? importedFn.ReturnType : null);

                // exported C-callable wrapper (called by C# via DLL import)
                {
                    var implName = foreignDecl?.OdinName
                        ?? $"{cleanTyName}{underScoreIfCleanTyName}{importedFn.Name}";

                    s_StrBld
                        .AppendIndent()
                        .AppendLine("@(export, private = \"file\")")
                        .AppendIndent()
                        .Append($"odntrop_export_{tyName}_{importedFn.Name} :: proc \"c\" (");

                    if (!importedFn.IsStatic)
                    {
                        s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(t);
                        s_StrBld.Append(", ");
                    }

                    var parms = importedFn.GetParameters();
                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name).Append(": ").Append(OdinParamType(i));
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.Append(")");
                    var returnType = OdinReturnType();
                    if (returnType != null)
                        s_StrBld.Append(" -> ").Append(returnType);

                    s_StrBld.AppendLine(" {");

                    s_StrBldIndent++;
                    s_StrBld
                        .AppendIndent()
                        .AppendLine("context = exports.CreateUnityContext() if exports.G_OdnTrop_Internal_CtxNesting == 0 else exports.G_OdnTrop_Internal_Ctx")
                        .AppendIndent()
                        .AppendLine("exports.G_OdnTrop_Internal_CtxNesting += 1")
                        .AppendIndent()
                        .AppendLine("defer exports.G_OdnTrop_Internal_CtxNesting -= 1")
                        .AppendIndent()
                        .Append(importedFn.ReturnType == typeof(void) ? "" : "return ")
                        .Append(string.IsNullOrWhiteSpace(odinSrcAppend) ? "src." : "")
                        .Append(implName)
                        .Append("(");

                    if (!importedFn.IsStatic)
                    {
                        s_StrBld.Append(instName);
                        s_StrBld.Append(", ");
                    }

                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        s_StrBld.Append(p.Name);
                        s_StrBld.Append(", ");
                    }

                    s_StrBld.AppendLine(")");

                    s_StrBldIndent--;
                    s_StrBld.AppendIndent().AppendLine("}").AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(odinSrcAppend))
                s_StrBld.AppendLine(odinSrcAppend);

            File.WriteAllText(tgtFile, s_StrBld.ToString());
        }

        private static HashSet<Type> s_HandledTypes = new HashSet<Type>(256);
        private static readonly MethodInfo s_AlignOfMethod = typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AlignOf), BindingFlags.Public | BindingFlags.Static);
        private static StringBuilder AppendOdnTypeDef(this StringBuilder sb, Type t)
        {
            if (s_HandledTypes.Contains(t))
                return sb;

            if (t == typeof(UnityEngine.Object))
            {
                s_HandledTypes.Add(t);
                return sb.AppendIndent().AppendLine("Object :: struct { id: i32 }").AppendLine();
            }

            var resolvedName = t.GetResolvedOdnTypeName();

            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                s_ExportedTypes.Add(t.BaseType);

                var baseTypeResolvedName = t.BaseType.GetResolvedOdnTypeName();
                s_HandledTypes.Add(t);
                return sb
                    .AppendIndent()
                    .AppendLine($"{resolvedName} :: struct {{ #subtype parent: {baseTypeResolvedName} }}")
                    .AppendIndent()
                    .AppendLine($"OBJECT_TYPE_{resolvedName} :: `{t.AssemblyQualifiedName}`")
                    .AppendLine();
            }

            if (t.IsEnum)
            {
                var underlyingType = t.GetEnumUnderlyingType();
                sb.AppendIndent().Append($"{resolvedName} :: enum ").AppendOdnTypeName(underlyingType).AppendLine(" {");
                s_StrBldIndent++;
                var names = t.GetEnumNames();
                var vals = t.GetEnumValues();
                for (int i = 0; i < names.Length; i++)
                {
                    sb.AppendIndent().Append(names[i]).Append(" = ");
                    if (underlyingType == typeof(ulong))
                    {
                        sb.Append((ulong)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(long))
                    {
                        sb.Append((long)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(uint))
                    {
                        sb.Append((uint)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(int))
                    {
                        sb.Append((int)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(ushort))
                    {
                        sb.Append((ushort)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(short))
                    {
                        sb.Append((short)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(byte))
                    {
                        sb.Append((byte)vals.GetValue(i));
                    }
                    else if (underlyingType == typeof(sbyte))
                    {
                        sb.Append((sbyte)vals.GetValue(i));
                    }
                    sb.AppendLine(",");
                }
                s_StrBldIndent--;
                s_HandledTypes.Add(t);
                return sb.AppendIndent().AppendLine("}").AppendLine();
            }

            if (UnsafeUtility.IsUnmanaged(t) && t.IsValueType)
            {
                sb.AppendIndent().AppendLine($"#assert(size_of({resolvedName}) == {UnsafeUtility.SizeOf(t)}, \"Size mismatch for {resolvedName}!\")");
                sb.AppendIndent().AppendLine($"#assert(align_of({resolvedName}) == {(int)s_AlignOfMethod.MakeGenericMethod(t).Invoke(null, null)}, \"Align mismatch for {resolvedName}!\")");


                sb.AppendIndent().AppendLine($"{resolvedName} :: struct {{");
                s_StrBldIndent++;
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    sb
                        .AppendIndent()
                        .Append(field.Name)
                        .Append(": ")
                        .AppendOdnTypeName(field.FieldType)
                        .AppendLine(",");
                }
                s_StrBldIndent--;
                s_HandledTypes.Add(t);
                return sb.AppendIndent().AppendLine("}").AppendLine();
            }

            // unknown
            s_HandledTypes.Add(t);
            return sb.Append($"#panic(\"{resolvedName} has not been handled correctly.\")").AppendLine();
        }

        private static readonly Dictionary<Type, string> s_OdnTypeNameCache = new Dictionary<Type, string>(256);
        private static string GetResolvedOdnTypeName(this Type t)
        {
            if (s_OdnTypeNameCache.TryGetValue(t, out var cachedName))
                return cachedName;

            var isSpecialNamespace = true;
            var resolvedName = t.FullName.Replace('+', '.').Replace('.', '_');
            if (resolvedName.StartsWith("UnityEngine_SceneManagement_")) // this must come first otherwise it matches UnityEngine_
                resolvedName = resolvedName["UnityEngine_SceneManagement_".Length..];
            else if (resolvedName.StartsWith("UnityEngine_Audio_"))
                resolvedName = resolvedName["UnityEngine_Audio_".Length..];
            else if (resolvedName.StartsWith("UnityEngine_Playables_"))
                resolvedName = resolvedName["UnityEngine_Playables_".Length..];
            else if (resolvedName.StartsWith("UnityEngine_Rendering_"))
                resolvedName = resolvedName["UnityEngine_Rendering_".Length..];
            else if (resolvedName.StartsWith("UnityEngine_UI_"))
                resolvedName = resolvedName["UnityEngine_UI_".Length..];
            else if (resolvedName.StartsWith("UnityEngine_"))
                resolvedName = resolvedName["UnityEngine_".Length..];
            else if (resolvedName.StartsWith("UnityEditor_"))
                resolvedName = resolvedName["UnityEditor_".Length..];
            else if (resolvedName.StartsWith("OdinInterop_"))
                resolvedName = resolvedName["OdinInterop_".Length..];
            else
                isSpecialNamespace = false;

            if (isSpecialNamespace) // remove underscores if it's an internal type
                resolvedName = resolvedName.Replace("_", "");

            s_OdnTypeNameCache[t] = resolvedName;
            return resolvedName;
        }

        private static StringBuilder AppendOdnTypeName(this StringBuilder sb, Type t)
        {
            if (t.IsPointer || t.IsByRef)
            {
                if (t == typeof(void*))
                {
                    return sb.Append("rawptr");
                }

                sb.Append("^");
                sb.AppendOdnTypeName(t.GetElementType());
                return sb;
            }

            var resolvedName = t.GetResolvedOdnTypeName();

            if (t == typeof(void))
            {
                sb.Append("()");
            }
            else if (t == typeof(byte))
            {
                sb.Append("u8");
            }
            else if (t == typeof(sbyte))
            {
                sb.Append("i8");
            }
            else if (t == typeof(short))
            {
                sb.Append("i16");
            }
            else if (t == typeof(ushort))
            {
                sb.Append("u16");
            }
            else if (t == typeof(int))
            {
                sb.Append("i32");
            }
            else if (t == typeof(uint))
            {
                sb.Append("u32");
            }
            else if (t == typeof(long))
            {
                sb.Append("i64");
            }
            else if (t == typeof(ulong))
            {
                sb.Append("u64");
            }
            else if (t == typeof(float))
            {
                sb.Append("f32");
            }
            else if (t == typeof(double))
            {
                sb.Append("f64");
            }
            else if (t == typeof(bool))
            {
                sb.Append("bool");
            }
            else if (t == typeof(Vector2))
            {
                sb.Append("[2]f32");
            }
            else if (t == typeof(Vector3))
            {
                sb.Append("[3]f32");
            }
            else if (t == typeof(Vector4))
            {
                sb.Append("[4]f32");
            }
            else if (t == typeof(Quaternion))
            {
                sb.Append("quaternion128");
            }
            else if (t == typeof(Color))
            {
                sb.Append("[4]f32");
            }
            else if (t.IsArray)
            {
                sb.Append("[]").AppendOdnTypeName(t.GetElementType());
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Slice<>))
            {
                sb.Append("[]").AppendOdnTypeName(t.GetGenericArguments()[0]);
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                sb.Append("[dynamic]").AppendOdnTypeName(t.GetGenericArguments()[0]);
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(DynamicArray<>))
            {
                sb.Append("[dynamic]").AppendOdnTypeName(t.GetGenericArguments()[0]);
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ObjectHandle<>))
            {
                var tgt = t.GetGenericArguments()[0];
                sb.AppendOdnTypeName(tgt);
            }
            else if (t == typeof(RawSlice))
            {
                sb.Append("runtime.Raw_Slice");
            }
            else if (t == typeof(RawDynamicArray))
            {
                sb.Append("runtime.Raw_Dynamic_Array");
            }
            else if (t == typeof(RawObjectHandle))
            {
                sb.Append("Object");
            }
            else if (t == typeof(string))
            {
                sb.Append("string16");
            }
            else if (t == typeof(String8))
            {
                sb.Append("string");
            }
            else if (t == typeof(String16))
            {
                sb.Append("string16");
            }
            else if (t == typeof(Allocator))
            {
                sb.Append("runtime.Allocator");
            }
            else if (t == typeof(Color32))
            {
                sb.Append("Color32");
            }
            else if (t == typeof(LayerMask))
            {
                sb.Append("GameObjectLayerMask");
            }
            else
            {
                sb.Append(resolvedName);
                s_ExportedTypes.Add(t);
            }

            return sb;
        }

        private static string AppendOdnTypeNameToString(this Type t)
        {
            var sb = new StringBuilder();
            sb.AppendOdnTypeName(t);
            return sb.ToString();
        }
    }
}
