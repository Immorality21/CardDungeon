using System.Collections.Generic;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    public interface ICombatUnit
    {
        string DisplayName { get; }
        Sprite Icon { get; }
        Stats Stats { get; }
        bool IsAlive { get; }
        bool IsHero { get; }
        Transform Transform { get; }
        List<Resistance> Resistances { get; }

        /// <summary>
        /// Damage type this unit's physical attacks deal. <see cref="DamageType.Normal"/> bypasses the
        /// elemental layer entirely, so it stays the default for heroes and for any enemy that has not
        /// opted into an element.
        /// </summary>
        DamageType AttackDamageType { get; }

        /// <summary>
        /// Damage the basic Attack command swings with. Derived, not a stat: each hero names the
        /// attribute it reads through <c>HeroSO.AttackStat</c>, so a Scout can fight off Agility and
        /// a caster off Intelligence while a Warrior fights off Strength. Enemies use Strength.
        /// </summary>
        int GetEffectiveAttackPower();

        int GetEffectiveStrength();
        int GetEffectiveEndurance();
        int GetEffectiveAgility();

        /// <summary>
        /// Caster and luck stats, gear folded in the same way as the three above. Spell power reads
        /// Intelligence or Spirit depending on the effect's <c>ScalingStat</c>; Luck drives crit.
        /// </summary>
        int GetEffectiveIntelligence();
        int GetEffectiveSpirit();
        int GetEffectiveLuck();
    }
}
