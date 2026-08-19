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

        int GetEffectiveAttack();
        int GetEffectiveDefense();
        int GetEffectiveAgility();
    }
}
