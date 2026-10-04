using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The live fight's <see cref="IChargeBank"/>: a hero's charges are their slots in the run's
    /// <see cref="EquippedMagicState"/>. Anything that is not a hero carries no slots.
    /// </summary>
    public class EquippedChargeBank : IChargeBank
    {
        private readonly EquippedMagicState _state;

        public EquippedChargeBank(EquippedMagicState state)
        {
            _state = state;
        }

        public bool Carries(ICombatUnit unit)
        {
            return _state != null && unit is Hero;
        }

        public ChargeRestore Restore(ICombatUnit unit, int slotIndex, int amount, int excludeSlot)
        {
            var hero = unit as Hero;
            if (_state == null || hero == null)
            {
                return ChargeRestore.None;
            }
            return _state.RestoreCharges(hero.HeroKey, slotIndex, amount, excludeSlot);
        }
    }
}
