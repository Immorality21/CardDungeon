using System;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class SpellEffect
    {
        public SpellEffectType EffectType;

        /// <summary>
        /// Base power before the caster's stat is added. <see cref="ScalingStat"/> decides which stat
        /// that is, so the same magic hits harder in the right hands.
        /// </summary>
        public int Power;

        /// <summary>
        /// Which caster stat scales this effect. Defaults to <see cref="SpellScalingStat.Strength"/>
        /// because that is what every effect used before the caster stats existed - magic authored
        /// then keeps its exact numbers until it is deliberately re-pointed at Intelligence or Spirit.
        /// </summary>
        public SpellScalingStat ScalingStat = SpellScalingStat.Strength;
        public DamageType DamageType;
        public BuffType BuffType;
        public int Duration = 3;

        // Upgrade level at which this effect becomes active (0 = always). Lets a magic or
        // combo gate extra functionality behind upgrade levels — e.g. a combo that only
        // debuffs Speed once its upgrade reaches level 5.
        public int UnlockLevel = 0;
    }
}
