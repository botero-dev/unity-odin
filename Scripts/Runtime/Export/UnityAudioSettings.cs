using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioSettings
    {
    // AudioSettings API

        private static double GetDspTime() => AudioSettings.dspTime;

        private static AudioSpeakerMode GetSpeakerMode() => AudioSettings.speakerMode;

        private static void GetDSPBufferSize(out int bufferLength, out int numBuffers) => AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);

        private static int GetOutputSampleRate() => AudioSettings.outputSampleRate;

        private static void SetOutputSampleRate(int sampleRate) => AudioSettings.outputSampleRate = sampleRate;

        private static void Reset(AudioConfiguration config) => AudioSettings.Reset(config);

        private static AudioConfiguration GetAudioConfiguration() => AudioSettings.GetConfiguration();
    }
}
