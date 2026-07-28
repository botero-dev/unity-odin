using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioListener
    {
    // AudioListener API

        private static float GetVolume() => AudioListener.volume;

        private static void SetVolume(float volume) => AudioListener.volume = volume;

        private static bool GetPause() => AudioListener.pause;

        private static void SetPause(bool pause) => AudioListener.pause = pause;

        private static AudioVelocityUpdateMode GetVelocityUpdateMode(ObjectHandle<AudioListener> listener) => listener ? listener.value.velocityUpdateMode : AudioVelocityUpdateMode.Auto;

        private static void SetVelocityUpdateMode(ObjectHandle<AudioListener> listener, AudioVelocityUpdateMode mode)
        {
            if (listener)
                listener.value.velocityUpdateMode = mode;
        }
    }
}
