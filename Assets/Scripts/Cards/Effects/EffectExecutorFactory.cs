using System.Collections.Generic;

namespace Assets.Scripts.Cards.Effects
{
    public class EffectExecutorFactory
    {
        private readonly Dictionary<CardEffectType, IEffectExecutor> _executors;

        public EffectExecutorFactory()
        {
            _executors = new Dictionary<CardEffectType, IEffectExecutor>
            {
                { CardEffectType.Damage, new DamageEffectExecutor() },
                { CardEffectType.Heal, new HealEffectExecutor() },
                { CardEffectType.Buff, new BuffEffectExecutor() },
                { CardEffectType.Debuff, new DebuffEffectExecutor() }
            };
        }

        public IEffectExecutor GetExecutor(CardEffectType type)
        {
            return _executors[type];
        }
    }
}
