
namespace OdinInterop
{
    /// <summary>
    /// Odin functions exposed to C# (imported from Odin).
    /// </summary>
    [OdinImport(odinSrcAppend = InteropGeneratorInbuiltFiles.ENGINE_BINDINGS_IMPORT_APPEND)]
    internal static partial class EngineBindingsImports
    {
        public static partial Allocator UnityOdnTropInternalGetMainOdnAllocator();
        public static partial Allocator UnityOdnTropInternalGetTempOdnAllocator();
        public static partial Slice<byte> UnityOdnTropInternalAllocateUsingOdnAllocator(int tySize, int tyAlignment, int tyCount, Allocator allocator);
        public static partial void UnityOdnTropInternalFreeUsingOdnAllocator(Slice<byte> ptr, Allocator allocator);
    }
}
