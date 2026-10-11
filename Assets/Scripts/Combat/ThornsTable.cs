using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Damage returned in one fight: a unit wearing thorns sends a share of every enemy blow that lands
    /// on it back at the attacker, in the thorns' element, for a few of its own turns (the Mage's cloaks,
    /// owner 2026-10-10: "also does % damage return upon hit in that element"). Lives on
    /// <see cref="CombatEvents"/> so the live fight and the simulator share it; the strike back is
    /// resolved by <c>Cards.RetaliationTracker</c>, which already answers blows for Divine Intervention.
    ///
    /// <para>Timed like a barrier: a second set on the same unit does not stack - the higher share and the
    /// longer time win - and one put on during the bearer's own turn does not lose a turn at its end, so
    /// a 3-turn cloak and its 3-turn thorns run out together.</para>
    /// </summary>
    public sealed class ThornsTable
    {
        private class Thorns
        {
            public int Percent;
            public DamageType Element;
            public int TurnsLeft;
            public bool SkipNextEnd;
        }

        private readonly Dictionary<ICombatUnit, Thorns> _thorns = new Dictionary<ICombatUnit, Thorns>();
        private ICombatUnit _acting;

        /// <summary>Puts thorns on <paramref name="unit"/>: <paramref name="percent"/>% of each blow returned
        /// as <paramref name="element"/> for <paramref name="turns"/> of its turns.</summary>
        public void Add(ICombatUnit unit, int percent, DamageType element, int turns)
        {
            if (unit == null || percent <= 0 || turns <= 0)
            {
                return;
            }
            if (!_thorns.TryGetValue(unit, out var thorns))
            {
                thorns = new Thorns();
                _thorns[unit] = thorns;
            }
            if (percent >= thorns.Percent)
            {
                thorns.Percent = percent;
                thorns.Element = element;
            }
            thorns.TurnsLeft = thorns.TurnsLeft > turns ? thorns.TurnsLeft : turns;
            thorns.SkipNextEnd |= ReferenceEquals(unit, _acting);
        }

        /// <summary>The share a unit returns and in what element; false when it wears none.</summary>
        public bool TryGet(ICombatUnit unit, out int percent, out DamageType element)
        {
            percent = 0;
            element = DamageType.Normal;
            if (unit == null || !_thorns.TryGetValue(unit, out var thorns))
            {
                return false;
            }
            percent = thorns.Percent;
            element = thorns.Element;
            return true;
        }

        /// <summary>What a blow of <paramref name="landed"/> sends back at <paramref name="percent"/>%:
        /// rounded, and at least 1 whenever anything landed.</summary>
        public static int Returned(int landed, int percent)
        {
            if (landed <= 0 || percent <= 0)
            {
                return 0;
            }
            int amount = (int)System.Math.Round(landed * percent / 100.0, System.MidpointRounding.AwayFromZero);
            return amount < 1 ? 1 : amount;
        }

        /// <summary>Takes a unit's thorns away (a revived hero starts clean).</summary>
        public void Clear(ICombatUnit unit)
        {
            if (unit != null)
            {
                _thorns.Remove(unit);
            }
        }

        public void TurnStarted(ICombatUnit unit)
        {
            _acting = unit;
        }

        /// <summary>A unit's turn closed: its thorns lose a turn, and go when they have none left.</summary>
        public void TurnEnded(ICombatUnit unit)
        {
            if (ReferenceEquals(unit, _acting))
            {
                _acting = null;
            }
            if (unit == null || !_thorns.TryGetValue(unit, out var thorns))
            {
                return;
            }
            if (thorns.SkipNextEnd)
            {
                thorns.SkipNextEnd = false;
                return;
            }
            thorns.TurnsLeft--;
            if (thorns.TurnsLeft <= 0)
            {
                _thorns.Remove(unit);
            }
        }
    }
}
