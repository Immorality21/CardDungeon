using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using UnityEngine;

namespace Assets.Scripts.Combat.Triggers
{
    /// <summary>When a <see cref="TriggeredEffect"/> fires, always from its bearer's point of view.
    /// Serialized by ordinal: append, never insert.</summary>
    public enum TriggerKind
    {
        /// <summary>The fight begins.</summary>
        OnCombatStart = 0,

        /// <summary>The bearer's turn opens (before its start-of-turn ticks).</summary>
        OnTurnStart = 1,

        /// <summary>The bearer's turn closes (after its upkeep).</summary>
        OnTurnEnd = 2,

        /// <summary>The bearer's attack, ability or damage-over-time took health off someone else.</summary>
        OnDealDamage = 3,

        /// <summary>Someone else's attack, ability or damage-over-time took health off the bearer, and it
        /// is still standing.</summary>
        OnTakeDamage = 4,

        /// <summary>The bearer struck the blow that felled someone on the other side (an ally given to a
        /// Sacrifice does not count).</summary>
        OnKill = 5,

        /// <summary>The bearer went down. The effects still resolve, with the fallen bearer as their
        /// source - "explodes when it dies".</summary>
        OnDefeated = 6,

        /// <summary>Someone on the bearer's side went down.</summary>
        OnAllyDefeated = 7,

        /// <summary>The bearer used an ability (a cast, a summon's ability, an Ultra's strike).</summary>
        OnAbilityUsed = 8
    }

    /// <summary>How often a trigger may fire for one bearer.</summary>
    public enum TriggerLimit
    {
        Unlimited = 0,
        OncePerTurn = 1,
        OncePerCombat = 2
    }

    /// <summary>Who a trigger's effects land on.</summary>
    public enum TriggerTarget
    {
        /// <summary>The bearer itself.</summary>
        Bearer = 0,

        /// <summary>The other party to the event: the one killed (OnKill), the killer (OnDefeated), the
        /// one hit (OnDealDamage), the attacker (OnTakeDamage), the fallen ally (OnAllyDefeated). Events
        /// with no other party (turns, combat start, abilities) fire nothing.</summary>
        Other = 1,

        /// <summary>Every living unit on the bearer's side, the bearer included.</summary>
        AllAllies = 2,

        /// <summary>Every living unit on the other side.</summary>
        AllEnemies = 3,

        /// <summary>One living unit on the other side, at random.</summary>
        RandomEnemy = 4
    }

    /// <summary>
    /// A reaction authored on an item, an enemy or a summon: <b>when</b> something happens to its bearer
    /// in a fight, <see cref="Effects"/> resolve - through the same executors an ability uses, with the
    /// bearer as their source, so a reaction can be anything an ability can be. Flat payload (one class,
    /// inert fields) rather than subclasses, the codebase's idiom for authored data Unity has to
    /// serialize (see <c>SphereGridNode</c>, <c>EnemyActionEntry</c>).
    ///
    /// <para>Fired by <see cref="TriggerRegistry"/>, which the live fight and the balance simulator
    /// both build, so a reaction is in the model the moment it is authored.</para>
    /// </summary>
    [Serializable]
    public class TriggeredEffect
    {
        [Tooltip("Shown in the combat log when it fires, e.g. \"Soul Hunger\". Empty falls back to the item, enemy or summon's name.")]
        public string Label;

        [Tooltip("When it fires, from the bearer's point of view.")]
        public TriggerKind When;

        [Tooltip("Chance to fire each time the event happens, 0..1.")]
        [Range(0f, 1f)]
        public float Chance = 1f;

        [Tooltip("How often it may fire for one bearer.")]
        public TriggerLimit Limit;

        [Tooltip("Only fire for damage of one element (OnDealDamage, OnTakeDamage, OnKill, OnDefeated).")]
        public bool FilterDamageType;

        public DamageType DamageType;

        [Tooltip("Who the effects land on.")]
        public TriggerTarget Target;

        [Tooltip("What happens: resolved like an ability's effects, with the bearer as the caster.")]
        public List<SpellEffect> Effects = new List<SpellEffect>();
    }

    /// <summary>One trigger a unit carries, and what it came from (an item's or an enemy's name), for
    /// the combat log.</summary>
    public struct CarriedTrigger
    {
        public TriggeredEffect Trigger;
        public string From;

        public CarriedTrigger(TriggeredEffect trigger, string from)
        {
            Trigger = trigger;
            From = from;
        }
    }

    /// <summary>A combat unit that carries triggers: a hero (from their equipped gear), an enemy and a
    /// summon (from their definitions), and the simulator's stand-ins for all three.</summary>
    public interface ITriggerSource
    {
        /// <summary>Every trigger the unit carries right now. Read when an event happens, never cached,
        /// so a change of gear between fights needs no bookkeeping.</summary>
        IEnumerable<CarriedTrigger> GetTriggers();
    }

    /// <summary>A trigger fired: raised on the fight's <see cref="CombatEvents"/> after its effects
    /// resolved, for the combat log and the floating text.</summary>
    public class ReactionResolved
    {
        public ICombatUnit Bearer;
        public TriggeredEffect Trigger;
        public string From;
        public List<ICombatUnit> Targets = new List<ICombatUnit>();
        public EffectResult Result;

        /// <summary>"Soul Hunger" - the trigger's label, else what it came from.</summary>
        public string Name => !string.IsNullOrEmpty(Trigger?.Label) ? Trigger.Label : From;
    }
}
