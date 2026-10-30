using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Divine protection in one fight (the Paladin's Divine Protection and Aegis): the next damaging hit
    /// on a unit - a blow or an ability, of any element - is negated outright, until it is spent or the
    /// bearer has had a few of its own turns. Lives on <see cref="CombatEvents"/>, so the live fight and
    /// the simulator share it, and is read by the one place health falls - <see cref="HealthOps.Damage"/>.
    /// A tick is not a hit and a price is not damage, so neither spends it (<see cref="Negates"/>).
    ///
    /// <para>Timed like a barrier: a second protection on the same unit does not stack - the more hits
    /// and the longer time win - and one put up during the bearer's own turn does not lose a turn at
    /// that turn's end.</para>
    /// </summary>
    public sealed class ProtectionTable
    {
        private class Protection
        {
            public int Hits;
            public int TurnsLeft;
            public bool SkipNextEnd;
        }

        private readonly Dictionary<ICombatUnit, Protection> _protections = new Dictionary<ICombatUnit, Protection>();
        private ICombatUnit _acting;

        /// <summary>Whether a hit of this cause is negated: blows and abilities are; ticks, prices and the rest are not.</summary>
        public static bool Negates(HealthCause cause)
        {
            return cause == HealthCause.Attack || cause == HealthCause.Ability;
        }

        /// <summary>Protects <paramref name="unit"/> from its next <paramref name="hits"/> hits for <paramref name="turns"/> of its turns.</summary>
        public void Add(ICombatUnit unit, int hits, int turns)
        {
            if (unit == null || hits <= 0 || turns <= 0)
            {
                return;
            }
            if (!_protections.TryGetValue(unit, out var protection))
            {
                protection = new Protection();
                _protections[unit] = protection;
            }
            protection.Hits = protection.Hits > hits ? protection.Hits : hits;
            protection.TurnsLeft = protection.TurnsLeft > turns ? protection.TurnsLeft : turns;
            protection.SkipNextEnd |= ReferenceEquals(unit, _acting);
        }

        /// <summary>The hits a unit's protection will still negate, 0 for none.</summary>
        public int HitsOf(ICombatUnit unit)
        {
            return unit != null && _protections.TryGetValue(unit, out var protection) ? protection.Hits : 0;
        }

        /// <summary>Spends one hit of the unit's protection: true when it had one, and the hit is negated.</summary>
        public bool Consume(ICombatUnit unit)
        {
            if (unit == null || !_protections.TryGetValue(unit, out var protection))
            {
                return false;
            }
            protection.Hits--;
            if (protection.Hits <= 0)
            {
                _protections.Remove(unit);
            }
            return true;
        }

        /// <summary>Takes a unit's protection away (a revived hero starts clean).</summary>
        public void Clear(ICombatUnit unit)
        {
            if (unit != null)
            {
                _protections.Remove(unit);
            }
        }

        public void TurnStarted(ICombatUnit unit)
        {
            _acting = unit;
        }

        /// <summary>A unit's turn closed: its protection loses a turn, and goes when it has none left.</summary>
        public void TurnEnded(ICombatUnit unit)
        {
            if (ReferenceEquals(unit, _acting))
            {
                _acting = null;
            }
            if (unit == null || !_protections.TryGetValue(unit, out var protection))
            {
                return;
            }
            if (protection.SkipNextEnd)
            {
                protection.SkipNextEnd = false;
                return;
            }
            protection.TurnsLeft--;
            if (protection.TurnsLeft <= 0)
            {
                _protections.Remove(unit);
            }
        }
    }
}
