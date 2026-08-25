using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>What one authored action does on the enemy's turn.</summary>
    public enum EnemyActionKind
    {
        /// <summary>A basic swing at one hero.</summary>
        Attack,

        /// <summary>A swing at one hero with a damage multiplier. Telegraph it to make it a charge.</summary>
        HeavyAttack,

        /// <summary>One hit on every living hero. Telegraph it to make it a boss signature.</summary>
        AoeAttack,

        /// <summary>Restores health to the most wounded of this enemy and its allies.</summary>
        Heal,

        /// <summary>A negative stat buff on a hero that does not already carry one.</summary>
        Debuff,

        /// <summary>Casts a magic — a named one, or one picked from this enemy's Draw list.</summary>
        CastMagic
    }

    /// <summary>The gate on whether an action is available at all this turn.</summary>
    public enum EnemyConditionKind
    {
        /// <summary>No gate.</summary>
        Always,

        /// <summary>This enemy's health fraction is at or below <see cref="EnemyActionCondition.Value"/>.</summary>
        SelfHealthBelow,

        /// <summary>This enemy's health fraction is above <see cref="EnemyActionCondition.Value"/>.</summary>
        SelfHealthAbove,

        /// <summary>This enemy or one of its allies is below full health.</summary>
        AllyWounded,

        /// <summary>Some living hero does not yet carry a negative buff on <see cref="EnemyActionCondition.Stat"/>.</summary>
        HeroMissingDebuff,

        /// <summary>Turns taken is an exact multiple of <see cref="EnemyActionCondition.Value"/>.</summary>
        EveryNthTurn,

        /// <summary>Not this enemy's opening turn, so a fight reads readably before a telegraph.</summary>
        NotFirstTurn
    }

    /// <summary>
    /// One gate on an action.
    ///
    /// <para>The vocabulary is a <b>closed enum on purpose.</b> `BalanceMath` has to price a
    /// behaviour in closed form — that is what every danger and attrition number in the project is
    /// drawn from — so a condition the analyzer cannot reason about would silently opt an enemy out
    /// of being measured. Adding a member means teaching <c>EnemyBehaviorModel</c> its expected
    /// occupancy at the same time.</para>
    /// </summary>
    [Serializable]
    public class EnemyActionCondition
    {
        public EnemyConditionKind Kind = EnemyConditionKind.Always;

        [Tooltip("Health fraction for SelfHealthBelow/Above, or N for EveryNthTurn.")]
        public float Value;

        [Tooltip("Which stat, for HeroMissingDebuff.")]
        public StatType Stat = StatType.Strength;
    }

    /// <summary>
    /// One action an enemy can take, and when. Authored on an <see cref="EnemyBehaviorSO"/>.
    ///
    /// <para><b>Selection order is gate, then priority, then weight.</b> An entry is eligible when
    /// every <see cref="Conditions"/> entry holds, its <see cref="ChanceGate"/> roll passes, and the
    /// action has somewhere to land. Among eligible entries the <b>highest
    /// <see cref="Priority"/></b> tier wins outright, and <see cref="Weight"/> picks between entries
    /// inside that tier. Two knobs rather than one because the existing behaviours need both: a boss
    /// is priority logic ("if the cadence is up, wind up the signature; otherwise swing"), while
    /// casting against attacking is a weighted coin flip.</para>
    /// </summary>
    [Serializable]
    public class EnemyActionEntry
    {
        [Tooltip("Authoring label only — never shown to the player. Say what the action is for.")]
        public string Label = "";

        public EnemyActionKind Kind = EnemyActionKind.Attack;

        [Tooltip("Higher tiers pre-empt lower ones entirely. Entries that share a priority compete " +
                 "on Weight. Leave ordinary attacks at 0 and put situational actions above them.")]
        public int Priority;

        [Tooltip("Relative likelihood against other eligible entries at the same Priority. Only " +
                 "ratios matter, so 0.75 against 0.25 is the same as 3 against 1.")]
        [Min(0f)]
        public float Weight = 1f;

        [Tooltip("Independent chance this entry is even considered, before Priority is looked at. " +
                 "0 means no gate (always considered). This is how an action pre-empts the whole " +
                 "repertoire some fraction of the time rather than competing within a tier.")]
        [Range(0f, 1f)]
        public float ChanceGate;

        [Tooltip("Spend one turn winding up, then deliver on the next. The wind-up is the player's " +
                 "window to heal, guard or kill it first, so anything big should telegraph. " +
                 "HeavyAttack and AoeAttack only.")]
        public bool Telegraphed;

        [Tooltip("Damage multiplier, for Attack / HeavyAttack / AoeAttack.")]
        public float Multiplier = 1f;

        [Tooltip("Heal amount, or debuff magnitude.")]
        public int Power;

        [Tooltip("Debuff duration in turns.")]
        public int Duration = 3;

        [Tooltip("Which stat a Debuff reduces.")]
        public StatType TargetStat = StatType.Strength;

        [Tooltip("CastMagic only. Leave empty to pick from this enemy's own DrawableMagics — which " +
                 "keeps the promise that what it throws is what you can steal from it. Name a magic " +
                 "for a signature the player cannot obtain.")]
        public MagicSO Magic;

        [Tooltip("Every one of these must hold for the action to be available.")]
        public List<EnemyActionCondition> Conditions = new List<EnemyActionCondition>();

        /// <summary>True for the two kinds that can be wound up over two turns.</summary>
        public bool CanTelegraph =>
            Kind == EnemyActionKind.HeavyAttack || Kind == EnemyActionKind.AoeAttack;

        /// <summary>Whether this entry actually spends a turn winding up.</summary>
        public bool IsTelegraphed => Telegraphed && CanTelegraph;

        public string DescribeForEditor()
        {
            string name = string.IsNullOrEmpty(Label) ? Kind.ToString() : Label;
            string gate = ChanceGate > 0f ? $" {ChanceGate:0%} gate" : "";
            string tele = IsTelegraphed ? " telegraphed" : "";
            return $"P{Priority} w{Weight:0.##} {name}{gate}{tele}";
        }
    }
}
