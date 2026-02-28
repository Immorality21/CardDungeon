using System;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class LevelConfiguration
    {
        public int Level;
        public int XpRequired;
        public int AttackGain;
        public int DefenseGain;
        public int HealthGain;
    }
}
