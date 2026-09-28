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

        /// <summary>
        /// True for a stat change authored as a percentage (<see cref="PowerMode.PercentOfTargetStat"/>).
        /// <see cref="Amount"/> is already the resolved delta; the flag only decides that a newer
        /// percentage buff on the same stat replaces this one instead of stacking with it.
        /// </summary>
        public bool IsPercent;

        /// <summary>
        /// True when this entry was applied to (or refreshed on) a unit during that unit's own turn.
        /// That turn's upkeep then leaves it alone - no over-time tick, no duration tick - so a
        /// 3-turn self-buff lasts three of the caster's turns, the same as it lasts everyone else's.
        /// Set by <see cref="CombatBuffTracker"/> while a turn is open, cleared by the next
        /// <see cref="CombatBuffTracker.TickBuffs"/> on that unit.
        /// </summary>
        public bool SkipNextUpkeep;
    }
}
