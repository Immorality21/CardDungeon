using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Where a unit's ability charges live, as a <see cref="SpellEffectType.RestoreCharge"/> sees
    /// them. The live fight answers from the run's <see cref="EquippedMagicState"/>
    /// (<see cref="EquippedChargeBank"/>), the balance model from its simulated slots. A unit that
    /// carries no abilities (an enemy, a summon) restores nothing.
    /// </summary>
    public interface IChargeBank
    {
        /// <summary>Whether <paramref name="unit"/> carries ability slots at all. A restore passes
        /// over one that does not (a summon, an enemy) without a word.</summary>
        bool Carries(ICombatUnit unit);

        /// <summary>
        /// Gives <paramref name="amount"/> charges back to <paramref name="unit"/>'s slot
        /// <paramref name="slotIndex"/> - or, for <see cref="SpellcastAction.AnyChargeSlot"/>, its
        /// most-spent slot - capped at the slot's maximum, never touching
        /// <paramref name="excludeSlot"/>. Returns the restored slot's magic and the charges
        /// actually added (0 when there was nothing to restore).
        /// </summary>
        ChargeRestore Restore(ICombatUnit unit, int slotIndex, int amount, int excludeSlot);
    }

    /// <summary>What one restore did: which ability got charges back, and how many.</summary>
    public struct ChargeRestore
    {
        public MagicSO Magic;
        public int Added;

        public static ChargeRestore None => new ChargeRestore();
    }
}
