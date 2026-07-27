using System;

namespace OdinInterop
{
    /// <summary>
    /// Marks a class whose methods should be exported from C# to Odin.
    /// Private/internal methods are exported as callable functions in Odin.
    /// </summary>
    public class OdinExportAttribute : Attribute
    {
        public OdinExportAttribute() { }

        public string odinSrcAppend { get; set; } = "";
    }

    /// <summary>
    /// Marks a class whose public static partial methods should be imported from Odin into C#.
    /// These are Odin functions exposed as C# callable methods.
    /// </summary>
    public class OdinImportAttribute : Attribute
    {
        public OdinImportAttribute() { }

        public string odinSrcAppend { get; set; } = "";
    }

    /// <summary>
    /// Legacy attribute for backward compatibility. Combines both OdinExport and OdinImport behavior.
    /// Prefer using OdinExport and OdinImport separately for clarity.
    /// </summary>
    public class GenerateOdinInteropAttribute : Attribute
    {
        public GenerateOdinInteropAttribute() { }

        public string odinSrcAppend { get; set; } = "";
    }
}
