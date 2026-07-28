using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioMixer
    {
    // AudioMixer API

        private static bool SetFloatValue(ObjectHandle<AudioMixer> mixer, String8 name, float value) => mixer ? mixer.value.SetFloat(name.ToString(), value) : false;

        private static bool GetFloatValue(ObjectHandle<AudioMixer> mixer, String8 name, out float value)
        {
            if (mixer)
                return mixer.value.GetFloat(name.ToString(), out value);
            value = 0f;
            return false;
        }

        private static bool ClearFloatValue(ObjectHandle<AudioMixer> mixer, String8 name) => mixer ? mixer.value.ClearFloat(name.ToString()) : false;

        private static ObjectHandle<AudioMixerSnapshot> FindSnapshot(ObjectHandle<AudioMixer> mixer, String8 name) => mixer ? mixer.value.FindSnapshot(name.ToString()) : default;

        private static void TransitionToSnapshot(ObjectHandle<AudioMixerSnapshot> snapshot, float timeToReach)
        {
            if (snapshot)
                snapshot.value.TransitionTo(timeToReach);
        }

        private static void TransitionToSnapshots(ObjectHandle<AudioMixer> mixer, Slice<ObjectHandle<AudioMixerSnapshot>> snapshots, Slice<float> weights, float timeToReach)
        {
            if (snapshots.len != weights.len || snapshots.len.ToInt32() == 0)
                return;

            var snapshotArr = new AudioMixerSnapshot[(int)snapshots.len];
            var weightArr = new float[(int)weights.len];
            for (var i = 0; i < (int)snapshots.len; i++)
            {
                snapshotArr[i] = snapshots.ptr[i];
                weightArr[i] = weights.ptr[i];
            }

            mixer.value.TransitionToSnapshots(snapshotArr, weightArr, timeToReach);
        }

        // AudioMixerGroup API

        private static ObjectHandle<AudioMixer> GetFromGroup(ObjectHandle<AudioMixerGroup> group) => group ? group.value.audioMixer : default;
    }
}
