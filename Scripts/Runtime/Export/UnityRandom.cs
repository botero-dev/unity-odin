using Random = UnityEngine.Random;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class UnityRandom
    {
        private static void InitState(int seed) => Random.InitState(seed);

        private static Random.State GetState() => Random.state;

        private static void SetState(Random.State state) => Random.state = state;

        private static int GetNextInt() => Random.Range(int.MinValue, int.MaxValue);
    }
}
