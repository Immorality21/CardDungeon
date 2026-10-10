using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Fires readied counterstrikes (<see cref="RetaliationTable"/>) off one fight's <see cref="CombatEvents"/>:
    /// when an enemy's blow lands on an armed unit - or is soaked by its barrier - the unit strikes the
    /// attacker once with its readied effect, through the same engine an ability uses. The strike is a
    /// reaction (<see cref="HealthOps.ReactionScope"/>), so it can never set off another one, and it is
    /// announced as a <see cref="ReactionResolved"/> for the log and the floating text.
    ///
    /// <para>Built per fight by its owner - <c>CombatManager.RunCombat</c> and the simulator's encounter loop.</para>
    /// </summary>
    public sealed class RetaliationTracker
    {
        private readonly CombatEvents _events;
        private readonly EffectResolver _resolver;
        private readonly CombatBuffTracker _buffs;
        private readonly TriggeredEffect _label = new TriggeredEffect { Label = "Retribution" };

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
            if (attacker.IsHero == bearer.IsHero || !_events.Retaliations.TryTake(bearer, out object armed))
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
    }
}
