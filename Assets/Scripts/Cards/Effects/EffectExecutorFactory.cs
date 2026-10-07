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
        /// <param name="events">The fight's event stream, raised by every effect that moves health.
        /// Null (a room event, a resolver with no fight) changes health without telling anyone.</param>
        /// <param name="killsOf">How many of a unit's kind the player has defeated (the Bestiary), read
        /// by <see cref="SpellEffectType.Disassemble"/>. Null reads every machine as never defeated.</param>
        /// <param name="roll">A uniform roll in [0, 1) for chance-based effects. Null uses Unity's.</param>
        public EffectExecutorFactory(
            Func<TurnManager> clock = null,
            Func<IChargeBank> charges = null,
            Func<CombatEvents> events = null,
            Func<Func<ICombatUnit, int>> killsOf = null,
            Func<Func<float>> roll = null)
        {
            _executors = new Dictionary<SpellEffectType, IEffectExecutor>
            {
                { SpellEffectType.Damage, new DamageEffectExecutor(events) },
                { SpellEffectType.Heal, new HealEffectExecutor(events) },
                { SpellEffectType.Buff, new BuffEffectExecutor() },
                { SpellEffectType.Debuff, new DebuffEffectExecutor() },
                { SpellEffectType.HealthCost, new HealthCostEffectExecutor(events) },
                { SpellEffectType.TurnDelay, new TurnDelayEffectExecutor(clock) },
                { SpellEffectType.Drain, new DrainEffectExecutor(events) },
                { SpellEffectType.RestoreCharge, new RestoreChargeEffectExecutor(charges) },
                { SpellEffectType.Disassemble, new DisassembleEffectExecutor(killsOf, roll, events) }
            };
        }

        public IEffectExecutor GetExecutor(SpellEffectType type)
        {
            return _executors[type];
        }
    }
}
