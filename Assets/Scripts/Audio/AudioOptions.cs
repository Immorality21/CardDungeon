using Assets.Scripts.IO;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    /// <summary>
    /// The game's volume and mute state: one static, lazily-loaded home for the three
    /// <see cref="AudioChannel"/> dials, persisted to <c>savedata/Audio.json</c> the moment the
    /// player moves one.
    ///
    /// <para>Static rather than a <c>SingletonBehaviour</c> because it is read from places that have
    /// no scene of their own — the hub's options screen, <see cref="CombatAudio"/>'s
    /// <c>PlayOneShot</c> volume, <see cref="MusicPlayer"/>'s per-frame source volume — and it holds
    /// no state that a scene load should reset. <see cref="Apply"/> is what actually reaches the
    /// engine, and only for Master: it drives <c>AudioListener.volume</c>, so it scales everything,
    /// including sounds no channel knows about. Music and SFX are applied by their own players, which
    /// read <see cref="MusicVolume"/> / <see cref="SfxVolume"/> at the point of use.</para>
    ///
    /// <para>The arithmetic is separated out as pure statics (<see cref="Snap"/>,
    /// <see cref="Nudged"/>, <see cref="Gated"/>) so the stepping and the mute interaction are
    /// testable without an <c>AudioSource</c> — see <c>AudioOptionsTests</c>.</para>
    /// </summary>
    public static class AudioOptions
    {
        /// <summary>How far one press of a volume button moves the dial.</summary>
        public const float Step = 0.1f;

        private static AudioOptionsSaveData _data;
        private static FileHandler _files;

        private static AudioOptionsSaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _files = new FileHandler();
                    _data = _files.Load<AudioOptionsSaveData>();
                    // A hand-edited or older file could hold anything; snap it into range once on load
                    // rather than defending against it at every read.
                    _data.Master = Snap(_data.Master);
                    _data.Music = Snap(_data.Music);
                    _data.Sfx = Snap(_data.Sfx);
                    AudioListener.volume = Gated(_data.Master, _data.Muted);
                }
                return _data;
            }
        }

        public static bool Muted
        {
            get { return Data.Muted; }
        }

        /// <summary>
        /// The multiplier a music source should use. Master is deliberately *not* folded in — the
        /// listener already applies it, and folding it in again would square it.
        /// </summary>
        public static float MusicVolume
        {
            get { return Gated(Data.Music, Data.Muted); }
        }

        /// <summary>The multiplier a one-shot SFX should use, mute included.</summary>
        public static float SfxVolume
        {
            get { return Gated(Data.Sfx, Data.Muted); }
        }

        /// <summary>The stored value of one dial, 0..1, ignoring mute.</summary>
        public static float Get(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    return Data.Music;
                case AudioChannel.Sfx:
                    return Data.Sfx;
                default:
                    return Data.Master;
            }
        }

        /// <summary>Sets one dial (snapped and clamped), applies it and writes the file.</summary>
        public static void Set(AudioChannel channel, float value)
        {
            float snapped = Snap(value);
            switch (channel)
            {
                case AudioChannel.Music:
                    Data.Music = snapped;
                    break;
                case AudioChannel.Sfx:
                    Data.Sfx = snapped;
                    break;
                default:
                    Data.Master = snapped;
                    break;
            }
            Apply();
            Save();
        }

        /// <summary>Moves one dial by <paramref name="steps"/> presses. Used by the options screen.</summary>
        public static void Nudge(AudioChannel channel, int steps)
        {
            Set(channel, Nudged(Get(channel), steps));
        }

        public static void SetMuted(bool muted)
        {
            Data.Muted = muted;
            Apply();
            Save();
        }

        public static void ToggleMute()
        {
            SetMuted(!Muted);
        }

        /// <summary>
        /// Pushes Master (and mute) at the engine. Safe to call every frame; it is one assignment.
        /// Music and SFX are not applied here — their players scale themselves, because a listener
        /// volume cannot tell one kind of sound from another.
        /// </summary>
        public static void Apply()
        {
            var data = Data; // forces the load, so calling this first thing in a scene is correct
            AudioListener.volume = Gated(data.Master, data.Muted);
        }

        private static void Save()
        {
            if (_files == null)
            {
                _files = new FileHandler();
            }
            _files.Save(_data);
        }

        // ============================================================
        //  Pure arithmetic (no engine state — unit-tested)
        // ============================================================

        /// <summary>Rounds to the nearest <see cref="Step"/> and clamps to 0..1.</summary>
        public static float Snap(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }
            float snapped = Mathf.Round(Mathf.Clamp01(value) / Step) * Step;
            return Mathf.Clamp01(snapped);
        }

        /// <summary>The value <paramref name="steps"/> presses away from <paramref name="value"/>.</summary>
        public static float Nudged(float value, int steps)
        {
            return Snap(Snap(value) + steps * Step);
        }

        /// <summary>
        /// One dial as the engine should hear it: silence while muted, the clamped value otherwise.
        /// Mute is a gate rather than a stored zero, so un-muting restores the dials the player set
        /// instead of leaving them at silence. Used by all three appliers - the listener's master,
        /// the music source and every one-shot - so there is one place the mute rule lives.
        /// </summary>
        public static float Gated(float value, bool muted)
        {
            if (muted)
            {
                return 0f;
            }
            return Mathf.Clamp01(value);
        }

        /// <summary>A dial as a percentage for display, e.g. <c>70%</c>.</summary>
        public static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }
}
