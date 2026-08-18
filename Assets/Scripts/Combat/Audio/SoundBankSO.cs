using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Combat.Audio
{
    /// <summary>
    /// Maps each <see cref="CombatSound"/> event to one or more interchangeable clips (a random
    /// one is picked per play, so repeated hits don't sound identical) plus a per-event volume.
    /// Resources-loaded by <see cref="CombatAudio"/> from <c>Resources/CombatSoundBank</c>, mirroring
    /// the project's other Resources-loaded catalogs (MagicCatalog, ItemCatalog, CombatIcons).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSoundBank", menuName = "SO/Combat Sound Bank")]
    public class SoundBankSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public CombatSound Key;
            [Range(0f, 1f)] public float Volume = 1f;
            public AudioClip[] Clips;
        }

        public Entry[] Entries;

        private Dictionary<CombatSound, Entry> _map;

        public Entry Get(CombatSound key)
        {
            if (_map == null)
            {
                _map = new Dictionary<CombatSound, Entry>();
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
