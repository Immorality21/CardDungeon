using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Works <see cref="MomentumRule"/> for one fight, off its <see cref="CombatEvents"/>: a bearer's turn
    /// opens with no hit landed, a hit or ability of theirs that takes health off the other side marks
    /// it, and when the turn closes the stacks go up by one or back to none
    /// (<see cref="MomentumOps.Next"/>). What the stacks are worth sits on the bearer as one keyed
    /// Strength buff (<see cref="CombatBuffTracker.SetKeyedBuff"/>), so every attack and ability reads
    /// it like any other Strength buff, and it never counts down on its own.
    ///
    /// <para>Built per fight by its owner - <c>CombatManager.RunCombat</c> and the simulator's encounter
    /// loop - so Momentum is in the balance model as it is in the game. Ticks, health prices and a
    /// reaction's hits never count: only what the bearer did with their turn.</para>
    /// </summary>
    public sealed class MomentumTracker
    {
        /// <summary>The key the stacks' Strength is filed under on the bearer.</summary>
        public const string BuffKey = "momentum";

        private readonly CombatBuffTracker _buffs;
        private readonly Dictionary<ICombatUnit, int> _stacks = new Dictionary<ICombatUnit, int>();
        private readonly HashSet<ICombatUnit> _landed = new HashSet<ICombatUnit>();

        /// <summary>A bearer's stacks moved (unit, new count) - for the floating text.</summary>
        public event Action<ICombatUnit, int> Changed;

        public MomentumTracker(CombatEvents events, CombatBuffTracker buffs)
        {
            _buffs = buffs;
            if (events == null)
            {
                return;
            }
            events.Subscribe<TurnStarted>(e => _landed.Remove(e.Unit));
            events.Subscribe<HealthChange>(OnHealthChange);
            events.Subscribe<TurnEnded>(OnTurnEnded);
            // A revived hero starts over (the keyed buff went with the rest of their buffs).
            events.Subscribe<UnitRevived>(e =>
            {
                _stacks.Remove(e.Unit);
                _landed.Remove(e.Unit);
            });
        }

        /// <summary>A unit's stacks right now (0 for anyone without Momentum).</summary>
        public int StacksOf(ICombatUnit unit)
        {
            return unit != null && _stacks.TryGetValue(unit, out int stacks) ? stacks : 0;
        }

        private static MomentumRule RuleOf(ICombatUnit unit)
        {
            var rule = (unit as IHasMomentum)?.Momentum;
            return rule != null && rule.IsActive ? rule : null;
        }

        private void OnHealthChange(HealthChange change)
        {
            var attacker = change.Attacker;
            if (change.Landed <= 0 || change.Source.Reaction || attacker == null || change.Target == null)
            {
                return;
            }
            if (change.Cause != HealthCause.Attack && change.Cause != HealthCause.Ability)
            {
                return;
            }
            // Only a blow on the other side builds anything.
            if (change.Target.IsHero == attacker.IsHero || RuleOf(attacker) == null)
            {
                return;
            }
            _landed.Add(attacker);
        }

        private void OnTurnEnded(TurnEnded e)
        {
            var rule = RuleOf(e.Unit);
            if (rule == null)
            {
                return;
            }
            int before = StacksOf(e.Unit);
            int after = MomentumOps.Next(before, _landed.Contains(e.Unit), rule);
            _landed.Remove(e.Unit);
            _stacks[e.Unit] = after;
            int strength = e.Unit.GetEffectiveStat(StatType.Strength);
            _buffs?.SetKeyedBuff(e.Unit, StatType.Strength, MomentumOps.StrengthBonus(strength, after, rule), BuffKey);
            if (after != before)
            {
                Changed?.Invoke(e.Unit, after);
            }
        }
    }
}
