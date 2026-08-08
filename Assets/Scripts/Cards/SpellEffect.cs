using System;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class SpellEffect
    {
        public SpellEffectType EffectType;
        public int Power;
        public DamageType DamageType;
        public BuffType BuffType;
        public int Duration = 3;

        // Upgrade level at which this effect becomes active (0 = always). Lets a magic or
        // combo gate extra functionality behind upgrade levels — e.g. a combo that only
        // debuffs Speed once its upgrade reaches level 5.
        public int UnlockLevel = 0;
    }
}
