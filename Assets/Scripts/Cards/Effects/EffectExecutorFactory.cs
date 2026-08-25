using System.Collections.Generic;

namespace Assets.Scripts.Cards.Effects
{
    public class EffectExecutorFactory
    {
        private readonly Dictionary<SpellEffectType, IEffectExecutor> _executors;

        public EffectExecutorFactory()
        {
            _executors = new Dictionary<SpellEffectType, IEffectExecutor>
            {
                { SpellEffectType.Damage, new DamageEffectExecutor() },
                { SpellEffectType.Heal, new HealEffectExecutor() },
                { SpellEffectType.Buff, new BuffEffectExecutor() },
                { SpellEffectType.Debuff, new DebuffEffectExecutor() },
                { SpellEffectType.HealthCost, new HealthCostEffectExecutor() }
            };
        }

        public IEffectExecutor GetExecutor(SpellEffectType type)
        {
            return _executors[type];
        }
    }
}
