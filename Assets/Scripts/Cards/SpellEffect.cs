using System;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

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
        /// How <see cref="Power"/> is read: a base the caster's stat is added to, a flat number, or a
        /// percentage of the affected unit's max health. Damage, Heal and HealthCost honour it; buff
        /// and debuff magnitudes are stat deltas and ignore it. Defaults to
        /// <see cref="Cards.PowerMode.BasePower"/>, so existing assets are unchanged.
        /// </summary>
        public PowerMode PowerMode = PowerMode.BasePower;

        /// <summary>
        /// Which caster stat scales this effect, or <see cref="StatType.None"/> for flat power.
        /// Damage and healing add the stat in full; buffs and debuffs add a fraction of it
        /// (<see cref="SpellScaling.BuffScalingDivisor"/>), because their Power is a stat delta
        /// rather than a damage number.
        /// </summary>
        public StatType ScalingStat = StatType.None;

        public DamageType DamageType;
        public BuffType BuffType;
        public int Duration = 3;

        // Upgrade level at which this effect becomes active (0 = always). Lets a magic or
        // combo gate extra functionality behind upgrade levels - e.g. a combo that only
        // debuffs Speed once its upgrade reaches level 5.
        public int UnlockLevel = 0;
    }
}
