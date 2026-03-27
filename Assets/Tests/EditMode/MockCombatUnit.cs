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

        public int GetEffectiveAttack()
        {
            return Stats.Attack;
        }

        public int GetEffectiveDefense()
        {
            return Stats.Defense;
        }

        public MockCombatUnit(string name, int attack, int defense, int health, int agility = 5, bool isHero = true)
        {
            DisplayName = name;
            Stats = new Stats(attack, defense, health, agility);
            IsHero = isHero;
        }
    }
}
