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
        int GetEffectiveAttack();
        int GetEffectiveDefense();
    }
}
