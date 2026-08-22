using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>Who an outcome's effects land on.</summary>
    public enum RoomEventTargets
    {
        /// <summary>Only the hero the check was resolved against - the one who reached in.</summary>
        ActingHero = 0,

        /// <summary>Every living hero. For anything the whole party shares: a cave-in, a blessing.</summary>
        WholeParty = 1
    }

    /// <summary>
    /// One thing that can happen when a <see cref="RoomEventOption"/> resolves. An option holds a
    /// weighted <i>pool</i> of these per branch, so "you get the tome" and "you get the tome and the
    /// spider bite" can both live in the success pool - a partial success is an outcome that carries
    /// a reward and a cost, not a third branch.
    ///
    /// <para>Nothing here is a new effect system: <see cref="Effects"/> are the same
    /// <see cref="SpellEffect"/>s magic uses (run through the same executors),
    /// <see cref="LootTable"/> rolls through <c>LootRoller</c>, and gold goes through the run's
    /// pending-gold pool so it is lost on death like every other in-run reward.</para>
    /// </summary>
    [Serializable]
    public class RoomEventOutcome
    {
        [TextArea]
        [Tooltip("What the player is told happened. This is the outcome's whole presentation.")]
        public string Text;

        [Tooltip("Relative likelihood within its pool. Two outcomes at 3 and 1 land 75% / 25%.")]
        public int Weight = 1;

        [Tooltip("Optional: a stat on the acting hero that bends this outcome's weight. Luck is " +
                 "the obvious one - fortune deciding how a gamble lands - but it is an authoring " +
                 "choice, so an outcome can just as well turn on Endurance or Spirit.")]
        public StatType WeightModifierStat = StatType.None;

        [Tooltip("How hard that stat pushes, as a percent per point: effective weight = " +
                 "Weight * (1 + stat * rate / 100). POSITIVE favours this outcome (the clean " +
                 "success, the glancing failure); NEGATIVE steers away from it (the bite, the " +
                 "collapse). 0 - the default - leaves the outcome purely weight-driven, so this is " +
                 "opt-in per outcome.")]
        public float WeightModifierRate;

        [Tooltip("Damage/heal land at once; buffs and debuffs are recorded as level afflictions " +
                 "(see LevelAfflictionTracker) because there is no combat running to hold them. " +
                 "Power is used flat - an event's numbers are the event's, not the party's.")]
        public List<SpellEffect> Effects = new List<SpellEffect>();

        public RoomEventTargets Targets = RoomEventTargets.ActingHero;

        [Tooltip("Rolled through LootRoller, so rarity and run depth decide whether each item " +
                 "actually drops - a treasure event is a chance at gear, not a guarantee.")]
        public List<ItemSO> LootTable = new List<ItemSO>();

        [Tooltip("Added to the run's pending gold, so it is banked on level-clear and lost on death.")]
        public int Gold;

        [Tooltip("Spends one consumable from the belt, if the party is carrying any. The cheapest " +
                 "failure cost there is: it hurts most when the party is already thin.")]
        public bool LoseAConsumable;

        [Tooltip("The noise woke something. These spawn into the room, turning a safe room into a " +
                 "fight the player did not choose.")]
        public List<EnemySO> AwakenedEnemies = new List<EnemySO>();
    }
}
