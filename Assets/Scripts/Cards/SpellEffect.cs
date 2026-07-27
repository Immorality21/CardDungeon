using System;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class SpellEffect
    {
        public SpellEffectType EffectType;
        public int Power;
        public DamageType DamageType;
        public BuffType BuffType;
        public int Duration = 3;
    }
}
