using UnityEngine;
using UnityEngine.Diagnostics;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class UnityPanic
    {
        private static void Fatal(String8 prefix, String8 message, String8 procedure, String8 file, int line, int column)
        {
            Debug.LogErrorFormat(
                "[Assertion Failure] {0}: {1} (in function {2} at {3}:{4}:{5})",
                prefix.ToString(),
                message.ToString(),
                procedure.ToString(),
                file.ToString(),
                line,
                column
            );
            Utils.ForceCrash(ForcedCrashCategory.FatalError);
        }
    }
}
