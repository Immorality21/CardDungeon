using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// <b>The only code that writes <c>Stats.Health</c></b> (<c>CombatEventsTests.OnlyHealthOps_WritesHealth</c>
    /// guards it). Every hit, heal, tick, price and rescale goes through one
    /// of these four methods, which apply the clamp that kind of change has always had, report what
    /// actually moved as a <see cref="HealthChange"/>, and - given the fight's
    /// <see cref="CombatEvents"/> - raise it, plus a <see cref="UnitDefeated"/> the moment a bar crosses
    /// zero.
    ///
    /// <para>Before this there were thirty-odd direct writes, each with its own clamp, and a death was
    /// found afterwards by scanning for corpses, which is why nothing could say who struck the blow.
    /// Pure and static: the live fight, the simulator and room events all call it, and pass no
    /// events where there is no fight to tell.</para>
    /// </summary>
    public static class HealthOps
    {
        private static int _reactionDepth;

        /// <summary>
        /// Marks every health change made until the scope is disposed as a reaction's
        /// (<see cref="HealthSource.Reaction"/>). Opened by <c>TriggerRegistry</c> around a trigger's
        /// effects. Combat is single-threaded, so a counter is all the bookkeeping this needs; scopes nest.
        /// </summary>
        public static ReactionScopeHandle ReactionScope()
        {
            _reactionDepth++;
            return new ReactionScopeHandle(true);
        }

        /// <summary>Closes a <see cref="ReactionScope"/> when disposed.</summary>
        public readonly struct ReactionScopeHandle : System.IDisposable
        {
            private readonly bool _open;

            public ReactionScopeHandle(bool open)
            {
                _open = open;
            }

            public void Dispose()
            {
                if (_open && _reactionDepth > 0)
                {
                    _reactionDepth--;
                }
            }
        }

        /// <summary>
        /// Takes <paramref name="amount"/> off <paramref name="target"/>. Not clamped at zero - a bar may
        /// read negative after a big hit, which every <c>IsAlive</c> check already treats as down - but
        /// <see cref="HealthChange.Landed"/> only counts health that was there to take. A negative
        /// amount is an <b>absorbed</b> hit (resistance above 100%) and heals instead, clamped at the
        /// target's maximum.
        /// </summary>
        public static HealthChange Damage(ICombatUnit target, int amount, HealthSource source, CombatEvents events)
        {
            if (amount < 0)
            {
                return Heal(target, -amount, source, events);
            }
            var change = Begin(target, source);
            if (change == null)
            {
                return Empty(target, source);
            }
            change.Landed = Mathf.Min(amount, Mathf.Max(0, change.Before));
            target.Stats.Health = change.Before - amount;
            return Finish(change, events);
        }

        /// <summary>
        /// Restores up to <paramref name="amount"/>, never past the target's effective maximum and never
        /// below where it already was (a bar standing over its maximum is left alone, not cut down).
        /// Any scaling - a Fear level's healing cut, say - is the caller's to apply first.
        /// </summary>
        public static HealthChange Heal(ICombatUnit target, int amount, HealthSource source, CombatEvents events)
        {
            var change = Begin(target, source);
            if (change == null)
            {
                return Empty(target, source);
            }
            int room = Mathf.Max(0, target.GetEffectiveStat(StatType.MaxHealth) - change.Before);
            change.Healed = Mathf.Clamp(amount, 0, room);
            target.Stats.Health = change.Before + change.Healed;
            return Finish(change, events);
        }

        /// <summary>
        /// Charges <paramref name="amount"/> as a price, keeping a <b>1 HP floor</b>: an ability's
        /// HealthCost and a summon's blood price are gated on being affordable before they are ever
        /// submitted, and this floor is the safety net that keeps a bug from killing a unit through its
        /// own price.
        /// </summary>
        public static HealthChange Pay(ICombatUnit unit, int amount, HealthSource source, CombatEvents events)
        {
            var change = Begin(unit, source);
            if (change == null)
            {
                return Empty(unit, source);
            }
            int paid = Mathf.Min(Mathf.Max(0, amount), Mathf.Max(0, change.Before - 1));
            change.Landed = paid;
            unit.Stats.Health = change.Before - paid;
            return Finish(change, events);
        }

        /// <summary>
        /// Puts the bar at exactly <paramref name="value"/>: a Sacrifice (0), a form rescaling the bar,
        /// a fallen summon clamped to 0, a saved bar restored. Whatever moved is reported as damage or
        /// healing accordingly.
        /// </summary>
        public static HealthChange Set(ICombatUnit unit, int value, HealthSource source, CombatEvents events)
        {
            var change = Begin(unit, source);
            if (change == null)
            {
                return Empty(unit, source);
            }
            unit.Stats.Health = value;
            if (value < change.Before)
            {
                change.Landed = Mathf.Min(change.Before - value, Mathf.Max(0, change.Before));
            }
            else
            {
                change.Healed = value - change.Before;
            }
            return Finish(change, events);
        }

        private static HealthChange Begin(ICombatUnit target, HealthSource source)
        {
            if (target == null || target.Stats == null)
            {
                return null;
            }
            source.Reaction |= _reactionDepth > 0;
            return new HealthChange { Target = target, Source = source, Before = target.Stats.Health };
        }

        private static HealthChange Empty(ICombatUnit target, HealthSource source)
        {
            return new HealthChange { Target = target, Source = source };
        }

        private static HealthChange Finish(HealthChange change, CombatEvents events)
        {
            change.After = change.Target.Stats.Health;
            // Nothing moved - or a corpse was tidied to zero (a fallen summon clamped as it leaves),
            // which is bookkeeping, not a heal.
            if (events == null || change.After == change.Before || (change.Before <= 0 && change.After <= 0))
            {
                return change;
            }
            if (!change.Killed)
            {
                events.Publish(change);
                return change;
            }
            // The blow and the death go out back to back, so a reaction to the hit (whose own kills
            // queue behind it) can never be heard before the death that hit caused.
            events.Publish(change, new UnitDefeated
            {
                Victim = change.Target,
                Killer = change.Source.Source,
                Cause = change.Source.Cause,
                DamageType = change.Source.DamageType,
                ByReaction = change.Source.Reaction
            });
            return change;
        }
    }
}
