using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Effects
{
    public static class BuffTypeMapper
    {
        public static StatType? ToStatType(BuffType buffType)
        {
            switch (buffType)
            {
                case BuffType.Attack:
                    return StatType.Attack;
                case BuffType.Defense:
                    return StatType.Defense;
                case BuffType.Agility:
                    return StatType.Agility;
                default:
                    return null;
            }
        }
    }
}
