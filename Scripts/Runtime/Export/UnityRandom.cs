using Random = UnityEngine.Random;

namespace OdinExports
{
    [OdinInterop.OdinExportAll(typeof(UnityEngine.Random))]
    [OdinInterop.OdinExport]
    internal static partial class Random {
    // {
    //     public static void InitState(int seed) => Random.InitState(seed);

    //     public static Random.State GetState() => Random.state;

    //     public static void SetState(Random.State state) => Random.state = state;

        internal static int GetNextInt() => UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }
}
