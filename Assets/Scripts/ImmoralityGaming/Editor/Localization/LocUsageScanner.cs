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
    }

    /// <summary>
    /// Finds where text keys are used, for the Texts window's "where used" and missing/unused report.
    ///
    /// <para>Literal keys are found by text search: <c>Loc.Get/GetOrEmpty/Format/Has("key")</c> in C# and
    /// <c>"#key"</c> attributes in UXML. Keys that are built at runtime (an asset's
    /// <c>hero.{Key}.name</c>) cannot be found that way, so whoever builds them registers a
    /// <see cref="Providers"/> entry that lists them per asset.</para>
    /// </summary>
    public static class LocUsageScanner
    {
        /// <summary>Sources of keys built at runtime. Register from an <c>[InitializeOnLoad]</c> class.</summary>
        public static readonly List<Func<IEnumerable<LocUsage>>> Providers = new List<Func<IEnumerable<LocUsage>>>();

        private static readonly Regex CodeKey = new Regex(@"Loc\.(?:GetOrEmpty|Get|Format|Has)\(\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex UxmlKey = new Regex(@"=""" + Regex.Escape(Loc.KeyPrefix.ToString()) + @"([A-Za-z0-9_.\-]+)""", RegexOptions.Compiled);

        public static List<LocUsage> ScanAll()
        {
            var usages = new List<LocUsage>();
            ScanFiles("*.cs", CodeKey, usages);
            ScanFiles("*.uxml", UxmlKey, usages);
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

        private static void ScanFiles(string pattern, Regex regex, List<LocUsage> usages)
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
                        usages.Add(new LocUsage { Key = match.Groups[1].Value, Path = relative, Line = i + 1 });
                    }
                }
            }
        }
    }
}
