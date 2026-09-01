using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    /// <summary>
    /// Maps each <see cref="MusicTrack"/> to one or more interchangeable loops (one is picked at
    /// random when the track starts, so a floor does not always open on the same theme) plus a
    /// per-track volume. Resources-loaded by <see cref="MusicPlayer"/> from <c>Resources/MusicBank</c>,
    /// mirroring <see cref="SoundBankSO"/> and the project's other Resources-loaded catalogs.
    ///
    /// <para>A track with no clips authored is not an error — the player fades to silence for it.
    /// That is deliberate: it keeps an unfinished bank obvious instead of quietly leaving the
    /// previous bed running under a fight it was not written for.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MusicBank", menuName = "SO/Music Bank")]
    public class MusicBankSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public MusicTrack Key;
            [Range(0f, 1f)] public float Volume = 0.6f;
            public AudioClip[] Clips;
        }

        public Entry[] Entries;

        private Dictionary<MusicTrack, Entry> _map;

        public Entry Get(MusicTrack key)
        {
            if (_map == null)
            {
                _map = new Dictionary<MusicTrack, Entry>();
                if (Entries != null)
                {
                    foreach (var e in Entries)
                    {
                        if (e != null)
                        {
                            _map[e.Key] = e;
                        }
                    }
                }
            }

            _map.TryGetValue(key, out var entry);
            return entry;
        }
    }
}
