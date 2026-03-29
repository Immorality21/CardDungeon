using System;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class CardEffect
    {
        public CardEffectType EffectType;
        public int Power;
        public DamageType DamageType;
        public BuffType BuffType;
        public int Duration = 3;
    }
}
