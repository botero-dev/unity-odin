using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OdinInterop.SourceGenerator
{
    [Generator]
    public class InteropGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext ctx)
        {
            ctx.RegisterForSyntaxNotifications(() => new Receiver());
        }

        public void Execute(GeneratorExecutionContext ctx)
        {
            if (!(ctx.SyntaxReceiver is Receiver rx)) return;

            var compilation = ctx.Compilation;

            var sb = new StringBuilder();
            foreach (var classDeclaration in rx.candidateClasses)
            {
                var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(classDeclaration);

                if (classSymbol == null)
                    continue;

                var attrs = classSymbol.GetAttributes();
                var hasExport = attrs.Any(a => a.AttributeClass?.GetFullTypeName() == "OdinInterop.OdinExportAttribute");
                var hasImport = attrs.Any(a => a.AttributeClass?.GetFullTypeName() == "OdinInterop.OdinImportAttribute");
                var hasExportAll = attrs.Any(a => a.AttributeClass?.GetFullTypeName() == "OdinInterop.OdinExportAllAttribute");

                if (!hasExport && !hasImport && !hasExportAll)
                    continue;

                sb.Clear();

                if (hasExportAll)
                {
                    var exportAllAttr = attrs.First(a => a.AttributeClass?.GetFullTypeName() == "OdinInterop.OdinExportAllAttribute");
                    var targetTypeSymbol = exportAllAttr.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                    if (targetTypeSymbol != null)
                        GenerateExportAllCode(sb, classSymbol, targetTypeSymbol);
                }
                else
                {
                    var mode = hasExport ? InteropMode.Export : InteropMode.Import;
                    GenerateInteropCode(sb, classSymbol, mode);
                }

                if (sb.Length != 0)
                {
                    var fileName = $"{classSymbol.GetFullTypeName()}.g.cs";
                    ctx.AddSource(fileName, SourceText.From(sb.ToString(), Encoding.UTF8));
                }
                sb.Clear();
            }
        }

        private enum InteropMode
        {
            Export,  // C# -> Odin: only process exported methods
            Import   // Odin -> C#: only process imported methods
        }

        private void GenerateInteropCode(StringBuilder sb, INamedTypeSymbol classSymbol, InteropMode mode)
        {
            var sbIndent = 0;

            var tyName = classSymbol.GetFullTypeName().Replace('.', '_');
            var instTypeName = classSymbol.Name;
            var instParamName = $"_{instTypeName}";

            List<IMethodSymbol> exportedMethods;
            List<IMethodSymbol> importedMethods;

            if (mode == InteropMode.Export)
            {
                exportedMethods = classSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary &&
                               !m.Name.StartsWith("odntrop_"))
                    .ToList();

                if (classSymbol.IsStatic)
                    exportedMethods = exportedMethods.Where(m => m.DeclaredAccessibility == Accessibility.Internal ||
                                                                  m.DeclaredAccessibility == Accessibility.Public).ToList();
                else
                    exportedMethods = exportedMethods.Where(m => m.DeclaredAccessibility == Accessibility.Internal ||
                                                                  m.DeclaredAccessibility == Accessibility.Public).ToList();

                importedMethods = new List<IMethodSymbol>();
            }
            else // Import
            {
                exportedMethods = new List<IMethodSymbol>();

                importedMethods = classSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary &&
                               m.IsPartialDefinition &&
                               m.DeclaredAccessibility == Accessibility.Public &&
                               !m.Name.StartsWith("odntrop_"))
                    .ToList() ?? new List<IMethodSymbol>();
            }

            sb.AppendLine("using OdinInterop;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("#endif");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            // namespace
            if (!string.IsNullOrEmpty(classSymbol.ContainingNamespace?.Name) &&
                classSymbol.ContainingNamespace.Name != "<global namespace>")
            {
                sb.AppendLine($"namespace {classSymbol.ContainingNamespace}");
                sb.AppendLine("{");
                sbIndent++;
            }

            sb.AppendIndent(sbIndent);

            switch (classSymbol.DeclaredAccessibility)
            {
                case Accessibility.NotApplicable:
                    sb.Append("");
                    break;
                case Accessibility.Private:
                    sb.Append("private ");
                    break;
                case Accessibility.Internal:
                    sb.Append("internal ");
                    break;
                case Accessibility.Public:
                    sb.Append("public ");
                    break;
                default:
                    sb.Append("UNSUPPORTED_ACCESSIBILITY ");
                    break;
            }

            var generatedClassName = classSymbol.IsStatic ? classSymbol.Name : $"{classSymbol.Name}_OdinInterop";
            sb.AppendLine($"static unsafe partial class {generatedClassName}");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;

            // dll name
            sb.AppendIndent(sbIndent).AppendLine("private const string k_OdinInteropDllName = ");
            sbIndent++;
            sb.AppendLine("#if UNITY_IOS && !UNITY_EDITOR");
            sb.AppendIndent(sbIndent).AppendLine("\"__Internal\";");
            sb.AppendLine("#else");
            sb.AppendIndent(sbIndent).AppendLine("\"OdinInterop\";");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sbIndent--;

            // Helper: append instance parameter with comma handling
            void AppendInstanceParam(bool hasMoreParams)
            {
                sb.Append("RawObjectHandle ").Append(instParamName);
                if (hasMoreParams) sb.Append(", ");
            }

            // generate some delegates for exported methods
            foreach (var method in exportedMethods)
            {
                sb.AppendIndent(sbIndent).Append("private delegate ");
                sb.AppendReturnType(method, true);
                sb.Append($" odntrop_del_{method.Name}(");
                if (!method.IsStatic) { AppendInstanceParam(method.Parameters.Length > 0); }
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(");");
                sb.AppendLine();

                // Delegate for setter
                sb.AppendIndent(sbIndent)
                    .Append("private delegate void odntrop_del_Set")
                    .Append(method.Name)
                    .Append("Delegate(odntrop_del_")
                    .Append(method.Name)
                    .AppendLine(" value);")
                    .AppendLine();
            }

            // generate some delegates for imported methods
            foreach (var method in importedMethods)
            {
                sb.AppendIndent(sbIndent).Append("private delegate ");
                sb.AppendReturnType(method, true);
                sb.Append($" odntrop_del_{method.Name}(");
                if (!method.IsStatic) { AppendInstanceParam(method.Parameters.Length > 0); }
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(");");
                sb.AppendLine();
            }

            // set up p/invoke friendly-wrappers for exported functions
            foreach (var method in exportedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("[AOT.MonoPInvokeCallback(typeof(odntrop_del_")
                    .Append(method.Name)
                    .AppendLine("))]")
                    .AppendIndent(sbIndent)
                    .Append("private static ")
                    .AppendReturnType(method, true)
                    .Append(" odntrop_exported_")
                    .Append(method.Name)
                    .Append("(");
                if (!method.IsStatic) { sb.Append("RawObjectHandle ").Append(instParamName); if (method.Parameters.Length > 0) sb.Append(", "); }
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(")");
                sb.AppendIndent(sbIndent).AppendLine("{");
                sbIndent++;

                // instance conversion for non-static methods
                if (!method.IsStatic)
                {
                    sb.AppendIndent(sbIndent)
                        .AppendTypeName(classSymbol, false)
                        .Append(" ")
                        .Append(instParamName)
                        .Append("_odntrop_internal_proxy = (")
                        .AppendTypeName(classSymbol, false)
                        .Append(")(OdinInterop.ObjectHandle<")
                        .AppendTypeName(classSymbol, false)
                        .Append(">)")
                        .Append(instParamName)
                        .AppendLine(";");
                }

                // conversions to non-interoperable (if any)
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    switch (par.Type.GetInteroperabilityType())
                    {
                        case InteroperabilityType.Blittable:
                            // no conversion needed
                            break;
                        case InteroperabilityType.AutoConverting:
                            // by val will be copied
                            // by in will be copied (special case in AppendParameters)
                            // by out will be created (special case in AppendParameters)
                            if (rk == RefKind.Ref)
                            {
                                sb.AppendIndent(sbIndent)
                                    .AppendTypeName(par.Type, false)
                                    .Append(" ")
                                    .Append(par.Name)
                                    .Append("_odntrop_internal_proxy")
                                    .Append(" = ")
                                    .Append(par.Name)
                                    .AppendLine(";"); // direct assignment
                            }

                            break;
                        case InteroperabilityType.CustomMarshalled:
                            if (par.Type.IsUnityObject())
                            {
                                // Unity Objects: convert RawObjectHandle → original type
                                if (rk == RefKind.None || rk == RefKind.Ref || rk == RefKind.In)
                                {
                                    sb.AppendIndent(sbIndent)
                                        .AppendTypeName(par.Type, false)
                                        .Append(" ")
                                        .Append(par.Name)
                                        .Append("_odntrop_internal_proxy")
                                        .Append(" = (")
                                        .AppendTypeName(par.Type, false)
                                        .Append(")(OdinInterop.ObjectHandle<")
                                        .AppendTypeName(par.Type, false)
                                        .Append(">)")
                                        .AppendLine(";");
                                }
                                else if (rk == RefKind.Out)
                                {
                                    sb.AppendIndent(sbIndent)
                                        .AppendTypeName(par.Type, false)
                                        .Append(" ")
                                        .Append(par.Name)
                                        .Append("_odntrop_internal_proxy")
                                        .AppendLine(" = default;");
                                }
                            }
                            else
                            {
                                // TODO handle non-Unity-Object custom marshalled types
                            }
                            break;
                    }
                }

                // actual method call
                sb.AppendIndent(sbIndent);
                if (!method.ReturnsVoid)
                {
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                    sb.Append("var odntrop_internal_RetValXXX = ");
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                }
                if (!method.IsStatic)
                {
                    sb.Append(instParamName).Append("_odntrop_internal_proxy.").Append(method.Name).Append("(");
                }
                else
                {
                    sb.Append(method.Name).Append("(");
                }
                sb.AppendParameters(method.Parameters, null);
                sb.AppendLine(");");

                // conversions to non-interoperable (if any)
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    switch (par.Type.GetInteroperabilityType())
                    {
                        case InteroperabilityType.Blittable:
                            // no conversion needed
                            break;
                        case InteroperabilityType.AutoConverting:
                            if (rk == RefKind.Out || rk == RefKind.Ref)
                            {
                                sb.AppendIndent(sbIndent)
                                    .Append(par.Name)
                                    .Append(" = ")
                                    .Append(par.Name)
                                    .AppendLine("_odntrop_internal_proxy;");
                            }

                            break;
                        case InteroperabilityType.CustomMarshalled:
                            if (par.Type.IsUnityObject())
                            {
                                // Unity Objects: convert back from original type → RawObjectHandle
                                if (rk == RefKind.Ref || rk == RefKind.Out)
                                {
                                    sb.AppendIndent(sbIndent)
                                        .Append(par.Name)
                                        .Append(" = (OdinInterop.RawObjectHandle)(OdinInterop.ObjectHandle<")
                                        .AppendTypeName(par.Type, false)
                                        .Append(">)")
                                        .Append(par.Name)
                                        .AppendLine("_odntrop_internal_proxy;");
                                }
                            }
                            else
                            {
                                // TODO handle non-Unity-Object custom marshalled types
                            }
                            break;
                    }
                }

                // return statement
                if (!method.ReturnsVoid)
                {
                    sb.AppendIndent(sbIndent);
                    sb.Append("return ");
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                    sb.AppendLine("odntrop_internal_RetValXXX;");
                    // TODO handle non-converting and non-blittable
                }

                sbIndent--;
                sb.AppendIndent(sbIndent).AppendLine("}");
                sb.AppendLine();
            }

            // editor-specific code (hot-reload style)
            sb.AppendLine("#if UNITY_EDITOR || ODININTEROP_RUNTIME_RELOADING");
            sb.AppendLine();

            // global var for delegate
            foreach (var method in importedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("private static odntrop_del_")
                    .Append(method.Name)
                    .Append(" odntrop_delref_")
                    .Append(method.Name)
                    .AppendLine(";");
                sb.AppendLine();
            }

            // editor initialization
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendIndent(sbIndent).AppendLine("[InitializeOnLoadMethod]");
            sb.AppendLine("#else");
            sb.AppendIndent(sbIndent).AppendLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]");
            sb.AppendLine("#endif");
            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_EditorInit()");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;
            sb.AppendIndent(sbIndent).AppendLine("OdinCompilerUtils.onHotReload += odntrop_OnHotReload;");
            sb.AppendIndent(sbIndent).AppendLine("if (OdinCompilerUtils.initialisedAfterDomainReload) odntrop_OnHotReload(OdinCompilerUtils.libraryHandle);");
            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}").AppendLine();

            // on hot reload
            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_OnHotReload(IntPtr libraryHandle)");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;

            // import functions
            foreach (var method in importedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("odntrop_delref_")
                    .Append(method.Name)
                    .Append(" = libraryHandle == IntPtr.Zero ? null : LibraryUtils.GetDelegate<odntrop_del_")
                    .Append(method.Name)
                    .Append(">(libraryHandle, \"odntrop_export_")
                    .Append(tyName)
                    .Append("_")
                    .Append(method.Name)
                    .AppendLine("\");");
            }

            sb.AppendLine();
            sb.AppendIndent(sbIndent).AppendLine("if (libraryHandle == IntPtr.Zero) return;");
            sb.AppendLine();

            // set exported delegates
            foreach (var method in exportedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("LibraryUtils.GetDelegate<odntrop_del_Set")
                    .Append(method.Name)
                    .Append("Delegate>(libraryHandle, \"odntrop_export_setter_")
                    .Append(tyName)
                    .Append("_")
                    .Append(method.Name)
                    .Append("\")?.Invoke(odntrop_exported_")
                    .Append(method.Name)
                    .AppendLine(");");
            }

            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");
            sb.AppendLine();

            // runtime code (bindings)
            sb.AppendLine("#elif !ODININTEROP_DISABLED");
            sb.AppendLine();

            // p/invoke declarations for imported functions
            foreach (var method in importedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("[DllImport(k_OdinInteropDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = \"odntrop_export_")
                    .Append(tyName)
                    .Append("_")
                    .Append(method.Name)
                    .AppendLine("\")]")
                    .AppendIndent(sbIndent)
                    .Append("private static extern ")
                    .AppendReturnType(method, true)
                    .Append(" odntrop_delref_")
                    .Append(method.Name)
                    .Append("(");
                if (!method.IsStatic) { sb.Append("RawObjectHandle ").Append(instParamName); if (method.Parameters.Length > 0) sb.Append(", "); }
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(");");
                sb.AppendLine();
            }

            // exported function setters
            foreach (var method in exportedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("[DllImport(k_OdinInteropDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = \"odntrop_export_setter_")
                    .Append(tyName)
                    .Append("_")
                    .Append(method.Name)
                    .AppendLine("\")]")
                    .AppendIndent(sbIndent)
                    .Append("private static extern void odntrop_set_")
                    .Append(method.Name)
                    .Append("(odntrop_del_")
                    .Append(method.Name)
                    .AppendLine(" value);");
                sb.AppendLine();
            }

            // runtime initialization
            sb.AppendIndent(sbIndent).AppendLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]");
            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_RuntimeInit()");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;

            foreach (var method in exportedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("odntrop_set_")
                    .Append(method.Name)
                    .Append("(odntrop_exported_")
                    .Append(method.Name)
                    .AppendLine(");");
            }

            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("#endif");
            sb.AppendLine();

            // wrapper for the user to call
            foreach (var method in importedMethods)
            {
                sb.AppendIndent(sbIndent)
                    .Append("public static partial ")
                    .AppendReturnType(method, false)
                    .Append(" ")
                    .Append(method.Name)
                    .Append("(");
                if (!method.IsStatic) { sb.AppendTypeName(classSymbol, false).Append(" ").Append(instParamName); if (method.Parameters.Length > 0) sb.Append(", "); }
                sb.AppendParameters(method.Parameters, false);
                sb.AppendLine(")");
                sb.AppendIndent(sbIndent).AppendLine("{");
                sbIndent++;

                sb.AppendLine("#if ODININTEROP_DISABLED");

                sb.AppendLine()
                    .AppendFailedReturn(method, sbIndent)
                    .AppendLine();

                sb.AppendLine("#else").AppendLine();

                sb.AppendLine("#if UNITY_EDITOR || ODININTEROP_RUNTIME_RELOADING");

                sb.AppendIndent(sbIndent)
                    .Append("if (odntrop_delref_")
                    .Append(method.Name)
                    .AppendLine(" == null)")
                    .AppendIndent(sbIndent)
                    .AppendLine("{");

                sbIndent++;
                sb.AppendFailedReturn(method, sbIndent);
                sbIndent--;

                sb.AppendIndent(sbIndent)
                    .AppendLine("}")
                    .AppendLine("#endif")
                    .AppendLine();

                // instance proxy conversion for non-static imported methods
                if (!method.IsStatic)
                {
                    sb.AppendIndent(sbIndent)
                        .Append("OdinInterop.RawObjectHandle ")
                        .Append(instParamName)
                        .Append("_odntrop_internal_proxy = (OdinInterop.RawObjectHandle)(OdinInterop.ObjectHandle<")
                        .AppendTypeName(classSymbol, false)
                        .Append(">)")
                        .Append(instParamName)
                        .AppendLine(";");
                }

                // proxy declarations for imported method call (convert to interop types)
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    if (par.Type.IsUnityObject())
                    {
                        if (rk == RefKind.None || rk == RefKind.Ref || rk == RefKind.In)
                        {
                            sb.AppendIndent(sbIndent)
                                .Append("OdinInterop.RawObjectHandle ")
                                .Append(par.Name)
                                .Append("_odntrop_internal_proxy = (OdinInterop.RawObjectHandle)(OdinInterop.ObjectHandle<")
                                .AppendTypeName(par.Type, false)
                                .Append(">)")
                                .Append(par.Name)
                                .AppendLine(";");
                        }
                        else if (rk == RefKind.Out)
                        {
                            sb.AppendIndent(sbIndent)
                                .Append("OdinInterop.RawObjectHandle ")
                                .Append(par.Name)
                                .Append("_odntrop_internal_proxy")
                                .AppendLine(" = default;");
                        }
                    }
                }

                sb.AppendIndent(sbIndent);
                if (!method.ReturnsVoid)
                    sb.Append("return ");
                if (method.ReturnsByRef)
                    sb.Append("ref ");
                else if (method.ReturnsByRefReadonly)
                    sb.Append("ref readonly ");
                sb.Append("odntrop_delref_")
                    .Append(method.Name)
                    .Append("(");
                if (!method.IsStatic) { sb.Append(instParamName).Append("_odntrop_internal_proxy"); if (method.Parameters.Length > 0) sb.Append(", "); }
                sb.AppendParameters(method.Parameters, null)
                    .AppendLine(");");

                // proxy copy-back for imported method call (convert back from interop types)
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    if (par.Type.IsUnityObject())
                    {
                        if (rk == RefKind.Ref || rk == RefKind.Out)
                        {
                            sb.AppendIndent(sbIndent)
                                .Append(par.Name)
                                .Append(" = (")
                                .AppendTypeName(par.Type, false)
                                .Append(")(OdinInterop.ObjectHandle<")
                                .AppendTypeName(par.Type, false)
                                .Append(">)")
                                .Append(par.Name)
                                .AppendLine("_odntrop_internal_proxy;");
                        }
                    }
                }

                sb.AppendLine("#endif");

                sbIndent--;
                sb.AppendIndent(sbIndent).AppendLine("}");
                sb.AppendLine();
            }

            // ── interop manifest: source locations for generated .odin files ──
            // Emitted here because Roslyn symbols carry exact file/line info for free.
            // InteropGenerator reads these via reflection at editor time to annotate
            // the generated .odin declarations with `// Source: file://...#L42` comments,
            // so users can jump from an Odin binding straight to the C# definition.
            {
                var sourceFilePath = classSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath?.Replace('\\', '/') ?? "";
                sb.AppendLine();
                sb.AppendIndent(sbIndent).Append("internal const string OdinInteropSourcePath = \"")
                    .Append(sourceFilePath).AppendLine("\";");

                var allMethods = exportedMethods.Concat(importedMethods).ToList();
                if (allMethods.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendIndent(sbIndent).AppendLine(
                        "internal static readonly System.Collections.Generic.Dictionary<string, int> OdinInteropMethodLines = new() {");
                    sbIndent++;
                    foreach (var method in allMethods)
                    {
                        var loc = method.Locations.FirstOrDefault();
                        var line = loc?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
                        sb.AppendIndent(sbIndent)
                            .Append("[\"").Append(method.Name).Append("\"] = ").Append(line).AppendLine(",");
                    }
                    sbIndent--;
                    sb.AppendIndent(sbIndent).AppendLine("};");

                    // Emit doc-comment summaries so InteropGenerator can produce // comments in .odin files
                    var docComments = allMethods
                        .Select(m => (name: m.Name, summary: GetDocSummary(m)))
                        .Where(x => x.summary != null)
                        .ToList();
                    if (docComments.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendIndent(sbIndent).AppendLine(
                            "internal static readonly System.Collections.Generic.Dictionary<string, string> OdinInteropComments = new() {");
                        sbIndent++;
                        foreach (var (name, summary) in docComments)
                        {
                            sb.AppendIndent(sbIndent)
                                .Append("[\"").Append(name).Append("\"] = \"")
                                .Append(EscapeForCSharp(summary)).AppendLine("\",");
                        }
                        sbIndent--;
                        sb.AppendIndent(sbIndent).AppendLine("};");
                    }
                }
            }

            // close class
            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");

            // close namespace
            if (!string.IsNullOrEmpty(classSymbol.ContainingNamespace?.Name) &&
                classSymbol.ContainingNamespace.Name != "<global namespace>")
            {
                sbIndent--;
                sb.AppendLine("}");
            }
        }

        /// <summary>
        /// Generates export bindings for ALL public instance methods of targetTypeSymbol.
        /// The stub class (classSymbol) carries [OdinExportAll(typeof(Target))] and is empty —
        /// all method info is derived from targetTypeSymbol via Roslyn.
        ///
        /// Overloaded methods get a suffix derived from parameter interop type names
        /// so each overload has a unique Odin name (e.g. Emit, Emit_i32, Emit_Particle).
        /// Methods from System.Object (ToString, Equals, etc.) and property accessors are skipped.
        /// </summary>
        private void GenerateExportAllCode(StringBuilder sb, INamedTypeSymbol classSymbol, INamedTypeSymbol targetTypeSymbol)
        {
            var sbIndent = 0;
            var tyName = classSymbol.GetFullTypeName().Replace('.', '_');
            var instParamName = $"_{targetTypeSymbol.Name}";

            // Enumerate public instance methods of the target type.
            // Skip methods with non-Unity-Object custom-marshalled types (no interop mapping exists yet).
            bool IsInteropSupported(ITypeSymbol ts) =>
                (ts.GetInteroperabilityType() != InteroperabilityType.CustomMarshalled || ts.IsUnityObject())
                && !ts.GetFullTypeName().StartsWith("Unity.Collections.Native"); // NativeArray, NativeList, etc.

            var allMethods = targetTypeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary
                         && !m.IsStatic
                         && m.DeclaredAccessibility == Accessibility.Public
                         && !m.Name.StartsWith("get_")
                         && !m.Name.StartsWith("set_")
                         && !m.Name.StartsWith("add_")
                         && !m.Name.StartsWith("remove_")
                         && !m.Name.StartsWith("odntrop_")
                         && m.ContainingType.SpecialType != SpecialType.System_Object
                         && !m.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute")
                         && m.Parameters.All(p => p.RefKind == RefKind.None || p.RefKind == RefKind.In) // skip ref/out params for now
                         && IsInteropSupported(m.ReturnType)
                         && m.Parameters.All(p => IsInteropSupported(p.Type)))
                .ToList();

            // Compute overload-safe Odin names. Methods with unique names keep the name;
            // overloaded methods get a suffix derived from parameter interop type names.
            var methodGroups = allMethods.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToList());
            var odinNames = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
            foreach (var kvp in methodGroups)
            {
                var name = kvp.Key;
                var methods = kvp.Value;
                if (methods.Count == 1)
                {
                    odinNames[methods[0]] = $"{targetTypeSymbol.Name}_{name}";
                }
                else
                {
                    foreach (var m in methods)
                    {
                        var suffix = m.Parameters.Length == 0 ? "void" : string.Join("_", m.Parameters.Select(p => p.Name));
                        odinNames[m] = $"{targetTypeSymbol.Name}_{name}_{suffix}";
                    }
                }
            }

            sb.AppendLine("using OdinInterop;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("#endif");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            // namespace
            if (!string.IsNullOrEmpty(classSymbol.ContainingNamespace?.Name) &&
                classSymbol.ContainingNamespace.Name != "<global namespace>")
            {
                sb.AppendLine($"namespace {classSymbol.ContainingNamespace}");
                sb.AppendLine("{");
                sbIndent++;
            }

            sb.AppendIndent(sbIndent).Append("internal ");
            sb.AppendLine($"static unsafe partial class {classSymbol.Name}");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;

            // dll name
            sb.AppendIndent(sbIndent).AppendLine("private const string k_OdinInteropDllName = ");
            sbIndent++;
            sb.AppendLine("#if UNITY_IOS && !UNITY_EDITOR");
            sb.AppendIndent(sbIndent).AppendLine("\"__Internal\";");
            sb.AppendLine("#else");
            sb.AppendIndent(sbIndent).AppendLine("\"OdinInterop\";");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sbIndent--;

            // Helper: append instance parameter
            void AppendInstanceParam(bool hasMoreParams)
            {
                sb.Append("RawObjectHandle ").Append(instParamName);
                if (hasMoreParams) sb.Append(", ");
            }

            // generate delegates for all methods
            var exportedMethods = allMethods; // all are exported
            foreach (var method in exportedMethods)
            {
                var odinName = odinNames[method];
                sb.AppendIndent(sbIndent).Append("private delegate ");
                sb.AppendReturnType(method, true);
                sb.Append($" odntrop_del_{odinName}(");
                AppendInstanceParam(method.Parameters.Length > 0);
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendIndent(sbIndent)
                    .Append("private delegate void odntrop_del_Set")
                    .Append(odinName)
                    .Append("Delegate(odntrop_del_")
                    .Append(odinName)
                    .AppendLine(" value);")
                    .AppendLine();
            }

            // P/Invoke wrappers for each method
            foreach (var method in exportedMethods)
            {
                var odinName = odinNames[method];
                sb.AppendIndent(sbIndent)
                    .Append("[AOT.MonoPInvokeCallback(typeof(odntrop_del_")
                    .Append(odinName)
                    .AppendLine("))]")
                    .AppendIndent(sbIndent)
                    .Append("private static ")
                    .AppendReturnType(method, true)
                    .Append(" odntrop_exported_")
                    .Append(odinName)
                    .Append("(");
                sb.Append("RawObjectHandle ").Append(instParamName);
                if (method.Parameters.Length > 0) sb.Append(", ");
                sb.AppendParameters(method.Parameters, true);
                sb.AppendLine(")");
                sb.AppendIndent(sbIndent).AppendLine("{");
                sbIndent++;

                // Instance conversion: RawObjectHandle → target type
                sb.AppendIndent(sbIndent)
                    .AppendTypeName(targetTypeSymbol, false)
                    .Append(" ")
                    .Append(instParamName)
                    .Append("_proxy = (")
                    .AppendTypeName(targetTypeSymbol, false)
                    .Append(")(OdinInterop.ObjectHandle<")
                    .AppendTypeName(targetTypeSymbol, false)
                    .Append(">)")
                    .Append(instParamName)
                    .AppendLine(";");

                // Auto-converting parameter proxies
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    switch (par.Type.GetInteroperabilityType())
                    {
                        case InteroperabilityType.AutoConverting:
                            if (rk == RefKind.Ref)
                            {
                                sb.AppendIndent(sbIndent)
                                    .AppendTypeName(par.Type, false)
                                    .Append(" ").Append(par.Name)
                                    .Append("_odntrop_internal_proxy = ")
                                    .Append(par.Name).AppendLine(";");
                            }
                            break;
                        case InteroperabilityType.CustomMarshalled:
                            if (par.Type.IsUnityObject())
                            {
                                if (rk == RefKind.None || rk == RefKind.Ref || rk == RefKind.In)
                                {
                                    sb.AppendIndent(sbIndent)
                                        .AppendTypeName(par.Type, false)
                                        .Append(" ").Append(par.Name)
                                        .Append("_odntrop_internal_proxy = (")
                                        .AppendTypeName(par.Type, false)
                                        .Append(")(OdinInterop.ObjectHandle<")
                                        .AppendTypeName(par.Type, false)
                                        .Append(">)").Append(par.Name).AppendLine(";");
                                }
                            }
                            break;
                    }
                }

                // Call the target method
                sb.AppendIndent(sbIndent);
                if (!method.ReturnsVoid)
                {
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                    sb.Append("var odntrop_internal_RetValXXX = ");
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                }
                sb.Append(instParamName).Append("_proxy.").Append(method.Name).Append("(");
                sb.AppendParameters(method.Parameters, null);
                sb.AppendLine(");");

                // Copy-back for ref/out params
                foreach (var par in method.Parameters)
                {
                    var rk = par.RefKind;
                    switch (par.Type.GetInteroperabilityType())
                    {
                        case InteroperabilityType.AutoConverting:
                            if (rk == RefKind.Out || rk == RefKind.Ref)
                            {
                                sb.AppendIndent(sbIndent)
                                    .Append(par.Name).Append(" = ")
                                    .Append(par.Name).AppendLine("_odntrop_internal_proxy;");
                            }
                            break;
                        case InteroperabilityType.CustomMarshalled:
                            if (par.Type.IsUnityObject() && (rk == RefKind.Ref || rk == RefKind.Out))
                            {
                                sb.AppendIndent(sbIndent)
                                    .Append(par.Name)
                                    .Append(" = (OdinInterop.RawObjectHandle)(OdinInterop.ObjectHandle<")
                                    .AppendTypeName(par.Type, false)
                                    .Append(">)").Append(par.Name)
                                    .AppendLine("_odntrop_internal_proxy;");
                            }
                            break;
                    }
                }

                // Return statement
                if (!method.ReturnsVoid)
                {
                    sb.AppendIndent(sbIndent);
                    sb.Append("return ");
                    if (method.ReturnsByRef)
                        sb.Append("ref ");
                    else if (method.ReturnsByRefReadonly)
                        sb.Append("ref readonly ");
                    sb.AppendLine("odntrop_internal_RetValXXX;");
                }

                sbIndent--;
                sb.AppendIndent(sbIndent).AppendLine("}");
                sb.AppendLine();
            }

            // Editor hot-reload + runtime init
            sb.AppendLine("#if UNITY_EDITOR || ODININTEROP_RUNTIME_RELOADING");
            sb.AppendLine();

            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendIndent(sbIndent).AppendLine("[InitializeOnLoadMethod]");
            sb.AppendLine("#else");
            sb.AppendIndent(sbIndent).AppendLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]");
            sb.AppendLine("#endif");
            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_EditorInit()");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;
            sb.AppendIndent(sbIndent).AppendLine("OdinCompilerUtils.onHotReload += odntrop_OnHotReload;");
            sb.AppendIndent(sbIndent).AppendLine("if (OdinCompilerUtils.initialisedAfterDomainReload) odntrop_OnHotReload(OdinCompilerUtils.libraryHandle);");
            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}").AppendLine();

            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_OnHotReload(IntPtr libraryHandle)");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;
            sb.AppendIndent(sbIndent).AppendLine("if (libraryHandle == IntPtr.Zero) return;");
            sb.AppendLine();

            foreach (var method in exportedMethods)
            {
                var odinName = odinNames[method];
                sb.AppendIndent(sbIndent)
                    .Append("LibraryUtils.GetDelegate<odntrop_del_Set")
                    .Append(odinName)
                    .Append("Delegate>(libraryHandle, \"odntrop_export_setter_")
                    .Append(tyName)
                    .Append("_")
                    .Append(odinName)
                    .Append("\")?.Invoke(odntrop_exported_")
                    .Append(odinName)
                    .AppendLine(");");
            }

            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");
            sb.AppendLine();

            // Runtime bindings
            sb.AppendLine("#elif !ODININTEROP_DISABLED");
            sb.AppendLine();

            foreach (var method in exportedMethods)
            {
                var odinName = odinNames[method];
                sb.AppendIndent(sbIndent)
                    .Append("[DllImport(k_OdinInteropDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = \"odntrop_export_setter_")
                    .Append(tyName)
                    .Append("_")
                    .Append(odinName)
                    .AppendLine("\")]")
                    .AppendIndent(sbIndent)
                    .Append("private static extern void odntrop_set_")
                    .Append(odinName)
                    .Append("(odntrop_del_")
                    .Append(odinName)
                    .AppendLine(" value);");
                sb.AppendLine();
            }

            sb.AppendIndent(sbIndent).AppendLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]");
            sb.AppendIndent(sbIndent).AppendLine("private static void odntrop_RuntimeInit()");
            sb.AppendIndent(sbIndent).AppendLine("{");
            sbIndent++;
            foreach (var method in exportedMethods)
            {
                var odinName = odinNames[method];
                sb.AppendIndent(sbIndent)
                    .Append("odntrop_set_").Append(odinName)
                    .Append("(odntrop_exported_").Append(odinName).AppendLine(");");
            }
            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("#endif");
            sb.AppendLine();

            // Interop manifest
            {
                var sourceFilePath = classSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath?.Replace('\\', '/') ?? "";
                sb.AppendLine();
                sb.AppendIndent(sbIndent).Append("internal const string OdinInteropSourcePath = \"")
                    .Append(sourceFilePath).AppendLine("\";");

                if (exportedMethods.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendIndent(sbIndent).AppendLine(
                        "internal static readonly System.Collections.Generic.Dictionary<string, int> OdinInteropMethodLines = new() {");
                    sbIndent++;
                    foreach (var method in exportedMethods)
                    {
                        var loc = method.Locations.FirstOrDefault();
                        var line = loc?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
                        sb.AppendIndent(sbIndent)
                            .Append("[\"").Append(odinNames[method]).Append("\"] = ").Append(line).AppendLine(",");
                    }
                    sbIndent--;
                    sb.AppendIndent(sbIndent).AppendLine("};");
                }
            }

            // close class
            sbIndent--;
            sb.AppendIndent(sbIndent).AppendLine("}");

            // close namespace
            if (!string.IsNullOrEmpty(classSymbol.ContainingNamespace?.Name) &&
                classSymbol.ContainingNamespace.Name != "<global namespace>")
            {
                sbIndent--;
                sb.AppendLine("}");
            }
        }

        /// <summary>Extracts the &lt;summary&gt; text from a method's XML doc comment, or null if none.</summary>
        private static string GetDocSummary(IMethodSymbol method)
        {
            var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) return null;
            var node = syntaxRef.GetSyntax();
            var trivia = node.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            if (trivia == default) return null;
            var xml = trivia.ToFullString();
            var start = xml.IndexOf("<summary>", StringComparison.Ordinal);
            if (start < 0) return null;
            start += "<summary>".Length;
            var end = xml.IndexOf("</summary>", start, StringComparison.Ordinal);
            if (end < 0) return null;
            return xml.Substring(start, end - start).Trim();
        }

        /// <summary>Escapes a string for safe inclusion in a C# string literal.</summary>
        private static string EscapeForCSharp(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private sealed class Receiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> candidateClasses { get; } = new List<ClassDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclaration)
                {
                    // any attributes
                    if (classDeclaration.AttributeLists.Count > 0)
                    {
                        candidateClasses.Add(classDeclaration);
                    }
                }
            }
        }
    }

    internal enum InteroperabilityType
    {
        Blittable, // most interoperable; has a 1:1 memory representation
        AutoConverting, // can be automatically converted by the runtime (arrays, lists, etc)
        CustomMarshalled, // requires custom conversions
    }

    internal static class Extensions
    {
        public static StringBuilder AppendFailedReturn(this StringBuilder sb, IMethodSymbol method, int sbIndent)
        {
            foreach (var parm in method.Parameters)
            {
                if (parm.RefKind == RefKind.Out)
                {
                    sb
                        .AppendIndent(sbIndent)
                        .Append("EmptyRefReturn<")
                        .AppendTypeName(parm.Type, false)
                        .Append(">.Fill(out ")
                        .Append(parm.Name)
                        .AppendLine(");");
                }
            }

            sb.AppendIndent(sbIndent).Append("return");
            if (!method.ReturnsVoid)
            {
                if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                {
                    sb.Append(" ref ");
                    if (method.ReturnsByRefReadonly)
                        sb.Append("readonly ");

                    sb
                        .Append("EmptyRefReturn<")
                        .AppendTypeName(method.ReturnType, false)
                        .Append(">.corruptedValue");
                }
                else
                {
                    sb.Append(" default");
                }
            }
            sb.AppendLine(";");

            return sb;
        }

        public static StringBuilder AppendIndent(this StringBuilder sb, int indentVal)
        {
            for (int i = 0; i < indentVal; i++)
            {
                sb.Append('\t');
            }

            return sb;
        }

        public static StringBuilder AppendReturnType(this StringBuilder sb, IMethodSymbol mt, bool useInteroperableVersion)
        {
            if (mt.ReturnsByRef)
                sb.Append("ref ");
            else if (mt.ReturnsByRefReadonly)
                sb.Append("ref readonly ");

            sb.AppendTypeName(mt.ReturnType, useInteroperableVersion);
            return sb;
        }

        public static StringBuilder AppendParameters(this StringBuilder sb, IEnumerable<IParameterSymbol> parameters, bool? useInteroperableVersion)
        {
            var addComma = false;
            foreach (var par in parameters)
            {
                if (addComma) sb.Append(", ");
                addComma = true;

                var interoperability = par.Type.GetInteroperabilityType();
                var varIsProxy = interoperability != InteroperabilityType.Blittable;
                switch (par.RefKind)
                {
                    case RefKind.None:
                        varIsProxy = varIsProxy && (interoperability != InteroperabilityType.AutoConverting);
                        break;
                    case RefKind.Ref:
                        sb.Append("ref ");
                        break;
                    case RefKind.Out:
                        sb.Append("out ");
                        if (varIsProxy && !useInteroperableVersion.HasValue)
                            sb.Append("var ");
                        break;
                    case RefKind.In:
                        // only pass by by in if blittable, otherwise let it copy
                        if (useInteroperableVersion.HasValue || !varIsProxy)
                            sb.Append("in ");

                        break;
                    default:
                        sb.Append("#ERROR_REF_KIND# ");
                        break;
                }

                if (useInteroperableVersion.HasValue)
                {
                    sb.AppendTypeName(par.Type, useInteroperableVersion.Value);
                    sb.Append(" ");
                }
                sb.Append(par.Name);

                if (varIsProxy && !useInteroperableVersion.HasValue)
                {
                    sb.Append($"_odntrop_internal_proxy");
                }
            }

            return sb;
        }

        private static Dictionary<ITypeSymbol, InteroperabilityType> s_InteroperabilityTypeCache = new Dictionary<ITypeSymbol, InteroperabilityType>(SymbolEqualityComparer.Default);
        internal static InteroperabilityType GetInteroperabilityType(this ITypeSymbol type) // whether any conversions are required
        {
            if (s_InteroperabilityTypeCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            InteroperabilityType? result = null;
            switch (type.SpecialType)
            {
                case SpecialType.System_Void:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Boolean:
                    result = InteroperabilityType.Blittable;
                    goto foundResult;
                default:
                    break;
            }

            if (type is IPointerTypeSymbol)
            {
                result = InteroperabilityType.Blittable;
                goto foundResult;
            }

            if (type.TypeKind == TypeKind.Enum)
            {
                result = InteroperabilityType.Blittable;
                goto foundResult;
            }

            if (type.IsUnmanagedType && !(type is INamedTypeSymbol nt2 && nt2.IsGenericType))
            {
                result = InteroperabilityType.Blittable;
                goto foundResult;
            }

            if (type is INamedTypeSymbol nt &&
                nt.OriginalDefinition.SpecialType == SpecialType.None)
            {
                var odStr = nt.OriginalDefinition.ToString();
                if (odStr == "OdinInterop.DynamicArray<T>" ||
                    odStr == "OdinInterop.Slice<T>" ||
                    odStr == "OdinInterop.ObjectHandle<T>")
                {
                    result = InteroperabilityType.AutoConverting;
                    goto foundResult;
                }
            }

        foundResult:
            var res2 = result ?? InteroperabilityType.CustomMarshalled;
            s_InteroperabilityTypeCache[type] = res2;
            return res2;
        }

        public static StringBuilder AppendTypeName(this StringBuilder sb, ITypeSymbol type, bool useInteroperableVersion)
        {
            if (type is IPointerTypeSymbol pts)
            {
                sb.AppendTypeName(pts.PointedAtType, useInteroperableVersion);
                sb.Append("*");
                return sb;
            }

            if (type is IArrayTypeSymbol ats)
            {
                var elementType = ats.ElementType;
                if (useInteroperableVersion)
                {
                    return sb.Append("RawSlice");
                }
                else
                {
                    sb.AppendTypeName(elementType, false);
                    sb.Append("[]");
                    return sb;
                }
            }

            if (type is INamedTypeSymbol nt &&
                nt.OriginalDefinition.SpecialType == SpecialType.None)
            {
                var odStr = nt.OriginalDefinition.ToString();
                if (odStr == "System.Collections.Generic.List<T>")
                {
                    var elementType = nt.TypeArguments[0];
                    if (useInteroperableVersion)
                    {
                        return sb.Append("RawDynamicArray");
                    }
                    else
                    {
                        sb.Append("List<");
                        sb.AppendTypeName(elementType, false);
                        sb.Append(">");
                        return sb;
                    }
                }
                else if (odStr == "OdinInterop.DynamicArray<T>")
                {
                    if (useInteroperableVersion)
                    {
                        return sb.Append("RawDynamicArray");
                    }
                    else
                    {
                        sb.Append("DynamicArray<");
                        sb.AppendTypeName(nt.TypeArguments[0], false);
                        sb.Append(">");
                        return sb;
                    }
                }
                else if (odStr == "OdinInterop.Slice<T>")
                {
                    if (useInteroperableVersion)
                    {
                        return sb.Append("RawSlice");
                    }
                    else
                    {
                        sb.Append("Slice<");
                        sb.AppendTypeName(nt.TypeArguments[0], false);
                        sb.Append(">");
                        return sb;
                    }
                }
                else if (odStr == "OdinInterop.ObjectHandle<T>")
                {
                    if (useInteroperableVersion)
                    {
                        return sb.Append("RawObjectHandle");
                    }
                    else
                    {
                        sb.Append("ObjectHandle<");
                        sb.AppendTypeName(nt.TypeArguments[0], false);
                        sb.Append(">");
                        return sb;
                    }
                }
            }

            var specialType = type.SpecialType;

            string s;
            switch (specialType)
            {
                case SpecialType.System_Void: s = "void"; break;
                case SpecialType.System_Byte: s = "byte"; break;
                case SpecialType.System_SByte: s = "sbyte"; break;
                case SpecialType.System_Int16: s = "short"; break;
                case SpecialType.System_UInt16: s = "ushort"; break;
                case SpecialType.System_Int32: s = "int"; break;
                case SpecialType.System_UInt32: s = "uint"; break;
                case SpecialType.System_Int64: s = "long"; break;
                case SpecialType.System_UInt64: s = "ulong"; break;
                case SpecialType.System_Single: s = "float"; break;
                case SpecialType.System_Double: s = "double"; break;
                case SpecialType.System_Boolean: s = "bool"; break;
                default:
                    var fullName = type.GetFullTypeName();

                    // unity types
                    if (fullName == "UnityEngine.Vector2") s = "UnityEngine.Vector2";
                    else if (fullName == "UnityEngine.Vector3") s = "UnityEngine.Vector3";
                    else if (fullName == "UnityEngine.Vector4") s = "UnityEngine.Vector4";
                    else if (fullName == "UnityEngine.Quaternion") s = "UnityEngine.Quaternion";
                    else if (fullName == "UnityEngine.Color") s = "UnityEngine.Color";
                    else if (fullName.StartsWith("OdinInterop.Slice")) s = fullName;
                    else if (fullName.StartsWith("OdinInterop.DynamicArray")) s = fullName;
                    else if (fullName == "OdinInterop.String8") s = "OdinInterop.String8";
                    else if (fullName == "OdinInterop.String16") s = "OdinInterop.String16";
                    else if (fullName == "OdinInterop.Allocator") s = "OdinInterop.Allocator";
                    else if (type.TypeKind == TypeKind.Enum) // enum
                    {
                        s = fullName;
                    }
                    else if (type.IsUnityObject())
                    {
                        s = useInteroperableVersion ? "RawObjectHandle" : fullName;
                    }
                    else if (useInteroperableVersion)
                    {
                        if (type.IsUnmanagedType && !(type is INamedTypeSymbol nt2 && nt2.IsGenericType)) s = fullName;
                        else s = "odntrop_type_" + fullName.Replace('.', '_');
                    }
                    else
                    {
                        s = null;
                    }
                    break;
            }

            if (s != null)
            {
                sb.Append(s);
                return sb;
            }
            else
            {
                sb.Append($"#ERROR {type.GetFullTypeName()}#");
                return sb;
            }
        }

        private static Dictionary<ISymbol, string> s_FullTypeNameCache = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        public static string GetFullTypeName(this ISymbol symbol)
        {
            if (symbol == null) return string.Empty;

            if (s_FullTypeNameCache.TryGetValue(symbol, out var cached))
                return cached;

            var parts = new List<string>();

            ISymbol current = symbol;
            while (current is INamedTypeSymbol nts)
            {
                var name = nts.Name;
                if (nts.TypeArguments.Length != 0)
                    name = $"{name}<{string.Join(", ", nts.TypeArguments.Select(arg => arg.GetFullTypeName()))}>"; // recursive

                parts.Insert(0, name);
                current = nts.ContainingType;
            }

            var typeName = string.Join(".", parts);

            // add namespace if present
            var ns = symbol.ContainingNamespace;
            if (ns != null && !ns.IsGlobalNamespace)
            {
                var nsParts = new List<string>();
                while (ns != null && !ns.IsGlobalNamespace)
                {
                    nsParts.Insert(0, ns.Name);
                    ns = ns.ContainingNamespace;
                }

                if (nsParts.Count > 0)
                {
                    var fullName = string.Join(".", nsParts) + "." + typeName;
                    s_FullTypeNameCache[symbol] = fullName;
                    return fullName;
                }
            }

            s_FullTypeNameCache[symbol] = typeName;
            return typeName;
        }

        private static Dictionary<ITypeSymbol, bool> s_UnityObjectsCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);
        public static bool IsUnityObject(this ITypeSymbol type)
        {
            if (type == null) return false;

            if (s_UnityObjectsCache.TryGetValue(type, out var cached))
                return cached;

            if (type.GetFullTypeName() == "UnityEngine.Object")
            {
                s_UnityObjectsCache[type] = true;
                return true;
            }

            var isObj = type.BaseType.IsUnityObject();
            s_UnityObjectsCache[type] = isObj;
            return isObj;
        }
    }
}
