using System;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class LevelConfiguration
    {
        public int Level;
        public int XpRequired;
        public int StrengthGain;
        public int EnduranceGain;
        public int HealthGain;
        public int AgilityGain;
    }
}
