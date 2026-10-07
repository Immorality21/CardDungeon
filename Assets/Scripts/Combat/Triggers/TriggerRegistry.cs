using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using UnityEngine;

namespace Assets.Scripts.Combat.Triggers
{
    /// <summary>
    /// Fires authored reactions (<see cref="TriggeredEffect"/>) off one fight's
    /// <see cref="CombatEvents"/>. Built per fight by its owner - <c>CombatManager.RunCombat</c> and the
    /// simulator's encounter loop - after the game's own bookkeeping has subscribed, so a reaction
    /// always sees an event after the fight has (a death is already queued for its rewards when an
    /// on-kill reaction hears of it).
    ///
    /// <para>It caches nothing about who carries what: when an event happens it asks the units involved
    /// for their triggers (<see cref="ITriggerSource"/>), so a summon arriving mid-fight or a hero's gear
    /// needs no registration.</para>
    ///
    /// <para><b>A reaction's hits do not cause hit reactions.</b> Everything a reaction does is marked
    /// (<see cref="HealthOps.ReactionScope"/>), and <see cref="TriggerKind.OnDealDamage"/> /
    /// <see cref="TriggerKind.OnTakeDamage"/> ignore marked changes - two "when I deal damage, deal
    /// damage" items would otherwise feed each other until the stream's chain cap. Kills and deaths
    /// still trigger from a reaction (an explosion that kills can feed a soul-stealer): deaths run out.</para>
    /// </summary>
    public sealed class TriggerRegistry
    {
        /// <summary>Subscription order for reactions: after the fight's own bookkeeping (order 0).</summary>
        public const int Order = 100;

        private readonly CombatEvents _events;
        private readonly EffectResolver _resolver;
        private readonly CombatBuffTracker _buffTracker;
        private readonly Func<IEnumerable<ICombatUnit>> _livingUnits;
        private readonly Func<float> _roll;

        private int _turn;
        private readonly Dictionary<(ICombatUnit, TriggeredEffect), int> _lastFiredTurn = new Dictionary<(ICombatUnit, TriggeredEffect), int>();
        private readonly HashSet<(ICombatUnit, TriggeredEffect)> _firedThisCombat = new HashSet<(ICombatUnit, TriggeredEffect)>();

        /// <param name="livingUnits">Every unit standing in the fight, both sides - what "all allies",
        /// "all enemies" and "an ally fell" are measured against. Read fresh at every event.</param>
        /// <param name="roll">A 0..1 roll for <see cref="TriggeredEffect.Chance"/>; the game's random
        /// stream when null, so the simulator's seeded runs stay reproducible.</param>
        public TriggerRegistry(CombatEvents events, EffectResolver resolver, CombatBuffTracker buffTracker,
            Func<IEnumerable<ICombatUnit>> livingUnits, Func<float> roll = null)
        {
            _events = events;
            _resolver = resolver;
            _buffTracker = buffTracker;
            _livingUnits = livingUnits ?? (() => Enumerable.Empty<ICombatUnit>());
            _roll = roll ?? (() => UnityEngine.Random.Range(0f, 1f));

            if (_events == null)
            {
                return;
            }
            _events.Subscribe<CombatStarted>(OnCombatStarted, Order);
            _events.Subscribe<TurnStarted>(OnTurnStarted, Order);
            _events.Subscribe<TurnEnded>(e => Fire(e.Unit, TriggerKind.OnTurnEnd, null), Order);
            _events.Subscribe<HealthChange>(OnHealthChange, Order);
            _events.Subscribe<UnitDefeated>(OnUnitDefeated, Order);
            _events.Subscribe<AbilityUsed>(e => Fire(e.Caster, TriggerKind.OnAbilityUsed, null), Order);
        }

        private void OnCombatStarted(CombatStarted e)
        {
            foreach (var unit in e.Heroes.Concat(e.Enemies).ToList())
            {
                Fire(unit, TriggerKind.OnCombatStart, null);
            }
        }

        private void OnTurnStarted(TurnStarted e)
        {
            _turn++;
            Fire(e.Unit, TriggerKind.OnTurnStart, null);
        }

        private void OnHealthChange(HealthChange change)
        {
            if (change.Landed <= 0 || change.Source.Reaction || !IsAHit(change.Cause))
            {
                return;
            }
            var attacker = change.Attacker;
            if (attacker != null && !ReferenceEquals(attacker, change.Target))
            {
                Fire(attacker, TriggerKind.OnDealDamage, change.Target, change.Source.DamageType);
            }
            if (change.Target.IsAlive && !ReferenceEquals(attacker, change.Target))
            {
                Fire(change.Target, TriggerKind.OnTakeDamage, attacker, change.Source.DamageType);
            }
        }

