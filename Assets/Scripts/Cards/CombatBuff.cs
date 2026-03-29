using Assets.Scripts.Items;

namespace Assets.Scripts.Cards
{
    public class CombatBuff
    {
        public StatType Stat;
        public int Amount;
        public int TurnsRemaining;
        public bool IsStatusEffect;
        public BuffType BuffType;
    }
}
