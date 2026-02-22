using System;

namespace Assets.Scripts.Items
{
    [Serializable]
    public class ItemBonus
    {
        public StatType StatType;
        public BonusType BonusType;
        public float Value;
    }
}
