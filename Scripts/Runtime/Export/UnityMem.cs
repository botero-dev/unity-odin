using Unity.Collections.LowLevel.Unsafe;
using UnityAllocator = Unity.Collections.Allocator;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class Mem
    {
        internal static void Copy(void* destination, void* source, long size) => UnsafeUtility.MemCpy(destination, source, size);

        internal static void Move(void* destination, void* source, long size) => UnsafeUtility.MemMove(destination, source, size);

        internal static void Set(void* destination, byte value, long size) => UnsafeUtility.MemSet(destination, value, size);

        internal static void Clr(void* destination, long size) => UnsafeUtility.MemClear(destination, size);

        internal static void* Tmp(long size, int alignment) => UnsafeUtility.Malloc(size, alignment, UnityAllocator.Temp);
    }
}
