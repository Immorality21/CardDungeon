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

        public Stats(int attack, int defense, int health)
        {
            Attack = attack;
            Defense = defense;
            Health = health;
            MaxHealth = health;
        }
    }
}
