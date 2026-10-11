using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Works <see cref="FocusRule"/> for one fight, off its <see cref="CombatEvents"/>: a bearer's turn
    /// opens with nothing cast, an ability of theirs marks it, and when the turn closes their Focus goes up
    /// (<see cref="FocusOps.Next"/>). <see cref="Spend"/> takes all of it for a MultiCast, and the turn it
    /// is spent on builds nothing - the repeats of a MultiCast are not casts that earn Focus back.
    ///
    /// <para>Only the bearer's own abilities count: a summon's special attack and an Ultra's strike resolve
    /// through throwaway castables (<see cref="SummonOps.CastKeyPrefix"/>, <see cref="UltraOps.CastKeyPrefix"/>)
    /// and are not casts. Built per fight by its owner - <c>CombatManager.RunCombat</c> and the simulator's
    /// encounter loop - so Focus is in the balance model as it is in the game.</para>
    /// </summary>
    public sealed class FocusTracker
    {
        private readonly Dictionary<ICombatUnit, int> _focus = new Dictionary<ICombatUnit, int>();
        private readonly HashSet<ICombatUnit> _cast = new HashSet<ICombatUnit>();
        private readonly HashSet<ICombatUnit> _spent = new HashSet<ICombatUnit>();

        /// <summary>A bearer's Focus moved (unit, new amount) - for the floating text.</summary>
        public event Action<ICombatUnit, int> Changed;

        public FocusTracker(CombatEvents events)
        {
            if (events == null)
            {
                return;
            }
            events.Subscribe<TurnStarted>(e =>
            {
                _cast.Remove(e.Unit);
                _spent.Remove(e.Unit);
            });
            events.Subscribe<AbilityUsed>(OnAbilityUsed);
            events.Subscribe<TurnEnded>(OnTurnEnded);
            // A revived hero starts over.
            events.Subscribe<UnitRevived>(e => _focus.Remove(e.Unit));
        }

        /// <summary>A unit's Focus right now (0 for anyone without it).</summary>
        public int FocusOf(ICombatUnit unit)
        {
            return unit != null && _focus.TryGetValue(unit, out int focus) ? focus : 0;
        }

        /// <summary>Whether <paramref name="unit"/> builds Focus at all.</summary>
        public static bool Builds(ICombatUnit unit)
        {
            return RuleOf(unit) != null;
        }

        /// <summary>Takes all of <paramref name="unit"/>'s Focus for a MultiCast and returns it. The turn
        /// it is spent on earns none back.</summary>
        public int Spend(ICombatUnit unit)
        {
            int focus = FocusOf(unit);
            if (unit == null)
            {
                return 0;
            }
            _focus[unit] = 0;
            _spent.Add(unit);
            if (focus > 0)
            {
                Changed?.Invoke(unit, 0);
            }
            return focus;
        }

        private static FocusRule RuleOf(ICombatUnit unit)
        {
            var rule = (unit as IHasFocus)?.Focus;
            return rule != null && rule.IsActive ? rule : null;
        }

        private void OnAbilityUsed(AbilityUsed used)
        {
            if (used.Caster == null || used.Ability == null || RuleOf(used.Caster) == null)
            {
                return;
            }
            string key = used.Ability.Key ?? "";
            if (key.StartsWith(SummonOps.CastKeyPrefix) || key.StartsWith(UltraOps.CastKeyPrefix))
            {
                return;
            }
            _cast.Add(used.Caster);
        }

        private void OnTurnEnded(TurnEnded e)
        {
            var rule = RuleOf(e.Unit);
            if (rule == null)
            {
                return;
            }
            bool cast = _cast.Remove(e.Unit) && !_spent.Remove(e.Unit);
            int before = FocusOf(e.Unit);
            int after = FocusOps.Next(before, cast, rule);
            _focus[e.Unit] = after;
            if (after != before)
            {
                Changed?.Invoke(e.Unit, after);
            }
        }
    }
}
