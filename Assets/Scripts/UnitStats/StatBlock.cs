using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UnitStats
{
    /// <summary>One stat and its value. The unit of stat data everywhere: authoring, gains, bonuses.</summary>
    [Serializable]
    public class UnitStat
    {
        public StatType Type;
        public int Amount;

        public UnitStat() { }

        public UnitStat(StatType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// A sparse set of <see cref="UnitStat"/> entries, addressed by <see cref="StatType"/>. Replaces
    /// the parallel lists of hard-coded int fields that used to live on <c>Stats</c>, <c>HeroSO</c>,
    /// <c>EnemySO</c>, <c>SphereGridNode</c> and the balance model — adding a stat used to mean
    /// editing eight declarations plus their copy sites; now it means one enum member.
    ///
    /// <para>Sparse on purpose: an absent entry reads 0, so an asset only lists what it actually has
    /// and a new stat does not need back-filling across every existing asset.</para>
    ///
    /// <para>Duplicate entries for one <see cref="StatType"/> <b>sum</b>, matching how
    /// <c>DamageCalculator.GetResistance</c> already treats duplicate resistances. That makes
    /// concatenating blocks (base + level gains + gear) safe without de-duplicating first.</para>
    /// </summary>
    [Serializable]
    public class StatBlock
    {
        [Tooltip("Only the stats this unit actually has. Anything absent reads as 0.")]
        public List<UnitStat> Values = new List<UnitStat>();

        public StatBlock() { }

        public StatBlock(params UnitStat[] entries)
        {
            if (entries != null)
            {
                Values.AddRange(entries);
            }
        }

        /// <summary>
        /// Reads the summed value for a stat (0 when absent). Writing collapses the stat to a single
        /// entry, adding one if it was absent and dropping any duplicates.
        /// </summary>
        public int this[StatType stat]
        {
            get
            {
                if (stat == StatType.None)
                {
                    return 0;
                }

                int total = 0;
                for (int i = 0; i < Values.Count; i++)
                {
                    if (Values[i] != null && Values[i].Type == stat)
                    {
                        total += Values[i].Amount;
                    }
                }
                return total;
            }
            set
            {
                if (stat == StatType.None)
                {
                    return;
                }

                bool written = false;
                for (int i = Values.Count - 1; i >= 0; i--)
                {
                    if (Values[i] == null || Values[i].Type != stat)
                    {
                        continue;
                    }
                    if (written)
                    {
                        Values.RemoveAt(i);   // collapse duplicates on write
                    }
                    else
                    {
                        Values[i].Amount = value;
                        written = true;
                    }
                }

                if (!written)
                {
                    Values.Add(new UnitStat(stat, value));
                }
            }
        }

        /// <summary>
        /// Adds every entry of <paramref name="other"/> to this block. Used for level gains.
        /// Adding a block to itself is rejected rather than allowed to throw: the setter can
        /// remove entries, which would invalidate the enumerator mid-loop.
        /// </summary>
        public void Add(StatBlock other)
        {
            if (other == null || ReferenceEquals(other, this))
            {
                return;
            }
            foreach (var entry in other.Values)
            {
                if (entry != null && entry.Type != StatType.None)
                {
                    this[entry.Type] = this[entry.Type] + entry.Amount;
                }
            }
        }

        /// <summary>Adds a single stat, creating the entry if needed.</summary>
        public void Add(StatType stat, int amount)
        {
            if (stat == StatType.None || amount == 0)
            {
                return;
            }
            this[stat] = this[stat] + amount;
        }

        /// <summary>
        /// A block seeded from <see cref="StatCatalog"/>'s authoring defaults, for a newly created
        /// hero or enemy. Exists because the old per-stat int fields carried inline defaults and an
        /// empty block does not: a fresh EnemySO with 0 MaxHealth spawns already dead, which is a
        /// baffling thing for a designer to debug.
        /// </summary>
        public static StatBlock Defaults()
        {
            var block = new StatBlock();
            foreach (var definition in StatCatalog.All)
            {
                if (definition.AuthoringDefault != 0)
                {
                    block[definition.Type] = definition.AuthoringDefault;
                }
            }
            return block;
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock();
            foreach (var entry in Values)
            {
                if (entry != null)
                {
                    copy.Values.Add(new UnitStat(entry.Type, entry.Amount));
                }
            }
            return copy;
        }

        /// <summary>
        /// Every stat that carries a non-zero value, for display and iteration. Deliberately not the
        /// enum's full range: callers that want "all stats" should walk <see cref="StatCatalog.Types"/>.
        /// </summary>
        public IEnumerable<UnitStat> NonZero()
        {
            foreach (var entry in Values)
            {
                if (entry != null && entry.Type != StatType.None && entry.Amount != 0)
                {
                    yield return entry;
                }
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var stat in StatCatalog.Types)
            {
                int value = this[stat];
                if (value != 0)
                {
                    parts.Add(StatCatalog.ShortName(stat) + " " + value);
                }
            }
            return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : "(no stats)";
        }
    }

}
