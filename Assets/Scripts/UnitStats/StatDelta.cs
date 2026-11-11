using System;
using System.Collections.Generic;

namespace Assets.Scripts.UnitStats
{
    /// <summary>One stat and how much a change moves it.</summary>
    public struct StatDeltaLine
    {
        public StatType Stat;
        public int Amount;
    }

    /// <summary>
    /// How every hub screen shows what a change does to a stat: <b>only the change</b>, signed and
    /// coloured - "Strength +3" in the better colour, "HP −6" in the worse one (owner, 2026-11-10,
    /// playtest 4 findings 41, 42, 43, 65). Before/after pairs ("Strength 10 13") read as two
    /// unexplained numbers. The sphere grid's node detail, the Storehouse's "Stats if equipped", its
    /// candidate rows and the Merchant's "If X wore it" all go through here, so they cannot drift.
    ///
    /// <para>Pure and scene-free (<c>StatDeltaTests</c>). Higher is better for every stat today; a
    /// stat where less is better would need a row in <see cref="StatCatalog"/> and a branch in
    /// <see cref="ClassFor(int)"/>.</para>
    /// </summary>
    public static class StatDelta
    {
        /// <summary>USS class for a change that helps (CardDungeon.uss).</summary>
        public const string BetterClass = "cd-delta--better";

        /// <summary>USS class for a change that hurts (CardDungeon.uss).</summary>
        public const string WorseClass = "cd-delta--worse";

        /// <summary>A real minus sign: a hyphen is narrower than a plus and reads as a dash.</summary>
        public const string Minus = "−";

        /// <summary>"+3", "−6", "0".</summary>
        public static string Format(int amount)
        {
            if (amount > 0)
            {
                return "+" + amount;
            }
            if (amount < 0)
            {
                return Minus + (-amount);
            }
            return "0";
        }

        /// <summary>
        /// A change in percentage points, rounded to whole points: "+10%", "−5%", "0%". Resistances
        /// are stored as points already (20 means 20%).
        /// </summary>
        public static string FormatPercent(float points)
        {
            return Format(RoundPoints(points)) + "%";
        }

        /// <summary>A change rounded the way <see cref="FormatPercent"/> shows it.</summary>
        public static int RoundPoints(float points)
        {
            return (int)Math.Round(points, MidpointRounding.AwayFromZero);
        }

        /// <summary><see cref="BetterClass"/>, <see cref="WorseClass"/>, or null for no change.</summary>
        public static string ClassFor(int amount)
        {
            if (amount > 0)
            {
                return BetterClass;
            }
            if (amount < 0)
            {
                return WorseClass;
            }
            return null;
        }

        /// <summary>
        /// Every stat that differs between <paramref name="before"/> and <paramref name="after"/>, in
        /// <see cref="StatCatalog.Types"/> order. Unchanged stats are left out. A null block reads as
        /// all zeros.
        /// </summary>
        public static List<StatDeltaLine> Changes(StatBlock before, StatBlock after)
        {
            var changes = new List<StatDeltaLine>();
            foreach (var stat in StatCatalog.Types)
            {
                int from = before != null ? before[stat] : 0;
                int to = after != null ? after[stat] : 0;
                if (to != from)
                {
                    changes.Add(new StatDeltaLine { Stat = stat, Amount = to - from });
                }
            }
            return changes;
        }

        /// <summary>
        /// The <paramref name="count"/> largest changes by size, biggest first; ties keep
        /// <see cref="StatCatalog.Types"/> order.
        /// </summary>
        public static List<StatDeltaLine> Biggest(StatBlock before, StatBlock after, int count)
        {
            var changes = Changes(before, after);
            var order = new List<int>();
            for (int i = 0; i < changes.Count; i++)
            {
                order.Add(i);
            }
            // Stable: equal sizes fall back to catalog order (the original index).
            order.Sort((a, b) =>
            {
                int bySize = Math.Abs(changes[b].Amount).CompareTo(Math.Abs(changes[a].Amount));
                return bySize != 0 ? bySize : a.CompareTo(b);
            });
            var result = new List<StatDeltaLine>();
            for (int i = 0; i < order.Count && i < count; i++)
            {
                result.Add(changes[order[i]]);
            }
            return result;
        }
    }
}
