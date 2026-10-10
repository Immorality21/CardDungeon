using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Counterstrikes readied in one fight (the Paladin's Divine Intervention): a unit armed here strikes
    /// back at the first enemy whose blow lands on it, once, and the readiness ends when the unit's own
    /// next turn begins. Lives on <see cref="CombatEvents"/> so the live fight and the simulator share it;
    /// the strike itself is resolved by <c>Cards.RetaliationTracker</c>, which has the effect engine.
    /// The strike is stored as data (an effect owned by this table) rather than a number, so its element
    /// and scaling are whatever the ability authored.
    /// </summary>
    public sealed class RetaliationTable
    {
        private readonly Dictionary<ICombatUnit, object> _armed = new Dictionary<ICombatUnit, object>();

        /// <summary>Readies <paramref name="unit"/> to strike back with <paramref name="strike"/> (a
        /// <c>Cards.SpellEffect</c>). A second arming replaces the first.</summary>
        public void Arm(ICombatUnit unit, object strike)
        {
            if (unit != null && strike != null)
            {
                _armed[unit] = strike;
            }
        }

        public bool IsArmed(ICombatUnit unit)
        {
            return unit != null && _armed.ContainsKey(unit);
        }

        /// <summary>Takes <paramref name="unit"/>'s readied strike, if any: it fires once.</summary>
        public bool TryTake(ICombatUnit unit, out object strike)
        {
            strike = null;
            if (unit == null || !_armed.TryGetValue(unit, out strike))
            {
                return false;
            }
            _armed.Remove(unit);
            return true;
        }

        /// <summary>The unit's turn has begun: whatever it readied and did not use is gone.</summary>
        public void TurnStarted(ICombatUnit unit)
        {
            if (unit != null)
            {
                _armed.Remove(unit);
            }
        }
    }
}
