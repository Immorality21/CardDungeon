using System.Collections.Generic;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
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
        /// One stat, with gear and anything else the implementer layers on folded in. Replaces the
        /// old per-stat getters: a new <see cref="StatType"/> needs no interface change.
        /// </summary>
        int GetEffectiveStat(StatType stat);

        /// <summary>
        /// Which stat this unit's basic Attack scales off. Exposed on the interface because combat
        /// needs it beyond the raw number: a buff to attack power has to target *this* stat, or a
        /// Strength buff would boost an Agility-swinging Scout while Haste would not.
        /// </summary>
        StatType AttackStat { get; }

        /// <summary>
        /// Damage the basic Attack command swings with: <see cref="GetEffectiveStat"/> of
        /// <see cref="AttackStat"/>. Derived, not a stat.
        /// </summary>
        int GetEffectiveAttackPower();
    }
}
