using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// One timed entry on a unit: a stat change, a status effect, or an elemental resistance. The
    /// three are distinguished by the flags rather than by subclasses, so they all tick down through
    /// the same <see cref="CombatBuffTracker.TickBuffs"/> path and expire together.
    /// </summary>
    public class CombatBuff
    {
        public StatType Stat;
        public int Amount;
        public int TurnsRemaining;
        public bool IsStatusEffect;
        public BuffType BuffType;

        /// <summary>
        /// True for a temporary elemental resistance, where <see cref="ResistanceType"/> is the
        /// element and <see cref="Amount"/> is the percentage. Kept as a flag on the existing record
        /// deliberately: a resistance buff has to expire like every other buff, and duplicating the
        /// lifetime bookkeeping is where the bugs would live.
        /// </summary>
        public bool IsResistance;

        public DamageType ResistanceType;
    }
}
