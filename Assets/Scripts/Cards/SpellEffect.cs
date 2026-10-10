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

        /// <summary>
        /// Percent chance a <see cref="SpellEffectType.Debuff"/> takes hold, rolled once per target
        /// through <see cref="AfflictionRules"/> - every debuff, a stat cut and a status alike, so a
        /// Silence, a Blind or a Poison is a likely outcome rather than a certainty. 100 (the
        /// default, and what every asset authored before the field reads as) always lands. Ignored
        /// by every other effect type: a buff on your own side is not a gamble.
        /// </summary>
        [UnityEngine.Range(1, 100)]
        public int ApplyChance = AfflictionRules.Certain;

        /// <summary>
        /// A kind of unit this effect is meant for (Undead, say). A Damage effect hits such a unit
        /// <see cref="BonusPercent"/> harder; an Execute only destroys units of it. None for every
        /// effect authored before the field, which therefore reads as no bonus.
        /// </summary>
        public UnitTraits BonusVsTrait = UnitTraits.None;

        /// <summary>Extra damage against <see cref="BonusVsTrait"/>, in percent: 100 doubles the raw hit.</summary>
        public int BonusPercent;

        /// <summary>
        /// A field-for-field copy, for the places that adjust an authored effect before it resolves
        /// (an upgrade bonus, a summon's grid upgrades). Copying by hand dropped a field each time
        /// one was added - <see cref="ScalingStat"/> once, which silently made upgraded spells weaker.
        /// </summary>
        public SpellEffect Clone()
        {
            return (SpellEffect)MemberwiseClone();
        }

        /// <summary>
        /// A physical hit: damage scaled off a physical stat (Strength, Agility), which Endurance
        /// defends and Luck can dodge. Magic (Intelligence/Spirit), flat and percentage damage are
        /// not. See <see cref="DefenseRules"/>.
        /// </summary>
        public bool IsPhysicalHit =>
            EffectType == SpellEffectType.Damage
            && PowerMode == PowerMode.BasePower
            && StatCatalog.CanScalePower(ScalingStat)
            && !DefenseRules.IsMagic(ScalingStat);
    }
}
