using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class CardAction
    {
        public CardSO Card;
        public ICombatUnit Caster;
        public List<ICombatUnit> Targets;
    }
}
