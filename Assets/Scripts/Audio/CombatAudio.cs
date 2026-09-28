using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    /// <summary>
    /// Fire-and-forget combat SFX. Auto-creates on first use (no scene wiring), loads the
    /// <see cref="SoundBankSO"/> from <c>Resources/CombatSoundBank</c>, and plays a random clip
    /// for a given <see cref="CombatSound"/> through a single 2D <c>AudioSource</c> (PlayOneShot,
    /// so overlapping hits layer). Mirrors <c>CombatFeedback</c>'s auto-wired pattern.
    /// </summary>
    public class CombatAudio : SingletonBehaviour<CombatAudio>
    {
        private const string BankResource = "CombatSoundBank";

        private SoundBankSO _bank;
        private AudioSource _source;
        private bool _ready;

        private void EnsureReady()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;

            _bank = UnityEngine.Resources.Load<SoundBankSO>(BankResource);
            if (_bank == null)
            {
                Debug.LogWarning($"CombatAudio: no SoundBankSO at Resources/{BankResource}; combat will be silent.");
            }

            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D — combat is framed flat, no positional falloff

            // Push the saved master/mute at the listener. Combat is often the first thing in a scene
            // to make a sound, so this is where the player's settings become audible.
            AudioOptions.Apply();
        }

        /// <summary>Play a random clip for the given event. Safe to call before any setup.</summary>
        public static void Play(CombatSound sound, float volumeScale = 1f)
        {
            Instance.PlayInternal(sound, volumeScale);
        }

        /// <summary>
        /// Play one specific clip through the same source and SFX dial - for a sound that belongs to
        /// an asset rather than to an event (a summon's arrival). A null clip is silently nothing.
        /// </summary>
        public static void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }
            Instance.EnsureReady();
            if (Instance._source != null)
            {
                Instance._source.PlayOneShot(clip, Mathf.Clamp01(volume * AudioOptions.SfxVolume));
            }
        }

        private void PlayInternal(CombatSound sound, float volumeScale)
        {
            EnsureReady();
            if (_bank == null || _source == null)
            {
                return;
            }

            var entry = _bank.Get(sound);
            if (entry == null || entry.Clips == null || entry.Clips.Length == 0)
            {
                return;
            }

            var clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            if (clip != null)
            {
                // The SFX dial is applied here, not on the listener: the listener carries Master and
                // cannot tell a sound effect from the music bed playing under it.
                _source.PlayOneShot(clip, Mathf.Clamp01(entry.Volume * volumeScale * AudioOptions.SfxVolume));
            }
        }
    }
}
