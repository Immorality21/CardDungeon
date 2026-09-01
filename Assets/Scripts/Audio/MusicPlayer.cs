using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    /// <summary>
    /// The game's single looping music bed. Auto-creates on first use (no scene wiring, like
    /// <see cref="CombatAudio"/>) and survives scene loads, because the hub and the dungeon are
    /// separate scenes and a bed that restarted on every load would stutter at exactly the moments
    /// the game is trying to feel continuous.
    ///
    /// <para>Two <c>AudioSource</c>s and a weight each: starting a track fades the incoming one up
    /// while the outgoing one fades down, so nothing ever cuts. Requesting the track that is already
    /// playing is a no-op — <c>CombatManager</c> and the hub both call
    /// <c>Play</c> more often than the music actually needs to change,
    /// and re-entering a room must not restart the floor's theme.</para>
    ///
    /// <para>Volume is read from <see cref="AudioOptions"/> every frame rather than pushed on change,
    /// so a dial moved in the options screen is heard while it is being moved and there is no event
    /// to leak across a scene load.</para>
    /// </summary>
    public class MusicPlayer : SingletonBehaviour<MusicPlayer>
    {
        private const string BankResource = "MusicBank";
        private const float DefaultFadeSeconds = 1.2f;

        /// <summary>One of the two crossfading sources.</summary>
        private class Bed
        {
            public AudioSource Source;
            public float Scale;        // the track's authored volume
            public float Weight;       // 0..1 fade position
            public float TargetWeight;
        }

        private MusicBankSO _bank;
        private Bed _front;
        private Bed _back;
        private bool _ready;
        private float _fadeSeconds = DefaultFadeSeconds;

        /// <summary>The track currently faded in, or <see cref="MusicTrack.None"/> while silent.</summary>
        public MusicTrack CurrentTrack { get; private set; }

        protected override void Awake()
        {
            // Set before base.Awake: SingletonBehaviour reads these when it claims the instance, and
            // an auto-created MusicPlayer has no parent to carry with it.
            dontDestroyOnLoad = true;
            DetachFromRoot = true;
            base.Awake();
        }

        /// <summary>
        /// Fades to <paramref name="track"/>. <paramref name="overrideClip"/> wins over the bank —
        /// that is how a level's own theme is honoured (see <c>LevelDefinitionSO</c>). A track with no
        /// clip anywhere fades to silence.
        /// </summary>
        public static void Play(MusicTrack track, AudioClip overrideClip = null, float fadeSeconds = DefaultFadeSeconds)
        {
            Instance.PlayInternal(track, overrideClip, fadeSeconds);
        }

        /// <summary>Fades the bed out and forgets the current track.</summary>
        public static void Stop(float fadeSeconds = DefaultFadeSeconds)
        {
            if (HasInstance)
            {
                Instance.StopInternal(fadeSeconds);
            }
        }

        private void EnsureReady()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;

            // Qualify UnityEngine.Resources — the game has its own Assets.Scripts.Resources namespace.
            _bank = UnityEngine.Resources.Load<MusicBankSO>(BankResource);
            if (_bank == null)
            {
                Debug.LogWarning($"MusicPlayer: no MusicBankSO at Resources/{BankResource}; the game will have no music.");
            }

            _front = NewBed("MusicBedA");
            _back = NewBed("MusicBedB");
            AudioOptions.Apply();
        }

        private Bed NewBed(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D — music is not in the world
            source.volume = 0f;
            return new Bed { Source = source, Scale = 1f };
        }

        private void PlayInternal(MusicTrack track, AudioClip overrideClip, float fadeSeconds)
        {
            EnsureReady();

            // Decide *before* picking a clip: a multi-clip track picks at random, so comparing the
            // freshly picked clip against what is playing would restart the theme every call.
            if (track == CurrentTrack
                && _front.TargetWeight > 0f
                && (overrideClip == null || _front.Source.clip == overrideClip))
            {
                return; // already playing this — don't restart it
            }

            var entry = _bank != null ? _bank.Get(track) : null;
            var clip = overrideClip != null ? overrideClip : PickClip(entry);

            CurrentTrack = clip != null ? track : MusicTrack.None;
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);

            if (clip == null)
            {
                FadeOutFront();
                return;
            }

            // The outgoing bed fades away and the incoming one starts silent and rises, so a swap is
            // never a cut. Take the *quieter* bed for the incoming track rather than blindly swapping:
            // if two swaps land inside one fade (victory's Stop, then the summary's Continue restoring
            // the floor theme, is well under a second apart), a blind swap would hand the incoming
            // clip the bed still audibly playing the first track and cut it dead.
            bool backIsQuieter = IncomingTakesBackBed(_front.Weight, _back.Weight);
            var incoming = backIsQuieter ? _back : _front;
            var outgoing = backIsQuieter ? _front : _back;
            _front = incoming;
            _back = outgoing;

            _back.TargetWeight = 0f;

            _front.Source.clip = clip;
            _front.Scale = entry != null ? entry.Volume : 1f;
            _front.Weight = 0f;
            _front.TargetWeight = 1f;
            _front.Source.volume = 0f;
            _front.Source.Play();
        }

        private void StopInternal(float fadeSeconds)
        {
            if (!_ready)
            {
                return;
            }
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            CurrentTrack = MusicTrack.None;
            FadeOutFront();
        }

        private void FadeOutFront()
        {
            _front.TargetWeight = 0f;
            _back.TargetWeight = 0f;
        }

        /// <summary>
        /// Which of the two beds should carry an incoming track: the quieter one, because stealing an
        /// audible bed cuts its clip dead instead of fading it. In the ordinary case the back bed is
        /// already silent and this is just the obvious swap; it only differs when two swaps land
        /// inside one fade. Pure so the rule is testable without an <c>AudioSource</c> — see
        /// <c>AudioOptionsTests</c>.
        /// </summary>
        public static bool IncomingTakesBackBed(float frontWeight, float backWeight)
        {
            return backWeight <= frontWeight;
        }

        private static AudioClip PickClip(MusicBankSO.Entry entry)
        {
            if (entry == null || entry.Clips == null || entry.Clips.Length == 0)
            {
                return null;
            }
            return entry.Clips[Random.Range(0, entry.Clips.Length)];
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            float step = Time.unscaledDeltaTime / _fadeSeconds;
            float channel = AudioOptions.MusicVolume;
            Advance(_front, step, channel);
            Advance(_back, step, channel);
        }

        private static void Advance(Bed bed, float step, float channelVolume)
        {
            bed.Weight = Mathf.MoveTowards(bed.Weight, bed.TargetWeight, step);
            bed.Source.volume = bed.Weight * bed.Scale * channelVolume;

            if (bed.Weight <= 0f && bed.Source.isPlaying)
            {
                bed.Source.Stop(); // a silent source still costs a voice
            }
        }
    }
}
