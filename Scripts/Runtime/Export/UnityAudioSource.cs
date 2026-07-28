using UnityEngine;
using UnityEngine.Audio;

namespace OdinInterop
{    [OdinExport]
    internal static unsafe partial class UnityAudioSource
    {
    // AudioSource API

        private static bool IsPlaying(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.isPlaying : false;

        private static bool IsVirtual(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.isVirtual : false;

        private static void Play(ObjectHandle<AudioSource> audioSource, ulong delay = 0)
        {
            if (audioSource)
                audioSource.value.Play(delay);
        }

        private static void PlayDelayed(ObjectHandle<AudioSource> audioSource, float delay)
        {
            if (audioSource)
                audioSource.value.PlayDelayed(delay);
        }

        private static void PlayScheduled(ObjectHandle<AudioSource> audioSource, double time)
        {
            if (audioSource)
                audioSource.value.PlayScheduled(time);
        }

        private static void PlayOneShot(ObjectHandle<AudioSource> audioSource, ObjectHandle<AudioClip> clip, float volumeScale = 1.0f)
        {
            if (audioSource && clip)
                audioSource.value.PlayOneShot(clip, volumeScale);
        }

        private static void Stop(ObjectHandle<AudioSource> audioSource)
        {
            if (audioSource)
                audioSource.value.Stop();
        }

        private static void Pause(ObjectHandle<AudioSource> audioSource)
        {
            if (audioSource)
                audioSource.value.Pause();
        }

        private static void UnPause(ObjectHandle<AudioSource> audioSource)
        {
            if (audioSource)
                audioSource.value.UnPause();
        }

        private static void SetScheduledStartTime(ObjectHandle<AudioSource> audioSource, double time)
        {
            if (audioSource)
                audioSource.value.SetScheduledStartTime(time);
        }

        private static void SetScheduledEndTime(ObjectHandle<AudioSource> audioSource, double time)
        {
            if (audioSource)
                audioSource.value.SetScheduledEndTime(time);
        }

        private static ObjectHandle<AudioClip> GetClip(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.clip : default;

        private static void SetClip(ObjectHandle<AudioSource> audioSource, ObjectHandle<AudioClip> clip)
        {
            if (audioSource)
                audioSource.value.clip = clip;
        }

        private static float GetVolume(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.volume : 0f;

        private static void SetVolume(ObjectHandle<AudioSource> audioSource, float volume)
        {
            if (audioSource)
                audioSource.value.volume = volume;
        }

        private static float GetPitch(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.pitch : 1f;

        private static void SetPitch(ObjectHandle<AudioSource> audioSource, float pitch)
        {
            if (audioSource)
                audioSource.value.pitch = pitch;
        }

        private static float GetTime(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.time : 0f;

        private static void SetTime(ObjectHandle<AudioSource> audioSource, float time)
        {
            if (audioSource)
                audioSource.value.time = time;
        }

        private static int GetTimeSamples(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.timeSamples : 0;

        private static void SetTimeSamples(ObjectHandle<AudioSource> audioSource, int timeSamples)
        {
            if (audioSource)
                audioSource.value.timeSamples = timeSamples;
        }

        private static bool IsLooping(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.loop : false;

        private static void SetLooping(ObjectHandle<AudioSource> audioSource, bool loop)
        {
            if (audioSource)
                audioSource.value.loop = loop;
        }

        private static bool GetPlayOnAwake(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.playOnAwake : false;

        private static void SetPlayOnAwake(ObjectHandle<AudioSource> audioSource, bool playOnAwake)
        {
            if (audioSource)
                audioSource.value.playOnAwake = playOnAwake;
        }

        private static bool IsMuted(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.mute : false;

        private static void SetMuted(ObjectHandle<AudioSource> audioSource, bool muted)
        {
            if (audioSource)
                audioSource.value.mute = muted;
        }

        private static bool DoesBypassEffects(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.bypassEffects : false;

        private static void SetBypassEffects(ObjectHandle<AudioSource> audioSource, bool bypass)
        {
            if (audioSource)
                audioSource.value.bypassEffects = bypass;
        }

        private static bool DoesBypassListenerEffects(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.bypassListenerEffects : false;

        private static void SetBypassListenerEffects(ObjectHandle<AudioSource> audioSource, bool bypass)
        {
            if (audioSource)
                audioSource.value.bypassListenerEffects = bypass;
        }

        private static bool DoesBypassReverbZones(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.bypassReverbZones : false;

        private static void SetBypassReverbZones(ObjectHandle<AudioSource> audioSource, bool bypass)
        {
            if (audioSource)
                audioSource.value.bypassReverbZones = bypass;
        }

        private static int GetPriority(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.priority : 128;

        private static void SetPriority(ObjectHandle<AudioSource> audioSource, int priority)
        {
            if (audioSource)
                audioSource.value.priority = priority;
        }

        private static float GetPanStereo(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.panStereo : 0f;

        private static void SetPanStereo(ObjectHandle<AudioSource> audioSource, float pan)
        {
            if (audioSource)
                audioSource.value.panStereo = pan;
        }

        private static float GetSpatialBlend(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.spatialBlend : 0f;

        private static void SetSpatialBlend(ObjectHandle<AudioSource> audioSource, float blend)
        {
            if (audioSource)
                audioSource.value.spatialBlend = blend;
        }

        private static float GetReverbZoneMix(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.reverbZoneMix : 1f;

        private static void SetReverbZoneMix(ObjectHandle<AudioSource> audioSource, float mix)
        {
            if (audioSource)
                audioSource.value.reverbZoneMix = mix;
        }

        private static float GetDopplerLevel(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.dopplerLevel : 1f;

        private static void SetDopplerLevel(ObjectHandle<AudioSource> audioSource, float level)
        {
            if (audioSource)
                audioSource.value.dopplerLevel = level;
        }

        private static float GetSpread(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.spread : 0f;

        private static void SetSpread(ObjectHandle<AudioSource> audioSource, float spread)
        {
            if (audioSource)
                audioSource.value.spread = spread;
        }

        private static AudioRolloffMode GetRolloffMode(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.rolloffMode : AudioRolloffMode.Logarithmic;

        private static void SetRolloffMode(ObjectHandle<AudioSource> audioSource, AudioRolloffMode mode)
        {
            if (audioSource)
                audioSource.value.rolloffMode = mode;
        }

        private static float GetMinDistance(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.minDistance : 1f;

        private static void SetMinDistance(ObjectHandle<AudioSource> audioSource, float distance)
        {
            if (audioSource)
                audioSource.value.minDistance = distance;
        }

        private static float GetMaxDistance(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.maxDistance : 500f;

        private static void SetMaxDistance(ObjectHandle<AudioSource> audioSource, float distance)
        {
            if (audioSource)
                audioSource.value.maxDistance = distance;
        }

        private static ObjectHandle<AudioMixerGroup> GetOutputMixerGroup(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.outputAudioMixerGroup : default;

        private static void SetOutputMixerGroup(ObjectHandle<AudioSource> audioSource, ObjectHandle<AudioMixerGroup> mixerGroup)
        {
            if (audioSource)
                audioSource.value.outputAudioMixerGroup = mixerGroup;
        }

        private static bool GetIgnoreListenerVolume(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.ignoreListenerVolume : false;

        private static void SetIgnoreListenerVolume(ObjectHandle<AudioSource> audioSource, bool ignore)
        {
            if (audioSource)
                audioSource.value.ignoreListenerVolume = ignore;
        }

        private static bool GetIgnoreListenerPause(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.ignoreListenerPause : false;

        private static void SetIgnoreListenerPause(ObjectHandle<AudioSource> audioSource, bool ignore)
        {
            if (audioSource)
                audioSource.value.ignoreListenerPause = ignore;
        }

        private static AudioVelocityUpdateMode GetVelocityUpdateMode(ObjectHandle<AudioSource> audioSource) => audioSource ? audioSource.value.velocityUpdateMode : AudioVelocityUpdateMode.Auto;

        private static void SetVelocityUpdateMode(ObjectHandle<AudioSource> audioSource, AudioVelocityUpdateMode mode)
        {
            if (audioSource)
                audioSource.value.velocityUpdateMode = mode;
        }
    // Audio static play methods

        private static void PlayClipAtPoint(ObjectHandle<AudioClip> clip, Vector3 position, float volume = 1.0f)
        {
            if (clip)
                AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
