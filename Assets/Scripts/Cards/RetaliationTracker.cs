using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Answers enemy blows off one fight's <see cref="CombatEvents"/>, two ways:
    /// <list type="bullet">
    /// <item>a readied counterstrike (<see cref="RetaliationTable"/>, Divine Intervention): when an enemy's
    /// blow lands on an armed unit - or is soaked by its barrier - the unit strikes the attacker once with
    /// its readied effect, through the same engine an ability uses;</item>
    /// <item>thorns (<see cref="ThornsTable"/>, the Mage's cloaks): every enemy blow that lands on a unit
    /// wearing them sends a share back at the attacker in the thorns' element. Resistances apply, defense
    /// does not - the share is of a hit that already went through defense.</item>
    /// </list>
    /// Both are reactions (<see cref="HealthOps.ReactionScope"/>), so neither can ever set off another one,
    /// and both are announced as a <see cref="ReactionResolved"/> for the log and the floating text.
    ///
    /// <para>Built per fight by its owner - <c>CombatManager.RunCombat</c> and the simulator's encounter loop.</para>
    /// </summary>
    public sealed class RetaliationTracker
    {
        private static readonly Color ThornsColor = new Color(1f, 0.75f, 0.45f);

        private readonly CombatEvents _events;
        private readonly EffectResolver _resolver;
        private readonly CombatBuffTracker _buffs;
        private readonly TriggeredEffect _label = new TriggeredEffect { Label = "Retribution" };
        private readonly TriggeredEffect _thornsLabel = new TriggeredEffect { Label = "Thorns" };

        public RetaliationTracker(CombatEvents events, EffectResolver resolver, CombatBuffTracker buffs)
        {
            _events = events;
            _resolver = resolver;
            _buffs = buffs;
            if (_events != null)
            {
                _events.Subscribe<HealthChange>(OnHealthChange, TriggerRegistry.Order);
            }
        }

        private void OnHealthChange(HealthChange change)
        {
            var bearer = change.Target;
            var attacker = change.Attacker;
            if (bearer == null || attacker == null || change.Source.Reaction || !bearer.IsAlive || !attacker.IsAlive)
            {
                return;
            }
            if (change.Landed <= 0 && change.Absorbed <= 0)
            {
                return;
            }
            if (change.Cause != HealthCause.Attack && change.Cause != HealthCause.Ability)
            {
                return;
            }
            if (attacker.IsHero == bearer.IsHero)
            {
                return;
            }
            Retaliate(bearer, attacker);
            ReturnThorns(bearer, attacker, change.Landed + change.Absorbed);
        }

        private void Retaliate(ICombatUnit bearer, ICombatUnit attacker)
        {
            if (!attacker.IsAlive || !_events.Retaliations.TryTake(bearer, out object armed))
            {
                return;
            }
            var strike = armed as SpellEffect;
            if (strike == null || _resolver == null)
            {
                return;
            }
            var targets = new List<ICombatUnit> { attacker };
            EffectResult result;
            using (HealthOps.ReactionScope())
            {
                result = _resolver.ExecuteEffects(new List<SpellEffect> { strike }, bearer, targets, _buffs);
            }
            _events.Publish(new ReactionResolved
            {
                Bearer = bearer,
                Trigger = _label,
                From = _label.Label,
                Targets = targets,
                Result = result
            });
        }

        private void ReturnThorns(ICombatUnit bearer, ICombatUnit attacker, int hit)
        {
            if (!attacker.IsAlive || !_events.Thorns.TryGet(bearer, out int percent, out DamageType element))
            {
                return;
            }
            int returned = ThornsTable.Returned(hit, percent);
            if (returned <= 0)
            {
                return;
            }
            float resistance = _buffs != null ? _buffs.GetResistanceBonus(attacker, element) : 0f;
            int damage = DamageCalculator.Calculate(returned, 0, element, attacker.Resistances, resistance);
            var source = new HealthSource(bearer, HealthCause.Ability, element);
            HealthChange struck;
            using (HealthOps.ReactionScope())
            {
                struck = HealthOps.Damage(attacker, damage, source, _events);
            }
            var result = new EffectResult();
            result.Entries.Add(new EffectEntry
            {
                Target = attacker,
                Text = damage < 0 ? $"+{struck.Healed}" : damage.ToString(),
                Color = ThornsColor,
                Impact = damage > 0 ? damage : 0,
                Landed = struck.Landed,
                Effectiveness = DamageCalculator.Classify(element, attacker.Resistances, resistance)
            });
            _events.Publish(new ReactionResolved
            {
                Bearer = bearer,
                Trigger = _thornsLabel,
                From = _thornsLabel.Label,
                Targets = new List<ICombatUnit> { attacker },
                Result = result
            });
        }
    }
}
