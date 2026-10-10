using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Barriers in one fight: a pool of points on a unit that soaks damage before its bar is touched,
    /// for a few of the unit's own turns (the Cleric's Sacred Shield, the Guardian Angel). Lives on
    /// <see cref="CombatEvents"/>, so the live fight and the simulator share it, and is read by the one
    /// place health falls - <see cref="HealthOps.Damage"/> - so every blow, cast and tick honours it
    /// without being told. A price paid in health is never soaked (<see cref="Soaks"/>).
    ///
    /// <para>A second barrier on the same unit does not stack: the larger pool and the longer time win,
    /// so recasting tops a barrier up rather than doubling it. Like a buff, one put up during the
    /// bearer's own turn does not lose a turn at that turn's end.</para>
    /// </summary>
    public sealed class BarrierTable
    {
        private class Barrier
        {
            public int Points;
            public int TurnsLeft;
            public bool SkipNextEnd;
        }

        private readonly Dictionary<ICombatUnit, Barrier> _barriers = new Dictionary<ICombatUnit, Barrier>();
        private ICombatUnit _acting;

        /// <summary>Whether damage of this cause is soaked: hits, casts and ticks are; prices and the rest are not.</summary>
        public static bool Soaks(HealthCause cause)
        {
            return cause == HealthCause.Attack || cause == HealthCause.Ability || cause == HealthCause.OverTime;
        }

        /// <summary>Puts up (or tops up) a barrier of <paramref name="points"/> for <paramref name="turns"/> of the unit's turns.</summary>
        public void Add(ICombatUnit unit, int points, int turns)
        {
            if (unit == null || points <= 0 || turns <= 0)
            {
                return;
            }
            if (!_barriers.TryGetValue(unit, out var barrier))
            {
                barrier = new Barrier();
                _barriers[unit] = barrier;
            }
            barrier.Points = barrier.Points > points ? barrier.Points : points;
            barrier.TurnsLeft = barrier.TurnsLeft > turns ? barrier.TurnsLeft : turns;
            barrier.SkipNextEnd |= ReferenceEquals(unit, _acting);
        }

        /// <summary>The points left on a unit's barrier, 0 for none.</summary>
        public int PointsOf(ICombatUnit unit)
        {
            return unit != null && _barriers.TryGetValue(unit, out var barrier) ? barrier.Points : 0;
        }

        /// <summary>Soaks up to <paramref name="damage"/> from the unit's barrier; returns what it soaked.</summary>
        public int Absorb(ICombatUnit unit, int damage)
        {
            if (unit == null || damage <= 0 || !_barriers.TryGetValue(unit, out var barrier))
            {
                return 0;
            }
            int soaked = damage < barrier.Points ? damage : barrier.Points;
            barrier.Points -= soaked;
            if (barrier.Points <= 0)
            {
                _barriers.Remove(unit);
            }
            return soaked;
        }

        public void TurnStarted(ICombatUnit unit)
        {
            _acting = unit;
        }

        /// <summary>A unit's turn closed: its barrier loses a turn, and goes when it has none left.</summary>
        public void TurnEnded(ICombatUnit unit)
        {
            if (ReferenceEquals(unit, _acting))
            {
                _acting = null;
            }
            if (unit == null || !_barriers.TryGetValue(unit, out var barrier))
            {
                return;
            }
            if (barrier.SkipNextEnd)
            {
                barrier.SkipNextEnd = false;
                return;
            }
            barrier.TurnsLeft--;
            if (barrier.TurnsLeft <= 0)
            {
                _barriers.Remove(unit);
            }
        }
    }
}
