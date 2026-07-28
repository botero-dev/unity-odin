using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioReverbZone
    {
    // AudioReverbZone API

        private static float GetMinDistance(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.minDistance : 0f;

        private static void SetMinDistance(ObjectHandle<AudioReverbZone> zone, float distance)
        {
            if (zone)
                zone.value.minDistance = distance;
        }

        private static float GetMaxDistance(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.maxDistance : 0f;

        private static void SetMaxDistance(ObjectHandle<AudioReverbZone> zone, float distance)
        {
            if (zone)
                zone.value.maxDistance = distance;
        }

        private static AudioReverbPreset GetPreset(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.reverbPreset : AudioReverbPreset.User;

        private static void SetPreset(ObjectHandle<AudioReverbZone> zone, AudioReverbPreset preset)
        {
            if (zone)
                zone.value.reverbPreset = preset;
        }

        private static int GetRoom(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.room : 0;

        private static void SetRoom(ObjectHandle<AudioReverbZone> zone, int room)
        {
            if (zone)
                zone.value.room = room;
        }

        private static int GetRoomHF(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.roomHF : 0;

        private static void SetRoomHF(ObjectHandle<AudioReverbZone> zone, int roomHF)
        {
            if (zone)
                zone.value.roomHF = roomHF;
        }

        private static int GetRoomLF(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.roomLF : 0;

        private static void SetRoomLF(ObjectHandle<AudioReverbZone> zone, int roomLF)
        {
            if (zone)
                zone.value.roomLF = roomLF;
        }

        private static float GetDecayTime(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.decayTime : 1f;

        private static void SetDecayTime(ObjectHandle<AudioReverbZone> zone, float time)
        {
            if (zone)
                zone.value.decayTime = time;
        }

        private static float GetDecayHFRatio(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.decayHFRatio : 0.5f;

        private static void SetDecayHFRatio(ObjectHandle<AudioReverbZone> zone, float ratio)
        {
            if (zone)
                zone.value.decayHFRatio = ratio;
        }

        private static int GetReflections(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.reflections : 0;

        private static void SetReflections(ObjectHandle<AudioReverbZone> zone, int reflections)
        {
            if (zone)
                zone.value.reflections = reflections;
        }

        private static float GetReflectionsDelay(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.reflectionsDelay : 0f;

        private static void SetReflectionsDelay(ObjectHandle<AudioReverbZone> zone, float delay)
        {
            if (zone)
                zone.value.reflectionsDelay = delay;
        }

        private static int GetReverb(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.reverb : 0;

        private static void SetReverb(ObjectHandle<AudioReverbZone> zone, int reverb)
        {
            if (zone)
                zone.value.reverb = reverb;
        }

        private static float GetReverbDelay(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.reverbDelay : 0f;

        private static void SetReverbDelay(ObjectHandle<AudioReverbZone> zone, float delay)
        {
            if (zone)
                zone.value.reverbDelay = delay;
        }

        private static float GetDiffusion(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.diffusion : 100f;

        private static void SetDiffusion(ObjectHandle<AudioReverbZone> zone, float diffusion)
        {
            if (zone)
                zone.value.diffusion = diffusion;
        }

        private static float GetDensity(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.density : 100f;

        private static void SetDensity(ObjectHandle<AudioReverbZone> zone, float density)
        {
            if (zone)
                zone.value.density = density;
        }

        private static float GetHFReference(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.HFReference : 5000f;

        private static void SetHFReference(ObjectHandle<AudioReverbZone> zone, float reference)
        {
            if (zone)
                zone.value.HFReference = reference;
        }

        private static float GetLFReference(ObjectHandle<AudioReverbZone> zone) => zone ? zone.value.LFReference : 250f;

        private static void SetLFReference(ObjectHandle<AudioReverbZone> zone, float reference)
        {
            if (zone)
                zone.value.LFReference = reference;
        }
    }
}
