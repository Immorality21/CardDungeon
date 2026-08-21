using System;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// A unit's live stats: a <see cref="StatBlock"/> of attributes plus current health.
    ///
    /// <para>Health is the one value that is deliberately *not* a stat. Every other value is a
    /// standing property of the unit that gear, buffs and level-ups modify; current health is a
    /// consumable resource that combat spends and <c>Party.HealAll()</c> refills. <c>MaxHealth</c>
    /// lives in the block (gear and levels raise it), and is exposed here as a property because it
    /// is read constantly alongside <see cref="Health"/>.</para>
    /// </summary>
    [Serializable]
    public class Stats
    {
        [Tooltip("Current health. A resource, not a stat - see MaxHealth for the size of the bar.")]
        public int Health;

        [Tooltip("Every standing stat this unit has. Absent entries read as 0.")]
        public StatBlock Attributes = new StatBlock();

        public Stats() { }

        /// <summary>
        /// Builds live stats from an authored block. Health starts full, which is what every caller
        /// wanted from the old positional constructor.
        /// </summary>
        public Stats(StatBlock attributes)
        {
            Attributes = attributes != null ? attributes.Clone() : new StatBlock();
            Health = MaxHealth;
        }

        /// <summary>Reads or writes one stat. The single accessor - no per-stat fields.</summary>
        public int this[StatType stat]
        {
            get { return Attributes[stat]; }
            set { Attributes[stat] = value; }
        }

        /// <summary>Size of the health bar. Stored in <see cref="Attributes"/> like any other stat.</summary>
        public int MaxHealth
        {
            get { return Attributes[StatType.MaxHealth]; }
            set { Attributes[StatType.MaxHealth] = value; }
        }

        public Stats Clone()
        {
            return new Stats
            {
                Health = Health,
                Attributes = Attributes.Clone()
            };
        }

        public override string ToString()
        {
            return "HP " + Health + "/" + MaxHealth + "  " + Attributes;
        }
    }
}
