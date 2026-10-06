using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// What one rank of a revisit condition does. <b>Serialized by ordinal: append, never reorder.</b>
    /// A new kind costs one member here and one case in <see cref="RevisitOps.Resolve"/>; a new
    /// condition built from existing kinds costs no code at all.
    /// </summary>
    public enum RunModifierEffectKind
    {
        /// <summary>+N% enemy MaxHealth per rank, bosses included.</summary>
        EnemyHealthPercent = 0,

        /// <summary>+N% raw damage per rank on everything an enemy deals.</summary>
        EnemyDamagePercent = 1,

        /// <summary>+N enemies per rank in every ordinary room that already holds one.</summary>
        ExtraEnemiesPerRoom = 2,

        /// <summary>N% per rank on healing heroes receive. Author negative: -50 halves it, -100 stops it.</summary>
        HeroHealingPercent = 3,

        /// <summary>
        /// +N% enemy Agility per rank: they act sooner and more often. Added after the first playtest
        /// found fear 0 trivial because a fast party simply acted first (revisit playtest finding 12).
        /// </summary>
        EnemyAgilityPercent = 4,
    }

    [Serializable]
    public class RunModifierEffect
    {
        public RunModifierEffectKind Kind;

        [Tooltip("Applied once per rank. Percent kinds are percentage points (25 = +25%); " +
                 "HeroHealingPercent is authored negative.")]
        public int AmountPerRank;
    }

    /// <summary>
    /// One condition the player can turn on for a revisit - Hades' Pact of Punishment, one row of it.
    /// Lives in <see cref="RevisitRulesSO.Modifiers"/>: adding one is a new list entry.
    /// </summary>
    [Serializable]
    public class RunModifier
    {
        [Tooltip("Save identity, written into Run.json. Write-once once a build reaches a player.")]
        public string Key;

        public string DisplayName;

        [TextArea(1, 3)]
        [Tooltip("What one rank does, in the player's words. {0} is replaced by the rank's total " +
                 "amount, so \"Enemies have +{0}% health\" reads +25% at rank 1 and +50% at rank 2.")]
        public string Description;

        [Min(1)] public int MaxRank = 1;

        [Tooltip("Fear each rank adds. Fear is what the rewards scale with.")]
        [Min(0)] public int FearPerRank = 1;

        public List<RunModifierEffect> Effects = new List<RunModifierEffect>();
    }

    /// <summary>A condition the player chose, and at which rank. What <c>Run.json</c> stores.</summary>
    [Serializable]
    public class RunModifierSelection
    {
        public string Key;
        public int Rank;

        public RunModifierSelection()
        {
        }

        public RunModifierSelection(string key, int rank)
        {
            Key = key;
            Rank = rank;
        }
    }
}
