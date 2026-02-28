using System;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class HeroSaveData
    {
        public string HeroKey;
        public int Level;
        public int CurrentXp;
        public int Attack;
        public int Defense;
        public int Health;
        public int MaxHealth;
    }
}