        private void OnUnitDefeated(UnitDefeated death)
        {
            var victim = death.Victim;
            // A kill is a foe felled: a Sacrifice is credited to the Cultist, and must not pay an
            // on-kill item for giving up an ally.
            if (death.Killer != null && death.Killer.IsHero != victim.IsHero)
            {
                Fire(death.Killer, TriggerKind.OnKill, victim, death.DamageType);
            }
            Fire(victim, TriggerKind.OnDefeated, death.Killer, death.DamageType);
            foreach (var ally in _livingUnits().Where(u => u != null && u.IsHero == victim.IsHero && !ReferenceEquals(u, victim)).ToList())
            {
                Fire(ally, TriggerKind.OnAllyDefeated, victim);
            }
        }

        /// <summary>Damage that counts as a hit: blows, abilities and ticks - not a price paid, a
        /// Sacrifice, a bar resized or a fallen summon clamped.</summary>
        private static bool IsAHit(HealthCause cause)
        {
            return cause == HealthCause.Attack || cause == HealthCause.Ability || cause == HealthCause.OverTime;
        }

        /// <summary>Fires every trigger <paramref name="bearer"/> carries for <paramref name="kind"/>.</summary>
        private void Fire(ICombatUnit bearer, TriggerKind kind, ICombatUnit other, DamageType? damageType = null)
        {
            if (!(bearer is ITriggerSource source))
            {
                return;
            }
            // Only a fallen bearer's own death may fire from beyond it.
            if (!bearer.IsAlive && kind != TriggerKind.OnDefeated)
            {
                return;
            }

            foreach (var carried in source.GetTriggers())
            {
                var trigger = carried.Trigger;
                if (trigger == null || trigger.When != kind || trigger.Effects == null || trigger.Effects.Count == 0)
                {
                    continue;
                }
                if (trigger.FilterDamageType && (!damageType.HasValue || damageType.Value != trigger.DamageType))
                {
                    continue;
                }
                if (!WithinLimit(bearer, trigger))
                {
                    continue;
                }
                if (trigger.Chance < 1f && _roll() >= trigger.Chance)
                {
                    continue;
                }

                var targets = TargetsFor(trigger.Target, bearer, other);
                if (targets.Count == 0)
                {
                    continue;
                }

                MarkFired(bearer, trigger);
                EffectResult result;
                using (HealthOps.ReactionScope())
                {
                    result = _resolver.ExecuteEffects(trigger.Effects, bearer, targets, _buffTracker);
                }
                _events.Publish(new ReactionResolved
                {
                    Bearer = bearer,
                    Trigger = trigger,
                    From = carried.From,
                    Targets = targets,
                    Result = result
                });
            }
        }

        private bool WithinLimit(ICombatUnit bearer, TriggeredEffect trigger)
        {
            switch (trigger.Limit)
            {
                case TriggerLimit.OncePerCombat:
                    return !_firedThisCombat.Contains((bearer, trigger));
                case TriggerLimit.OncePerTurn:
                    return !_lastFiredTurn.TryGetValue((bearer, trigger), out int turn) || turn != _turn;
                default:
                    return true;
            }
        }

        private void MarkFired(ICombatUnit bearer, TriggeredEffect trigger)
        {
            _firedThisCombat.Add((bearer, trigger));
            _lastFiredTurn[(bearer, trigger)] = _turn;
        }

        private List<ICombatUnit> TargetsFor(TriggerTarget target, ICombatUnit bearer, ICombatUnit other)
        {
            switch (target)
            {
                case TriggerTarget.Bearer:
                    return new List<ICombatUnit> { bearer };
                case TriggerTarget.Other:
                    return other != null ? new List<ICombatUnit> { other } : new List<ICombatUnit>();
                case TriggerTarget.AllAllies:
                    return Living().Where(u => u.IsHero == bearer.IsHero).ToList();
                case TriggerTarget.AllEnemies:
                    return Living().Where(u => u.IsHero != bearer.IsHero).ToList();
                case TriggerTarget.RandomEnemy:
                {
                    var enemies = Living().Where(u => u.IsHero != bearer.IsHero).ToList();
                    if (enemies.Count == 0)
                    {
                        return enemies;
                    }
                    int pick = Mathf.Min(enemies.Count - 1, Mathf.FloorToInt(_roll() * enemies.Count));
                    return new List<ICombatUnit> { enemies[pick] };
                }
                default:
                    return new List<ICombatUnit>();
            }
        }

        private List<ICombatUnit> Living()
        {
            return _livingUnits().Where(u => u != null && u.IsAlive).ToList();
        }
    }
}
