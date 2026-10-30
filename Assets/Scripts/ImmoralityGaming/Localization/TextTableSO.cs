using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmoralityGaming.Localization
{
    /// <summary>
    /// One player-facing text: its key and its wording per language (<see cref="TextTableSO.Languages"/>
    /// order; index 0 is the default language).
    /// </summary>
    [Serializable]
    public class TextEntry
    {
        public string Key = "";
        public List<string> Values = new List<string>();

        [Tooltip("Ticked once the wording has had a human pass. The Texts window filters on it.")]
        public bool Reviewed;

        [Tooltip("Context for whoever writes or translates the line: where it shows, what the placeholders mean.")]
        public string Note = "";

        public string ValueAt(int language)
        {
            return language >= 0 && language < Values.Count ? Values[language] : null;
        }

        public void SetValue(int language, string value)
        {
            while (Values.Count <= language)
            {
                Values.Add("");
            }
            Values[language] = value ?? "";
        }
    }

    /// <summary>
    /// Every player-facing text in the game, keyed (<c>hero.warrior.name</c>, <c>ui.hub.forge.name</c>),
    /// one column per language. Code, UXML and assets hold keys; <see cref="Loc"/> turns them into words.
    /// Lives at <c>Assets/Resources/TextTable.asset</c> and is edited in <c>Tools ▸ Texts</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Text Table")]
    public class TextTableSO : ScriptableObject
    {
        public const string ResourcePath = "TextTable";

        [Tooltip("Language codes, one column each. The first is the default every other language falls back to.")]
        public List<string> Languages = new List<string> { "en" };

        public List<TextEntry> Entries = new List<TextEntry>();

        [NonSerialized] private Dictionary<string, TextEntry> _index;

        /// <summary>The entry for <paramref name="key"/>, or null.</summary>
        public TextEntry Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            if (_index == null)
            {
                RebuildIndex();
            }
            return _index.TryGetValue(key, out var entry) ? entry : null;
        }

        public int LanguageIndex(string language)
        {
            return Languages.IndexOf(language);
        }

        /// <summary>Call after adding, removing or renaming entries so <see cref="Find"/> sees them.</summary>
        public void RebuildIndex()
        {
            _index = new Dictionary<string, TextEntry>(StringComparer.Ordinal);
            foreach (var entry in Entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Key))
                {
                    _index[entry.Key] = entry;
                }
            }
        }

        private void OnValidate()
        {
            _index = null;
        }
    }
}
