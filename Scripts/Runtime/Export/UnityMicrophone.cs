using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityMicrophone
    {
    // Microphone API

        private static Slice<String8> GetDevices(Allocator allocator)
        {
            var devices = Microphone.devices;
            var slice = new Slice<String8>(devices.Length, allocator);
            for (var i = 0; i < devices.Length; i++)
                slice.ptr[i] = new String8(devices[i], allocator);
            return slice;
        }

        private static void GetDeviceCaps(String8 deviceName, out int minFreq, out int maxFreq) => Microphone.GetDeviceCaps(deviceName.ToString(), out minFreq, out maxFreq);

        private static void EndRecording(String8 deviceName) => Microphone.End(deviceName.ToString());

        private static bool IsRecording(String8 deviceName) => Microphone.IsRecording(deviceName.ToString());

        private static int GetPosition(String8 deviceName) => Microphone.GetPosition(deviceName.ToString());
    }
}
