using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ImmoralityGaming.Localization;
using UnityEngine;

namespace ImmoralityGaming.Editor.Localization
{
    /// <summary>One place a text key is used: a file (and line), or an asset whose key is derived.</summary>
    public struct LocUsage
    {
        public string Key;

        /// <summary>Project-relative path (<c>Assets/...</c>) to open.</summary>
        public string Path;

        /// <summary>1-based line, or 0 for an asset.</summary>
        public int Line;

        /// <summary>An asset text that may be left empty (an item with no description): such a key is
        /// not reported as missing from the table.</summary>
        public bool Optional;

        /// <summary>A key stem the code completes at runtime (<c>"items.rarity." + rarity</c>): it
        /// counts as a use of every key that starts with it.</summary>
        public bool Prefix;
    }

    /// <summary>
    /// Finds where text keys are used, for the Texts window's "where used" and missing/unused report.
    ///
    /// <para>Literal keys are found by text search: <c>Loc.Get/GetOrEmpty/Format/Has("key")</c> in C#
    /// (a required use: reported missing when the table lacks it), any other lowercase dotted string
    /// literal in C# (a key handed around as a value - counted as a use, never reported missing), and
    /// <c>"#key"</c> attributes in UXML. A literal ending in a dot is a stem completed at runtime and
    /// covers every key under it (<see cref="Index"/>). Keys built from an asset's identity
    /// (<c>hero.{Key}.name</c>) come from <see cref="Providers"/>.</para>
    /// </summary>
    public static class LocUsageScanner
    {
        /// <summary>Sources of keys built at runtime. Register from an <c>[InitializeOnLoad]</c> class.</summary>
        public static readonly List<Func<IEnumerable<LocUsage>>> Providers = new List<Func<IEnumerable<LocUsage>>>();

        private static readonly Regex CodeKey = new Regex(@"Loc\.(?:GetOrEmpty|Get|Format|Has)\(\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex CodeLiteral = new Regex(@"""([a-z][a-z0-9\-]*(?:\.[a-z0-9\-]+)+\.?)""", RegexOptions.Compiled);
        private static readonly Regex UxmlKey = new Regex(@"=""" + Regex.Escape(Loc.KeyPrefix.ToString()) + @"([A-Za-z0-9_.\-]+)""", RegexOptions.Compiled);

        public static List<LocUsage> ScanAll()
        {
            var usages = new List<LocUsage>();
            ScanFiles("*.cs", CodeKey, false, usages);
            ScanFiles("*.cs", CodeLiteral, true, usages);
            ScanFiles("*.uxml", UxmlKey, false, usages);
            foreach (var provider in Providers)
            {
                try
                {
                    usages.AddRange(provider());
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return usages;
        }

        /// <summary>Usages grouped by the table key they use, with each stem's usages added to every
        /// key in <paramref name="table"/> that starts with it.</summary>
        public static Dictionary<string, List<LocUsage>> Index(List<LocUsage> usages, TextTableSO table)
        {
            var index = new Dictionary<string, List<LocUsage>>(StringComparer.Ordinal);
            foreach (var usage in usages)
            {
                if (!usage.Prefix)
                {
                    AddTo(index, usage.Key, usage);
                    continue;
                }
                foreach (var entry in table.Entries)
                {
                    if (entry.Key != null && entry.Key.StartsWith(usage.Key, StringComparison.Ordinal))
                    {
                        AddTo(index, entry.Key, usage);
                    }
                }
            }
            return index;
        }

        private static void AddTo(Dictionary<string, List<LocUsage>> index, string key, LocUsage usage)
        {
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<LocUsage>();
                index[key] = list;
            }
            list.Add(usage);
        }

        private static void ScanFiles(string pattern, Regex regex, bool optional, List<LocUsage> usages)
        {
            string root = Application.dataPath;
            foreach (string file in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
            {
                string relative = "Assets" + file.Substring(root.Length).Replace('\\', '/');
                // Tests use made-up keys on purpose; they are not the game's texts.
                if (relative.StartsWith("Assets/Tests/", StringComparison.Ordinal))
                {
                    continue;
                }
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    // Doc comments quote example keys.
                    if (lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (Match match in regex.Matches(lines[i]))
                    {
                        string key = match.Groups[1].Value;
                        bool prefix = key.EndsWith(".", StringComparison.Ordinal);
                        usages.Add(new LocUsage
                        {
                            Key = key,
                            Path = relative,
                            Line = i + 1,
                            Optional = optional || prefix,
                            Prefix = prefix,
                        });
                    }
                }
            }
        }
    }
}
