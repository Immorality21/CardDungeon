using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class SpellcastAction
    {
        /// <summary><see cref="ChargeSlot"/> when nobody picked: a restore lands on the most-spent slot.</summary>
        public const int AnyChargeSlot = -1;

        public MagicSO Magic;
        public ICombatUnit Caster;
        public List<ICombatUnit> Targets;

        /// <summary>
        /// Which of the target's ability slots a <see cref="SpellEffectType.RestoreCharge"/> refills -
        /// the player's pick from the Life Tap picker - or <see cref="AnyChargeSlot"/>.
        /// </summary>
        public int ChargeSlot = AnyChargeSlot;

        /// <summary>
        /// The caster's slot this cast comes out of, or -1 (an enemy, a summon, a combo). A restore
        /// never refills it: the charge is spent after the effects resolve, so a Life Tap that
        /// topped itself up would cost nothing.
        /// </summary>
        public int CastSlot = -1;
    }
}
