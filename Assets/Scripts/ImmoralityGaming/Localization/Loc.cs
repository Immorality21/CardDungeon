using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Localization
{
    /// <summary>
    /// Turns a text key into words in the current language: <c>Loc.Get("ui.hub.forge.name")</c>.
    ///
    /// <para>Falls back to the default language (the table's first column) when the current one has no
    /// wording, and to the key itself when the table has no entry at all, so a missing text shows up on
    /// screen as its key instead of as a blank.</para>
    ///
    /// <para>Placeholders are named: the table holds <c>"{damage} damage"</c> and the code calls
    /// <c>Loc.Format("combat.ability.damage", ("damage", 18))</c>. Names, not positions, so a sentence
    /// can be reworded or translated in any order.</para>
    ///
    /// <para>UXML names a key by starting its text with <see cref="KeyPrefix"/>
    /// (<c>text="#ui.hub.forge.name"</c>); a screen calls <see cref="Localize"/> on its root once it is
    /// built.</para>
    /// </summary>
    public static class Loc
    {
        public const char KeyPrefix = '#';

        private static TextTableSO _table;
        private static string _language;
        private static int _languageIndex;

        /// <summary>The table in use: <c>Resources/TextTable.asset</c> unless <see cref="SetTable"/> swapped it.</summary>
        public static TextTableSO Table
        {
            get
            {
                if (_table == null)
                {
                    _table = UnityEngine.Resources.Load<TextTableSO>(TextTableSO.ResourcePath);
                    if (_table == null)
                    {
                        Debug.LogWarning($"[Loc] No text table at Resources/{TextTableSO.ResourcePath}; every text shows its key.");
                        _table = ScriptableObject.CreateInstance<TextTableSO>();
                    }
                    RefreshLanguageIndex();
                }
                return _table;
            }
        }

        /// <summary>The current language code; the table's default language until set.</summary>
        public static string Language
        {
            get
            {
                if (_language == null)
                {
                    _language = Table.Languages.Count > 0 ? Table.Languages[0] : "en";
                }
                return _language;
            }
            set
            {
                _language = value;
                RefreshLanguageIndex();
            }
        }

        /// <summary>Use another table (tests, the editor window). Null goes back to the Resources one.</summary>
        public static void SetTable(TextTableSO table)
        {
            _table = table;
            _language = null;
            if (_table != null)
            {
                _table.RebuildIndex();
            }
            RefreshLanguageIndex();
        }

        public static bool Has(string key)
        {
            return Table.Find(key) != null;
        }

        /// <summary>The words for <paramref name="key"/>; see the class summary for the fallbacks.</summary>
        public static string Get(string key)
        {
            var entry = Table.Find(key);
            if (entry == null)
            {
                return key ?? "";
            }
            string value = entry.ValueAt(_languageIndex);
            if (string.IsNullOrEmpty(value))
            {
                value = entry.ValueAt(0);
            }
            return string.IsNullOrEmpty(value) ? key : value;
        }

        /// <summary>Like <see cref="Get"/>, but "" when the table has no wording: for authored texts
        /// that may legitimately be empty (an item without a description), where the caller tests for
        /// empty to fall back to something else.</summary>
        public static string GetOrEmpty(string key)
        {
            var entry = Table.Find(key);
            if (entry == null)
            {
                return "";
            }
            string value = entry.ValueAt(_languageIndex);
            if (string.IsNullOrEmpty(value))
            {
                value = entry.ValueAt(0);
            }
            return value ?? "";
        }

        /// <summary>An asset's text (<see cref="GetOrEmpty"/>) by its derived key: <paramref name="id"/>,
        /// or the asset's name when the id is empty, as save keys fall back.</summary>
        public static string AssetText(string area, string id, Object asset, string field)
        {
            if (string.IsNullOrEmpty(id) && asset != null)
            {
                id = asset.name;
            }
            return GetOrEmpty(KeyFor(area, id, field));
        }

        /// <summary>The key of an asset's text:<c>KeyFor("ability", "AimedShot", "name")</c> is
        /// <c>ability.aimed-shot.name</c>.</summary>
        public static string KeyFor(string area, string id, string field)
        {
            return area + "." + Slug(id) + "." + field;
        }

        /// <summary>An identifier as a key segment: PascalCase, spaces and underscores become
        /// lowercase-hyphenated (<c>BloodStair_1</c> is <c>blood-stair-1</c>); dots are dropped so a
        /// segment never splits.</summary>
        public static string Slug(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "";
            }
            var sb = new StringBuilder(id.Length + 4);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == ' ' || c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                    {
                        sb.Append('-');
                    }
                    continue;
                }
                if (c == '.')
                {
                    continue;
                }
                bool boundary = i > 0 && sb.Length > 0 && sb[sb.Length - 1] != '-' &&
                                ((char.IsUpper(c) && (char.IsLower(id[i - 1]) || char.IsDigit(id[i - 1]) ||
                                                      (i + 1 < id.Length && char.IsUpper(id[i - 1]) && char.IsLower(id[i + 1])))) ||
                                 (char.IsDigit(c) && char.IsLetter(id[i - 1])));
                if (boundary)
                {
                    sb.Append('-');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Trim('-');
        }

        /// <summary><see cref="Get"/>, with each <c>{name}</c> replaced by its value. Unknown
        /// placeholders are left as written, so a typo shows on screen.</summary>
        public static string Format(string key, params (string Name, object Value)[] args)
        {
            return Fill(Get(key), args);
        }

        /// <summary>Replaces <c>{name}</c> placeholders in <paramref name="template"/>.</summary>
        public static string Fill(string template, params (string Name, object Value)[] args)
        {
            if (string.IsNullOrEmpty(template) || args == null || args.Length == 0 || template.IndexOf('{') < 0)
            {
                return template;
            }
            var sb = new StringBuilder(template);
            foreach (var (name, value) in args)
            {
                sb.Replace("{" + name + "}", value?.ToString() ?? "");
            }
            return sb.ToString();
        }

        /// <summary>Every text element (and tooltip) under <paramref name="root"/> whose text starts with
        /// <see cref="KeyPrefix"/> gets the words for that key. Run it once after a screen is built.</summary>
        public static void Localize(VisualElement root)
        {
            if (root == null)
            {
                return;
            }
            root.Query<VisualElement>().ForEach(element =>
            {
                if (element is TextElement text && IsKeyRef(text.text))
                {
                    text.text = Get(text.text.Substring(1));
                }
                if (IsKeyRef(element.tooltip))
                {
                    element.tooltip = Get(element.tooltip.Substring(1));
                }
            });
        }

        public static bool IsKeyRef(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Length > 1 && text[0] == KeyPrefix;
        }

        private static void RefreshLanguageIndex()
        {
            if (_table == null)
            {
                _languageIndex = 0;
                return;
            }
            int index = _language == null ? 0 : _table.LanguageIndex(_language);
            _languageIndex = index < 0 ? 0 : index;
        }

#if UNITY_EDITOR
        /// <summary>Editor only: writes the default-language wording of <paramref name="key"/> into the
        /// table asset (adding the entry), undoably. Empty text removes nothing; it just empties it.</summary>
        public static void EditorSet(string key, string text)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            var table = Table;
            UnityEditor.Undo.RecordObject(table, "Edit text");
            var entry = table.Find(key);
            if (entry == null)
            {
                entry = new TextEntry { Key = key };
                table.Entries.Add(entry);
                table.RebuildIndex();
            }
            entry.SetValue(0, text);
            UnityEditor.EditorUtility.SetDirty(table);
        }

        // Domain reload off: statics survive play mode, so drop the cached table and language.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _table = null;
            _language = null;
            _languageIndex = 0;
        }
#endif
    }
}
