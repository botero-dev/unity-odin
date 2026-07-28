using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioClip
    {
    // AudioClip API

        private static float GetLength(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.length : 0f;

        private static int GetSamples(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.samples : 0;

        private static int GetChannels(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.channels : 0;

        private static int GetFrequency(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.frequency : 0;

        private static bool IsPreloaded(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.preloadAudioData : false;

        private static bool IsAmbisonic(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.ambisonic : false;

        private static AudioClipLoadType GetLoadType(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.loadType : AudioClipLoadType.DecompressOnLoad;

        private static bool LoadData(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.LoadAudioData() : false;

        private static bool UnloadData(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.UnloadAudioData() : false;

        private static AudioDataLoadState GetLoadState(ObjectHandle<AudioClip> audioClip) => audioClip ? audioClip.value.loadState : AudioDataLoadState.Unloaded;

        private static ObjectHandle<AudioClip> StartMicrophoneRecording(String8 deviceName, bool loop, int lengthSec, int frequency) => Microphone.Start(deviceName.ToString(), loop, lengthSec, frequency);
    }
}
