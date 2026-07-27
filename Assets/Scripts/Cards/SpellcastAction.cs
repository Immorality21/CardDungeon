using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class SpellcastAction
    {
        public MagicSO Magic;
        public ICombatUnit Caster;
        public List<ICombatUnit> Targets;
    }
}
