using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Effects
{
    public class EffectExecutorFactory
    {
        private readonly Dictionary<SpellEffectType, IEffectExecutor> _executors;

        /// <param name="clock">The fight's turn clock, read when a <see cref="SpellEffectType.TurnDelay"/>
        /// lands. Null (a room event, a resolver with no fight) leaves that effect inert.</param>
        /// <param name="charges">Where the fight keeps ability charges, read when a
        /// <see cref="SpellEffectType.RestoreCharge"/> lands. Null leaves it inert the same way.</param>
        public EffectExecutorFactory(
            Func<TurnManager> clock = null,
            Func<IChargeBank> charges = null)
        {
            _executors = new Dictionary<SpellEffectType, IEffectExecutor>
            {
                { SpellEffectType.Damage, new DamageEffectExecutor() },
                { SpellEffectType.Heal, new HealEffectExecutor() },
                { SpellEffectType.Buff, new BuffEffectExecutor() },
                { SpellEffectType.Debuff, new DebuffEffectExecutor() },
                { SpellEffectType.HealthCost, new HealthCostEffectExecutor() },
                { SpellEffectType.TurnDelay, new TurnDelayEffectExecutor(clock) },
                { SpellEffectType.Drain, new DrainEffectExecutor() },
                { SpellEffectType.RestoreCharge, new RestoreChargeEffectExecutor(charges) }
            };
        }

        public IEffectExecutor GetExecutor(SpellEffectType type)
        {
            return _executors[type];
        }
    }
}
