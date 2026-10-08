using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Combat state a behavior needs to decide its action.</summary>
    public class EnemyCombatContext
    {
        public List<ICombatUnit> Heroes;        // living heroes (attack/debuff targets)
        public List<ICombatUnit> Allies;        // living enemy allies, excluding self
        public CombatBuffTracker BuffTracker;
        public int SelfTurnCount;               // turns this enemy has already taken (cadence conditions)

        /// <summary>
        /// Index into the behaviour's <c>Actions</c> of the telegraphed action currently in flight, or
        /// <see cref="EnemyActionPlanner.NoCharge"/>. This replaced a bare "is charging" bool: with
        /// telegraphs authored per action rather than fixed per archetype, knowing *that* an enemy is
        /// winding up is no longer enough to know what it is about to deliver.
        /// </summary>
        public int ChargingEntryIndex = EnemyActionPlanner.NoCharge;

        /// <summary>
        /// This fight's threat, which biases every single-hero pick toward whoever has dealt the most
        /// damage and healing. Null is even odds - what the balance simulator uses.
        /// </summary>
        public ThreatTable Threat;

        /// <summary>What this enemy can cast (its own spell list) — see EnemyMagicPlan.</summary>
        public List<EnemySpellEntry> Spells;

        /// <summary>
        /// Who is covering whom this fight (<c>CombatEvents.Guards</c>), so a Guard action does not pick an
        /// ally another guard already covers. Null reads as nobody covered.
        /// </summary>
        public GuardTable Guards;

        /// <summary>
        /// How many more bodies the enemy side can hold before the stage is full
        /// (<c>EnemyFormation.DesignMax</c> less the living enemies, this one included). A Summon with no
        /// room is not eligible.
        /// </summary>
        public int OpenSlots;

        /// <summary>
        /// How many times this enemy has used each authored action this fight, by index into the
        /// behaviour's <c>Actions</c>. Read for <c>EnemyActionEntry.MaxUses</c>; null reads as never.
        /// </summary>
        public IReadOnlyDictionary<int, int> ActionUses;

        /// <summary>Times the action at <paramref name="index"/> has been used this fight.</summary>
        public int UsesOf(int index)
        {
            return ActionUses != null && ActionUses.TryGetValue(index, out int uses) ? uses : 0;
        }

        /// <summary>True while a telegraphed action is in flight.</summary>
        public bool SelfIsCharging => ChargingEntryIndex >= 0;
    }
}
