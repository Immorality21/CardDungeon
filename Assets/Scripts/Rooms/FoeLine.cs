using System.Collections.Generic;

namespace Assets.Scripts.Rooms
{
    /// <summary>One enemy in a room, as the Fight / Flee heading needs it.</summary>
    public struct FoeEntry
    {
        /// <summary>What groups two enemies together - the definition's save key.</summary>
        public string Kind;
        public string Name;
        /// <summary>Whether the bestiary has met this kind; an unmet one is never named.</summary>
        public bool Seen;

        public FoeEntry(string kind, string name, bool seen)
        {
            Kind = kind;
            Name = name;
            Seen = seen;
        }
    }

    /// <summary>
    /// The line above the Fight / Flee bar: who is in the room, with no stats. Pure, so the wording is
    /// testable without a scene.
    ///
    /// <para>Met enemies are named and grouped by kind ("Slag Hound x2 + Cinder Imp"); every unmet one
    /// is folded into a single count at the end ("+ 2 unknown"). The first version gave each unmet
    /// <i>kind</i> its own "???", which read as "??? + ??? + ??? x2" in a room of strangers - a
    /// row of question marks that took counting to parse, and said nothing the sprites did not.</para>
    /// </summary>
    public static class FoeLine
    {
        public static string Describe(IEnumerable<FoeEntry> foes)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            var names = new Dictionary<string, string>();
            int unknown = 0;
            if (foes != null)
            {
                foreach (var foe in foes)
                {
                    if (!foe.Seen)
                    {
                        unknown++;
                        continue;
                    }
                    string kind = string.IsNullOrEmpty(foe.Kind) ? foe.Name : foe.Kind;
                    if (!counts.ContainsKey(kind))
                    {
                        counts[kind] = 0;
                        order.Add(kind);
                        names[kind] = foe.Name;
                    }
                    counts[kind]++;
                }
            }

            var parts = new List<string>();
            foreach (var kind in order)
            {
                parts.Add(counts[kind] > 1 ? $"{names[kind]} x{counts[kind]}" : names[kind]);
            }
            if (unknown > 0)
            {
                if (parts.Count > 0)
                {
                    parts.Add(unknown + " unknown");
                }
                else
                {
                    parts.Add(unknown == 1 ? "An unknown foe" : unknown + " unknown foes");
                }
            }
            return string.Join(" + ", parts);
        }
    }
}
