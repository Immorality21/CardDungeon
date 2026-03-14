using System;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class Stats
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int MaxHealth;
        public int Agility;

        public Stats(int attack, int defense, int health, int agility = 5)
        {
            Attack = attack;
            Defense = defense;
            Health = health;
            MaxHealth = health;
            Agility = agility;
        }
    }
}
