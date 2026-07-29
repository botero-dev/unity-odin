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
        internal static readonly string ODIN_INTEROP_OUT_DIR = Path.GetFullPath(Path.Combine(Application.dataPath, "UnityOdin", "Source"));
        internal static readonly string ODIN_INTEROP_EXPORTS_DIR = Path.GetFullPath(Path.Combine(ODIN_INTEROP_OUT_DIR, ".exports"));
        internal static readonly string ODIN_INTEROP_IMPORTS_DIR = Path.GetFullPath(Path.Combine(ODIN_INTEROP_OUT_DIR, ".imports"));

        private static HashSet<Type> s_ExportedTypesFlat = new HashSet<Type>(256); // flat lookup for QualifyExportType
        private static Dictionary<string, HashSet<Type>> s_ExportedTypesByNamespace = new Dictionary<string, HashSet<Type>>(16); // grouped by namespace→filename

        private static void AddExportedType(Type t)
        {
            if (t == null) return;
            s_ExportedTypesFlat.Add(t);
            var fileKey = GetNamespaceFileName(t);
            if (!s_ExportedTypesByNamespace.TryGetValue(fileKey, out var set))
            {
                set = new HashSet<Type>();
                s_ExportedTypesByNamespace[fileKey] = set;
            }
            set.Add(t);
        }

        internal static void GenerateInteropCode()
        {
            s_ExportedTypesFlat.Clear();
            s_ExportedTypesByNamespace.Clear();
            s_HandledTypes.Clear();
            s_ImplBld.Clear();

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

            // hand-coded internal files -> exports/
            {
                var p = Path.GetFullPath(Path.Combine(Application.dataPath, "UnityOdin", "Scripts", "Editor", "Generator", ".embedded"));
                foreach (var f in Directory.GetFiles(p, "*.odin", SearchOption.TopDirectoryOnly))
                {
                    var tgtFileName = Path.GetFileName(f);

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
            var exportAllStubTypes = new HashSet<Type>();

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

            // generate export-all bindings FIRST (they write the decl files,
            // then type-def resolution merges struct defs into them)
            foreach (var t in TypeCache.GetTypesWithAttribute<OdinExportAllAttribute>())
            {
                exportAllStubTypes.Add(t);
                var attr = t.GetCustomAttribute<OdinExportAllAttribute>();
                var targetType = attr?.TargetType;
                if (targetType != null)
                {
                    // Snapshot existing types so we can identify which types
                    // were discovered during this specific ExportAll codegen pass.
                    var preExistingTypes = new HashSet<Type>(s_ExportedTypesFlat);

                    GenerateExportAllOdinCode(t, targetType, attr?.odinSrcAppend ?? "");

                    // Append type definitions discovered during this ExportAll
                    // to the class-specific .odin file.
                    ResolveExportAllClassTypes(t, preExistingTypes);
                }
            }

            // generate import bindings
            foreach (var t in importTypes)
            {
                var attr = t.GetCustomAttribute<OdinImportAttribute>();
                GenerateImportOdinCode(t, attr?.odinSrcAppend ?? "");
            }

            // generate export bindings (skip types already handled by ExportAll)
            foreach (var t in exportTypes)
            {
                if (exportAllStubTypes.Contains(t))
                {
                    // This type also has [OdinExportAll] — generate export wrappers
                    // and merge them into the ExportAll decl file.
                    GenerateAndMergeExportIntoExportAllFile(t);
                }
                else
                {
                    var attr = t.GetCustomAttribute<OdinExportAttribute>();
                    GenerateExportOdinCode(t, attr?.odinSrcAppend ?? "");
                }
            }

            // export types — one file per C# namespace (sub-namespaces use underscore, e.g. UnityEngine_Audio.odin)
            {
                // per-namespace builders that accumulate across recursive resolution passes
                var namespaceBuilders = new Dictionary<string, StringBuilder>();

                while (true)
                {
                    var snapshot = new Dictionary<string, HashSet<Type>>(s_ExportedTypesByNamespace);
                    s_ExportedTypesByNamespace.Clear();

                    if (snapshot.Count == 0)
                        break;

                    foreach (var kvp in snapshot)
                    {
                        var fileKey = kvp.Key;
                        var types = kvp.Value;

                        if (types.Count == 0) continue;

                        // get or create the per-namespace builder (with header)
                        if (!namespaceBuilders.TryGetValue(fileKey, out var nsBuilder))
                        {
                            nsBuilder = new StringBuilder(4096);
                            nsBuilder
                                .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                                .AppendLine("#+vet !tabs !unused !style")
                                .AppendLine("package exports")
                                .AppendLine();
                            namespaceBuilders[fileKey] = nsBuilder;
                        }

                        // swap in the per-namespace builder so AppendOdnTypeDef writes to it
                        var savedStrBld = s_StrBld;
                        s_StrBld = nsBuilder;
                        var savedIndent = s_StrBldIndent;
                        s_StrBldIndent = 0;

                        while (types.Count > 0)
                        {
                            var typeCopy = types.ToArray();
                            types.Clear();
                            foreach (var t in typeCopy)
                                s_StrBld.AppendOdnTypeDef(t);
                        }

                        s_StrBld = savedStrBld;
                        s_StrBldIndent = savedIndent;
                    }
                }

                // Write type-def files, merging with any decl content (without duplicate headers)
                foreach (var kvp in namespaceBuilders)
                {
                    var tgtFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"{kvp.Key}.odin"));
                    var existing = "";
                    if (File.Exists(tgtFile))
                    {
                        var lines = File.ReadAllLines(tgtFile);
                        // Skip header lines that are duplicated in the type-def output
                        int skip = 0;
                        while (skip < lines.Length && (
                            lines[skip].StartsWith("//") ||
                            lines[skip].StartsWith("#+vet") ||
                            lines[skip].StartsWith("package ") ||
                            lines[skip].StartsWith("@require") ||
                            lines[skip].Length == 0))
                            skip++;
                        existing = string.Join("\n", lines.Skip(skip)) + "\n";
                    }
                    File.WriteAllText(tgtFile, kvp.Value.ToString() + "\n" + existing);
                }
            }

            // Write the single shared _impl.odin with all delegate plumbing
            if (s_ImplBld.Length > 0)
            {
                var implFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, "interop_impl.odin"));
                var implHeader = new StringBuilder()
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine()
                    .AppendLine("@require import \"base:runtime\"")
                    .AppendLine();
                File.WriteAllText(implFile, implHeader.ToString() + s_ImplBld.ToString());
            }
        }

        private static StringBuilder s_StrBld = new StringBuilder(16384);
        private static StringBuilder s_ImplBld = new StringBuilder(65536); // accumulated _impl content for all exports
        private static int s_StrBldIndent = 0;
        private static StringBuilder AppendIndent(this StringBuilder sb)
        {
            for (int i = 0; i < s_StrBldIndent; i++)
            {
                sb.Append('\t');
            }

            return sb;
        }

        // Source-location manifest — populated at compile time by SourceGenerator,
        // read at editor time via reflection. No filesystem scan needed.
        //
        // Each generated .g.cs file carries:
        //   OdinInteropSourcePath    → const string, path to the original C# file
        //   OdinInteropMethodLines   → Dictionary<string,int>, method name → line number
        //
        // These are consumed to annotate generated .odin files with `// Source:` comments
        // so readers can navigate from an Odin binding to the C# definition.
        private const BindingFlags s_ManifestFieldFlags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public;

        // Finds the type that holds the manifest (the user type itself for static partials,
        // or {Name}_OdinInterop for non-static classes).
        // NOTE: Requires the SourceGenerator DLL to be rebuilt after changes to SourceGenerator.cs.
        //   cd Assets/UnityOdin/Scripts/Editor/.generator && dotnet build
        private static Type FindManifestType(Type t)
        {
            // Static partial classes: manifest fields are on the type itself
            if (t.GetField("OdinInteropSourcePath", s_ManifestFieldFlags) != null)
                return t;

            // Non-static classes: manifest lives on {Namespace}.{Name}_OdinInterop
            // (top-level type, not nested — use Name/Namespace, not FullName which uses + for nesting)
            var manifestName = string.IsNullOrEmpty(t.Namespace)
                ? $"{t.Name}_OdinInterop"
                : $"{t.Namespace}.{t.Name}_OdinInterop";
            return t.Assembly.GetType(manifestName);
        }

        private static string GetCSharpSourcePath(Type t)
        {
            var manifestType = FindManifestType(t);
            return (string)manifestType?.GetField("OdinInteropSourcePath", s_ManifestFieldFlags)?.GetValue(null);
        }

        private static int GetMethodLineNumber(Type t, string methodName)
        {
            var manifestType = FindManifestType(t);
            if (manifestType == null) return 0;
            var dict = (Dictionary<string, int>)manifestType
                .GetField("OdinInteropMethodLines", s_ManifestFieldFlags)?.GetValue(null);
            if (dict == null) return 0;
            dict.TryGetValue(methodName, out var line);
            return line;
        }

        // Prefix type names that were exported by Unity (in s_ExportedTypesFlat) with "exports."
        // Also normalizes user-chosen import aliases (e.g. "unity.TestComponent" -> "exports.TestComponent")
        private static string QualifyExportType(string typeName, Type csharpType)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            if (typeName.Contains('.'))
            {
                // Already qualified — check if the base type is an exported Unity type
                var lastDot = typeName.LastIndexOf('.');
                var baseName = typeName.Substring(lastDot + 1);
                if (csharpType != null && s_ExportedTypesFlat.Contains(csharpType))
                    return $"exports.{baseName}";  // normalize alias to "exports."
                return typeName;  // keep non-exported qualified types (e.g. runtime.Allocator)
            }
            if (csharpType != null && s_ExportedTypesFlat.Contains(csharpType))
                return $"exports.{typeName}";
            return typeName;
        }

        private static void GenerateExportOdinCode(Type t, string odinSrcAppend)
        {
            var tyName = t.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = (tyName.StartsWith("OdinInterop_") || tyName.StartsWith("OdinExports_")) ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var className = t.Name;
            var instName = $"_{className}";

            var exportedFns = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.Name.StartsWith("odntrop_"))
                .Where(x => x.IsAssembly || x.IsPublic)
                .ToArray();

            // User-facing Odin wrapper names: class prefix (minus "Unity") + snake_case method
            string OdinFnName(MethodInfo m)
            {
                if (cleanTyName == "")
                {
                    var classStem = className.StartsWith("Unity") ? className["Unity".Length..] : className;
                    return $"{classStem}_{m.Name}";
                }
                return $"{cleanTyName}{underScoreIfCleanTyName}{m.Name}";
            }

            if (exportedFns.Length == 0)
                return;

            var fileName = tyName.StartsWith("OdinInterop_") ? tyName["OdinInterop_".Length..]
                         : tyName.StartsWith("OdinExports_") ? tyName["OdinExports_".Length..]
                         : tyName;

            // ── impl file content: delegate types, C-callable setters, and Odin wrapper logic ──
            // Accumulated into a single shared _impl.odin for all exports.

            s_StrBld.Clear();
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
                        .Append($"{OdinFnName(exportedFn)}_impl :: proc(");

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

            s_ImplBld.Append(s_StrBld.ToString());

            // ── decl file: user-facing #force_inline wrappers that forward to _impl ──
            // Annotated with C# source locations so readers can jump to the original code.
            // This is the file users open to understand the binding — the _impl file exists
            // solely to separate the delegate/plumbing noise from the readable API surface.
            {
                var declFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"{fileName}.odin"));
                s_StrBld
                    .Clear()
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine()
                    .AppendLine("@require import \"base:runtime\"")
                    .AppendLine();

                // Annotate the class-level source file so readers know where this binding originates
                var declSrcPath = GetCSharpSourcePath(t);
                if (declSrcPath != null)
                    s_StrBld.AppendLine($"// Source: file://{declSrcPath}").AppendLine();

                // Read doc comments emitted by SourceGenerator
                var manifestType = FindManifestType(t);
                var docComments = (Dictionary<string, string>)manifestType?
                    .GetField("OdinInteropComments", s_ManifestFieldFlags)?.GetValue(null);

                foreach (var exportedFn in exportedFns)
                {
                    // Emit doc comment from C# source if available
                    if (docComments != null && docComments.TryGetValue(exportedFn.Name, out var comment))
                        s_StrBld.AppendIndent().AppendLine($"// {comment}");

                    // Per-function source annotation: exact file + line so readers can jump to the C# definition
                    var declFnLine = GetMethodLineNumber(t, exportedFn.Name);
                    if (declFnLine > 0)
                        s_StrBld.AppendIndent().AppendLine($"// Source: file://{declSrcPath}#L{declFnLine}");

                    var parms = exportedFn.GetParameters();

                    s_StrBld
                        .AppendIndent()
                        .Append($"{OdinFnName(exportedFn)} :: #force_inline proc(");

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
                    s_StrBld.Append($"{OdinFnName(exportedFn)}_impl(");

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

        /// <summary>
        /// Generates export wrappers for a type that also has [OdinExportAll].
        /// Impl content is appended to s_ImplBld; decl wrappers are appended
        /// to the ExportAll decl file without re-writing the header.
        /// </summary>
        private static void GenerateAndMergeExportIntoExportAllFile(Type t)
        {
            var attr = t.GetCustomAttribute<OdinExportAttribute>();
            var tyName = t.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = (tyName.StartsWith("OdinInterop_") || tyName.StartsWith("OdinExports_")) ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var className = t.Name;
            var instName = $"_{className}";

            var exportedFns = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.Name.StartsWith("odntrop_"))
                .Where(x => x.IsAssembly || x.IsPublic)
                .ToArray();

            if (exportedFns.Length == 0)
                return;

            string OdinFnName(MethodInfo m)
            {
                if (cleanTyName == "")
                {
                    var classStem = className.StartsWith("Unity") ? className["Unity".Length..] : className;
                    return $"{classStem}_{m.Name}";
                }
                return $"{cleanTyName}{underScoreIfCleanTyName}{m.Name}";
            }

            // ── impl content: delegates, setters, wrapper logic ──
            s_StrBld.Clear();
            foreach (var exportedFn in exportedFns)
            {
                // delegate signature type
                s_StrBld.AppendIndent().AppendLine("@(private = \"file\")")
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

                // delegate global var
                s_StrBld.AppendIndent().AppendLine("@(private = \"file\")")
                    .AppendIndent()
                    .AppendLine($"odntrop_dydel_{tyName}_{exportedFn.Name}: odntrop_del_{tyName}_{exportedFn.Name} = nil")
                    .AppendLine();

                // delegate setter
                s_StrBld.AppendIndent().AppendLine("@(export, private = \"file\")")
                    .AppendIndent()
                    .AppendLine($"odntrop_export_setter_{tyName}_{exportedFn.Name} :: proc (value: odntrop_del_{tyName}_{exportedFn.Name}) {{");
                s_StrBldIndent++;
                s_StrBld.AppendIndent().AppendLine($"odntrop_dydel_{tyName}_{exportedFn.Name} = value");
                s_StrBldIndent--;
                s_StrBld.AppendIndent().AppendLine("}").AppendLine();

                // user-facing Odin wrapper
                s_StrBld.AppendIndent()
                    .Append($"{OdinFnName(exportedFn)}_impl :: proc(");

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
                s_StrBld.AppendIndent().AppendLine("odntrop_internal_tempCtx := G_OdnTrop_Internal_Ctx")
                    .AppendIndent().AppendLine("G_OdnTrop_Internal_Ctx = context")
                    .AppendIndent().AppendLine("defer G_OdnTrop_Internal_Ctx = odntrop_internal_tempCtx");

                if (exportedFn.ReturnType != typeof(void))
                {
                    s_StrBld.AppendIndent().Append("odntrop_internal_RetValXXX: ").AppendOdnTypeName(exportedFn.ReturnType).AppendLine();
                }

                s_StrBld.AppendIndent().AppendLine($"if odntrop_dydel_{tyName}_{exportedFn.Name} != nil {{");
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
                s_StrBld.AppendIndent().AppendLine("}");

                if (exportedFn.ReturnType != typeof(void))
                    s_StrBld.AppendIndent().AppendLine("return odntrop_internal_RetValXXX");

                s_StrBldIndent--;
                s_StrBld.AppendIndent().AppendLine("}").AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(attr?.odinSrcAppend ?? ""))
                s_StrBld.AppendLine(attr.odinSrcAppend);

            s_ImplBld.Append(s_StrBld.ToString());

            // ── decl wrappers (append to ExportAll decl file, no header) ──
            var fileName = tyName.StartsWith("OdinInterop_") ? tyName["OdinInterop_".Length..]
                         : tyName.StartsWith("OdinExports_") ? tyName["OdinExports_".Length..]
                         : tyName;
            var declFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"{fileName}.odin"));

            s_StrBld.Clear();

            var declSrcPath = GetCSharpSourcePath(t);
            var manifestType = FindManifestType(t);
            var docComments = (Dictionary<string, string>)manifestType?
                .GetField("OdinInteropComments", s_ManifestFieldFlags)?.GetValue(null);

            foreach (var exportedFn in exportedFns)
            {
                if (docComments != null && docComments.TryGetValue(exportedFn.Name, out var comment))
                    s_StrBld.AppendIndent().AppendLine($"// {comment}");

                var declFnLine = GetMethodLineNumber(t, exportedFn.Name);
                if (declFnLine > 0 && declSrcPath != null)
                    s_StrBld.AppendIndent().AppendLine($"// Source: file://{declSrcPath}#L{declFnLine}");

                var parms = exportedFn.GetParameters();

                s_StrBld.AppendIndent()
                    .Append($"{OdinFnName(exportedFn)} :: #force_inline proc(");

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
                s_StrBld.Append($"{OdinFnName(exportedFn)}_impl(");

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

            // Append to existing decl file
            if (File.Exists(declFile) && s_StrBld.Length > 0)
            {
                File.AppendAllText(declFile, s_StrBld.ToString());
            }
            else if (s_StrBld.Length > 0)
            {
                // No ExportAll file exists — write standalone
                var header = new StringBuilder()
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine()
                    .AppendLine("@require import \"base:runtime\"")
                    .AppendLine();
                File.WriteAllText(declFile, header.ToString() + s_StrBld.ToString());
            }
        }

        private static void GenerateImportOdinCode(Type t, string odinSrcAppend)
        {
            var tyName = t.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = (tyName.StartsWith("OdinInterop_") || tyName.StartsWith("OdinExports_")) ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var className = t.Name;
            var instName = $"_{className}";

            var importedFns = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.Name.StartsWith("odntrop_"))
                .ToArray();

            if (importedFns.Length == 0)
                return;

            var importSrcPath = GetCSharpSourcePath(t);
            var importOdinFileName = importSrcPath != null
                ? Path.GetFileNameWithoutExtension(importSrcPath)
                : tyName;
            var tgtFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_IMPORTS_DIR, $"{importOdinFileName}.odin"));

            // Collect unique Odin packages referenced by imported methods
            var odinPackages = new HashSet<string>();
            foreach (var importedFn in importedFns)
            {
                var fd = importedFn.GetCustomAttribute<ForeignDeclAttribute>();
                if (fd != null && !string.IsNullOrEmpty(fd.OdinPackage))
                    odinPackages.Add(fd.OdinPackage);
            }

            s_StrBld
                .Clear()
                .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                .AppendLine("#+vet !tabs !unused !style")
                .AppendLine("package imports")
                .AppendLine()
                .AppendLine("import src \"..\"")
                .AppendLine("import exports \"../.exports\"");

            foreach (var pkg in odinPackages)
                s_StrBld.AppendLine($"import {pkg} \"../{pkg}\"");

            s_StrBld
                .AppendLine("@require import \"base:runtime\"")
                .AppendLine();

            if (importSrcPath != null)
                s_StrBld.AppendLine($"// Source: file://{importSrcPath}").AppendLine();

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
                        .Append(importedFn.ReturnType == typeof(void) ? "" : "return ");

                    // Use the Odin package prefix from ForeignDecl, or default to "src."
                    var odinPkg = foreignDecl?.OdinPackage;
                    if (!string.IsNullOrEmpty(odinPkg))
                        s_StrBld.Append(odinPkg).Append(".");
                    else if (string.IsNullOrWhiteSpace(odinSrcAppend))
                        s_StrBld.Append("src.");

                    s_StrBld
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

        /// <summary>
        /// Generates .odin export files for ALL public instance methods of targetType.
        /// stubType is the class carrying [OdinExportAll(typeof(targetType))] — its name
        /// is used for the generated Odin file naming and tyName prefix.
        ///
        /// Overloaded methods get parameter-type suffixes (same convention as SourceGenerator).
        /// </summary>
        private static void GenerateExportAllOdinCode(Type stubType, Type targetType, string odinSrcAppend)
        {
            var tyName = stubType.FullName.Replace('+', '.').Replace('.', '_');
            var cleanTyName = (tyName.StartsWith("OdinInterop_") || tyName.StartsWith("OdinExports_")) ? "" : tyName;
            var underScoreIfCleanTyName = cleanTyName == "" ? "" : "_";
            var instName = char.ToLowerInvariant(targetType.Name[0]) + targetType.Name.Substring(1);

            // Enumerate public instance methods (same filtering as SourceGenerator).
            // Skip methods with non-Unity-Object custom-marshalled types.
            static bool IsInteropSupported(Type ts)
            {
                if (ts.IsGenericParameter) return false; // skip generic type params like T
                if (ts.IsByRefLike && !ts.Name.StartsWith("Span") && !ts.Name.StartsWith("ReadOnlySpan")) return false; // skip ref structs except Span/ReadOnlySpan
                if (ts.IsPrimitive || ts == typeof(void) || ts == typeof(string)) return true;
                if (ts.IsEnum) return true;
                if (ts.IsPointer) return IsInteropSupported(ts.GetElementType());
                if (typeof(UnityEngine.Object).IsAssignableFrom(ts)) return true;
                if (ts == typeof(Vector2) || ts == typeof(Vector3) || ts == typeof(Vector4)
                 || ts == typeof(Quaternion) || ts == typeof(Color) || ts == typeof(Color32)
                 || ts == typeof(LayerMask)) return true;
                if (ts.Namespace != null && ts.Namespace.StartsWith("Unity.Collections")) return false; // NativeArray etc.
                if (UnsafeUtility.IsUnmanaged(ts) && ts.IsValueType && !ts.IsByRefLike) return true;
                if (ts.IsGenericType)
                {
                    var gd = ts.GetGenericTypeDefinition();
                    if (gd == typeof(Slice<>) || gd == typeof(DynamicArray<>)
                     || gd == typeof(List<>) || gd == typeof(ObjectHandle<>)) return true;
                    if (gd == typeof(Span<>) || gd == typeof(ReadOnlySpan<>))
                        return IsInteropSupported(ts.GetGenericArguments()[0]);
                }
                return false;
            }

            var allMethods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => (!m.IsSpecialName || m.Name.StartsWith("get_") || m.Name.StartsWith("set_"))
                         && !m.Name.StartsWith("add_")
                         && !m.Name.StartsWith("remove_")
                         && !m.Name.StartsWith("odntrop_")
                         && m.DeclaringType != typeof(object)
                         && !Attribute.IsDefined(m, typeof(ObsoleteAttribute))
                         && m.GetParameters().All(p => p.ParameterType != typeof(IntPtr))
                         && IsInteropSupported(m.ReturnType)
                         && m.GetParameters().All(p => IsInteropSupported(p.ParameterType)))
                .ToArray();

            if (allMethods.Length == 0)
                return;

            // Compute overload-safe Odin names
            var methodGroups = allMethods.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToArray());
            var odinNames = new Dictionary<MethodInfo, string>();
            foreach (var (name, methods) in methodGroups)
            {
                if (methods.Length == 1)
                {
                    odinNames[methods[0]] = $"{targetType.Name}_{name}";
                }
                else
                {
                    foreach (var m in methods)
                    {
                        var suffix = m.GetParameters().Length == 0 ? "void" : string.Join("_", m.GetParameters().Select(p => p.Name));
                        odinNames[m] = $"{targetType.Name}_{name}_{suffix}";
                    }
                    // Detect collisions from identical parameter names — fall back to type-based suffix
                    var usedNames = new HashSet<string>();
                    foreach (var m in methods)
                    {
                        if (!usedNames.Add(odinNames[m]))
                        {
                            foreach (var mc in methods)
                            {
                                var typeSuffix = mc.GetParameters().Length == 0 ? "void" : string.Join("_", mc.GetParameters().Select(p => p.ParameterType.Name));
                                odinNames[mc] = $"{targetType.Name}_{name}_{typeSuffix}";
                            }
                            break;
                        }
                    }
                }
            }

            var fileName = tyName.StartsWith("OdinInterop_") ? tyName["OdinInterop_".Length..]
                         : tyName.StartsWith("OdinExports_") ? tyName["OdinExports_".Length..]
                         : tyName;

            // ── impl file content (accumulated into shared _impl.odin) ──

            s_StrBld.Clear();
            foreach (var m in allMethods)
            {
                var odinName = odinNames[m];

                // delegate signature type
                s_StrBld.AppendIndent().AppendLine("@(private = \"file\")")
                    .AppendIndent()
                    .Append($"odntrop_del_{tyName}_{odinName} :: #type proc \"c\" (");
                if (!m.IsStatic)
                    s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(targetType);
                var parms = m.GetParameters();
                for (int i = 0; i < parms.Length; i++)
                {
                    s_StrBld.Append(m.IsStatic && i == 0 ? "" : ", ").Append(parms[i].Name).Append(": ").AppendOdnTypeName(parms[i].ParameterType);
                }
                s_StrBld.Append(")");
                if (m.ReturnType != typeof(void))
                    s_StrBld.Append(" -> ").AppendOdnTypeName(m.ReturnType);
                s_StrBld.AppendLine().AppendLine();

                // delegate global var
                s_StrBld.AppendIndent().AppendLine("@(private = \"file\")")
                    .AppendIndent()
                    .AppendLine($"odntrop_dydel_{tyName}_{odinName}: odntrop_del_{tyName}_{odinName} = nil")
                    .AppendLine();

                // delegate setter
                s_StrBld.AppendIndent().AppendLine("@(export, private = \"file\")")
                    .AppendIndent()
                    .AppendLine($"odntrop_export_setter_{tyName}_{odinName} :: proc (value: odntrop_del_{tyName}_{odinName}) {{");
                s_StrBldIndent++;
                s_StrBld.AppendIndent().AppendLine($"odntrop_dydel_{tyName}_{odinName} = value");
                s_StrBldIndent--;
                s_StrBld.AppendIndent().AppendLine("}").AppendLine();

                // user-facing wrapper
                s_StrBld.AppendIndent()
                    .Append($"{odinName}_impl :: proc(");
                if (!m.IsStatic)
                    s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(targetType);
                for (int i = 0; i < parms.Length; i++)
                {
                    var p = parms[i];
                    s_StrBld.Append(m.IsStatic && i == 0 ? "" : ", ").Append(p.Name).Append(": ").AppendOdnTypeName(p.ParameterType);
                    if (p.HasDefaultValue)
                    {
                        if (p.ParameterType == typeof(float)) s_StrBld.Append(" = ").Append(((float)p.DefaultValue).ToString("0.0####").ToLowerInvariant());
                        else if (p.ParameterType == typeof(int)) s_StrBld.Append(" = ").Append((int)p.DefaultValue);
                        else if (p.ParameterType == typeof(bool)) s_StrBld.Append(" = ").Append(((bool)p.DefaultValue).ToString().ToLowerInvariant());
                    }
                }
                s_StrBld.Append(")");
                if (m.ReturnType != typeof(void))
                    s_StrBld.Append(" -> ").AppendOdnTypeName(m.ReturnType);

                s_StrBld.AppendLine(" {");
                s_StrBldIndent++;
                s_StrBld.AppendIndent().AppendLine("odntrop_internal_tempCtx := G_OdnTrop_Internal_Ctx")
                    .AppendIndent().AppendLine("G_OdnTrop_Internal_Ctx = context")
                    .AppendIndent().AppendLine("defer G_OdnTrop_Internal_Ctx = odntrop_internal_tempCtx");

                if (m.ReturnType != typeof(void))
                {
                    s_StrBld.AppendIndent().Append("odntrop_internal_RetValXXX: ").AppendOdnTypeName(m.ReturnType).AppendLine();
                }

                s_StrBld.AppendIndent().AppendLine($"if odntrop_dydel_{tyName}_{odinName} != nil {{");
                s_StrBldIndent++;
                s_StrBld.AppendIndent();
                if (m.ReturnType != typeof(void))
                    s_StrBld.Append("odntrop_internal_RetValXXX = ");
                s_StrBld.Append($"odntrop_dydel_{tyName}_{odinName}(");
                if (!m.IsStatic)
                    s_StrBld.Append(instName);
                for (int i = 0; i < parms.Length; i++)
                    s_StrBld.Append((!m.IsStatic || i > 0) ? ", " : "").Append(parms[i].Name);
                s_StrBld.AppendLine(")");
                s_StrBldIndent--;
                s_StrBld.AppendIndent().AppendLine("}");

                if (m.ReturnType != typeof(void))
                    s_StrBld.AppendIndent().AppendLine("return odntrop_internal_RetValXXX");

                s_StrBldIndent--;
                s_StrBld.AppendIndent().AppendLine("}").AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(odinSrcAppend))
                s_StrBld.AppendLine(odinSrcAppend);

            s_ImplBld.Append(s_StrBld.ToString());

            // ── decl file: #force_inline forwarding wrappers ──
            {
                var declFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"{fileName}.odin"));
                s_StrBld.Clear()
                    .AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                    .AppendLine("#+vet !tabs !unused !style")
                    .AppendLine("package exports")
                    .AppendLine()
                    .AppendLine("@require import \"base:runtime\"")
                    .AppendLine();

                // No source links for ExportAll — the methods come from the target type, not the stub file.

                foreach (var m in allMethods)
                {
                    var odinName = odinNames[m];

                    var parms2 = m.GetParameters();
                    s_StrBld.AppendIndent().Append($"{odinName} :: #force_inline proc(");
                    if (!m.IsStatic)
                        s_StrBld.Append(instName).Append(": ").AppendOdnTypeName(targetType);
                    for (int i = 0; i < parms2.Length; i++)
                        s_StrBld.Append((m.IsStatic && i == 0) ? "" : ", ").Append(parms2[i].Name).Append(": ").AppendOdnTypeName(parms2[i].ParameterType);
                    s_StrBld.Append(")");
                    if (m.ReturnType != typeof(void)) s_StrBld.Append(" -> ").AppendOdnTypeName(m.ReturnType);
                    s_StrBld.AppendLine(" {");
                    s_StrBldIndent++;
                    s_StrBld.AppendIndent();
                    if (m.ReturnType != typeof(void)) s_StrBld.Append("return ");
                    s_StrBld.Append($"{odinName}_impl(");
                    if (!m.IsStatic)
                        s_StrBld.Append(instName);
                    for (int i = 0; i < parms2.Length; i++)
                        s_StrBld.Append((!m.IsStatic || i > 0) ? ", " : "").Append(parms2[i].Name);
                    s_StrBld.AppendLine(")");
                    s_StrBldIndent--;
                    s_StrBld.AppendIndent().AppendLine("}").AppendLine();
                }

                // Emit parapoly groups for overloaded methods so users can call
                // the base name and Odin dispatches to the right overload by type.
                foreach (var kvp in methodGroups)
                {
                    if (kvp.Value.Length <= 1) continue;
                    var overloadNames = kvp.Value.Select(m => odinNames[m]);
                    s_StrBld.AppendIndent()
                        .Append(targetType.Name).Append("_").Append(kvp.Key)
                        .Append(" :: proc{")
                        .Append(string.Join(", ", overloadNames))
                        .AppendLine("}")
                        .AppendLine();
                }

                File.WriteAllText(declFile, s_StrBld.ToString());
            }
        }

        /// <summary>
        /// After GenerateExportAllOdinCode runs, resolves type definitions for all types
        /// discovered during the codegen pass and writes them into the class-specific
        /// .odin file (e.g., .exports/ParticleSystem.odin) alongside the method wrappers.
        /// These types are also removed from the global namespace tracking so they
        /// don't appear in namespace-based files.
        /// </summary>
        private static void ResolveExportAllClassTypes(Type stubType, HashSet<Type> preExistingTypes)
        {
            // Collect seed types: types newly added to s_ExportedTypesFlat
            // during this ExportAll invocation.
            var seedTypes = new HashSet<Type>();
            foreach (var exportedType in s_ExportedTypesFlat)
                if (!preExistingTypes.Contains(exportedType))
                    seedTypes.Add(exportedType);

            if (seedTypes.Count == 0)
                return;

            // Remove seed types from global namespace tracking —
            // they will be written to the class-specific .odin file instead.
            foreach (var exportedType in seedTypes)
            {
                var fileKey = GetNamespaceFileName(exportedType);
                if (s_ExportedTypesByNamespace.TryGetValue(fileKey, out var nsSet))
                    nsSet.Remove(exportedType);
            }

            // Recursively resolve type definitions.
            // AppendOdnTypeDef may discover new types (field types of structs)
            // via AppendOdnTypeName → AddExportedType, so we iterate until stable.
            var typeDefBuilder = new StringBuilder(8192);
            var queue = new Queue<Type>(seedTypes);
            var resolvedInThisPass = new HashSet<Type>();

            while (queue.Count > 0)
            {
                var resolveType = queue.Dequeue();
                if (s_HandledTypes.Contains(resolveType) || resolvedInThisPass.Contains(resolveType))
                    continue;

                // Snapshot before resolution to catch recursively-discovered types
                var preResolve = new HashSet<Type>(s_ExportedTypesFlat);

                var savedSb = s_StrBld;
                var savedIndent = s_StrBldIndent;
                s_StrBld = typeDefBuilder;
                s_StrBldIndent = 0;
                s_StrBld.AppendOdnTypeDef(resolveType);
                s_StrBld = savedSb;
                s_StrBldIndent = savedIndent;

                resolvedInThisPass.Add(resolveType);

                // Enqueue any new types discovered during resolution
                // (e.g. field types of structs, base types of Unity Objects)
                foreach (var newType in s_ExportedTypesFlat)
                    if (!preResolve.Contains(newType) && !resolvedInThisPass.Contains(newType))
                        queue.Enqueue(newType);
            }

            // Remove recursively-discovered types from global namespace tracking too
            foreach (var resolvedType in resolvedInThisPass)
            {
                if (!seedTypes.Contains(resolvedType))
                {
                    var fileKey = GetNamespaceFileName(resolvedType);
                    if (s_ExportedTypesByNamespace.TryGetValue(fileKey, out var nsSet))
                        nsSet.Remove(resolvedType);
                }
            }

            // Rebuild the decl file: header + type defs + method wrappers
            var tyName = stubType.FullName.Replace('+', '.').Replace('.', '_');
            var fileName = tyName.StartsWith("OdinInterop_") ? tyName["OdinInterop_".Length..]
                         : tyName.StartsWith("OdinExports_") ? tyName["OdinExports_".Length..]
                         : tyName;
            var declFile = Path.GetFullPath(Path.Combine(ODIN_INTEROP_EXPORTS_DIR, $"{fileName}.odin"));

            if (File.Exists(declFile))
            {
                var lines = File.ReadAllLines(declFile);

                // Skip header lines to extract method wrappers
                int skip = 0;
                while (skip < lines.Length && (
                    lines[skip].StartsWith("//") ||
                    lines[skip].StartsWith("#+vet") ||
                    lines[skip].StartsWith("package ") ||
                    lines[skip].StartsWith("@require") ||
                    lines[skip].Trim().Length == 0))
                    skip++;

                var rebuilder = new StringBuilder(8192 + typeDefBuilder.Length);
                rebuilder.AppendLine("// THIS IS A GENERATED FILE - DO NOT MODIFY OR YOUR CHANGES WILL BE LOST!")
                        .AppendLine("#+vet !tabs !unused !style")
                        .AppendLine("package exports")
                        .AppendLine()
                        .AppendLine("@require import \"base:runtime\"")
                        .AppendLine();
                rebuilder.Append(typeDefBuilder.ToString());

                for (int i = skip; i < lines.Length; i++)
                    rebuilder.AppendLine(lines[i]);

                File.WriteAllText(declFile, rebuilder.ToString());
            }
        }

        private static HashSet<Type> s_HandledTypes = new HashSet<Type>(256);
        private static readonly MethodInfo s_AlignOfMethod = typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AlignOf), BindingFlags.Public | BindingFlags.Static);
        private static StringBuilder AppendOdnTypeDef(this StringBuilder sb, Type t)
        {
            if (s_HandledTypes.Contains(t))
                return sb;

            if (t.IsGenericParameter)
            {
                s_HandledTypes.Add(t);
                return sb; // skip generic type parameters — they have no Odin representation
            }

            if (t == typeof(UnityEngine.Object))
            {
                s_HandledTypes.Add(t);
                return sb.AppendIndent().AppendLine("Object :: struct { id: i32 }").AppendLine();
            }

            var resolvedName = t.GetResolvedOdnTypeName();

            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                AddExportedType(t.BaseType);

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
                try
                {
                    sb.AppendIndent().AppendLine($"#assert(align_of({resolvedName}) == {(int)s_AlignOfMethod.MakeGenericMethod(t).Invoke(null, null)}, \"Align mismatch for {resolvedName}!\")");
                }
                catch
                {
                    // AlignOf fails on types with ref-like fields; skip the assert
                }


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

        private static string GetNamespaceFileName(Type t)
        {
            var ns = t.Namespace;
            if (string.IsNullOrEmpty(ns))
                return t.Name; // global-namespace types get their own file
            return ns.Replace('.', '_');
        }

        private static readonly Dictionary<Type, string> s_OdnTypeNameCache = new Dictionary<Type, string>(256);
        private static string GetResolvedOdnTypeName(this Type t)
        {
            if (s_OdnTypeNameCache.TryGetValue(t, out var cachedName))
                return cachedName;

            var isSpecialNamespace = true;
            var resolvedName = t.FullName?.Replace('+', '.').Replace('.', '_') ?? t.Name;
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
            else if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(Span<>) || t.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>)))
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
                AddExportedType(t);
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
