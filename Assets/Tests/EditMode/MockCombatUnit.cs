using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Tests.EditMode
{
    public class MockCombatUnit : ICombatUnit
    {
        public string DisplayName { get; set; }
        public Sprite Icon => null;
        public Stats Stats { get; set; }
        public bool IsAlive => Stats.Health > 0;
        public bool IsHero { get; set; }
        public Transform Transform => null;
        public List<Resistance> Resistances { get; set; } = new List<Resistance>();

        public int GetEffectiveAttack()
        {
            return Stats.Attack;
        }

        public int GetEffectiveDefense()
        {
            return Stats.Defense;
        }

        // When set, models item/level agility bonuses layered on top of the raw stat.
        public int? EffectiveAgilityOverride { get; set; }

        public int GetEffectiveAgility()
        {
            return EffectiveAgilityOverride ?? Stats.Agility;
        }

        public MockCombatUnit(string name, int attack, int defense, int health, int agility = 5, bool isHero = true)
        {
            DisplayName = name;
            Stats = new Stats(attack, defense, health, agility);
            IsHero = isHero;
        }
    }
}
