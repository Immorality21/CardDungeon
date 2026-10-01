using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    public class TurnManager
    {
        // Public so the balance model can convert agility into turn frequency without
        // hardcoding a second copy of this constant.
        public const float BASE_TICKS = 100f;

        private Dictionary<ICombatUnit, float> _ticksUntilTurn = new Dictionary<ICombatUnit, float>();
        private CombatBuffTracker _buffTracker;

        // Units taken off the clock with their counters frozen (the party while a summon replaces it).
        private readonly Dictionary<ICombatUnit, float> _suspended = new Dictionary<ICombatUnit, float>();

        // A unit that takes the very next turn, ahead of the clock (a summon's arrival turn).
        private ICombatUnit _actsNext;

        public void SetBuffTracker(CombatBuffTracker buffTracker)
        {
            _buffTracker = buffTracker;
        }

        public void Initialize(List<ICombatUnit> units)
        {
            _ticksUntilTurn.Clear();
            _suspended.Clear();
            _actsNext = null;

            foreach (var unit in units)
            {
                float agility = Mathf.Max(1, GetEffectiveAgility(unit));
                _ticksUntilTurn[unit] = BASE_TICKS / agility;
            }
        }

        public ICombatUnit GetNextUnit()
        {
            // A unit inserted to act next goes first and costs the clock nothing: no time passes for
            // anyone else. It then joins the clock as if it had just acted.
            if (_actsNext != null)
            {
                var inserted = _actsNext;
                _actsNext = null;
                if (inserted.IsAlive && _ticksUntilTurn.ContainsKey(inserted))
                {
                    _ticksUntilTurn[inserted] = BASE_TICKS / Mathf.Max(1, GetEffectiveAgility(inserted));
                    return inserted;
                }
            }

            // Find the alive unit with the lowest ticks (soonest to act)
            ICombatUnit next = null;
            float lowest = float.MaxValue;

            foreach (var kvp in _ticksUntilTurn)
            {
                if (kvp.Key.IsAlive && kvp.Value < lowest)
                {
                    lowest = kvp.Value;
                    next = kvp.Key;
                }
            }

            if (next == null)
            {
                return null;
            }

            // Advance time: subtract the lowest value from everyone
            var keys = _ticksUntilTurn.Keys.ToList();
            foreach (var unit in keys)
            {
                _ticksUntilTurn[unit] -= lowest;
            }

            // The acting unit gets a new turn timer based on their effective agility
            float agility = Mathf.Max(1, GetEffectiveAgility(next));
            _ticksUntilTurn[next] = BASE_TICKS / agility;

            return next;
        }

        public void RemoveUnit(ICombatUnit unit)
        {
            _ticksUntilTurn.Remove(unit);
            _suspended.Remove(unit);
            if (ReferenceEquals(_actsNext, unit))
            {
                _actsNext = null;
            }
        }

        /// <summary>
        /// Adds a unit to a fight already under way. With <paramref name="actsNext"/> it takes the
        /// very next turn ahead of the clock (a party-replacing summon arriving, §4b); otherwise it
        /// waits a full turn at its own Agility, like a unit that has just acted.
        /// </summary>
        public void AddUnit(ICombatUnit unit, bool actsNext)
        {
            if (unit == null)
            {
                return;
            }
            _ticksUntilTurn[unit] = BASE_TICKS / Mathf.Max(1, GetEffectiveAgility(unit));
            if (actsNext)
            {
                _actsNext = unit;
            }
        }

        /// <summary>
        /// Takes units off the clock with their counters frozen: time passes for everyone else and
        /// not for them, they are out of the turn-order preview, and <see cref="Resume"/> puts them
        /// back exactly where they left. What a party-replacing summon does to the party (§4b).
        /// </summary>
        public void Suspend(IEnumerable<ICombatUnit> units)
        {
            if (units == null)
            {
                return;
            }
            foreach (var unit in units)
            {
                if (unit != null && _ticksUntilTurn.TryGetValue(unit, out float ticks))
                {
                    _suspended[unit] = ticks;
                    _ticksUntilTurn.Remove(unit);
                }
            }
        }

        /// <summary>Every suspended unit back on the clock, at the counter it was frozen with.</summary>
        public void Resume()
        {
            foreach (var pair in _suspended)
            {
                _ticksUntilTurn[pair.Key] = pair.Value;
            }
            _suspended.Clear();
        }

        /// <summary>
        /// The most a unit's counter may hold, as a multiple of its own full turn: one turn plus one
        /// extra. A unit that has just acted sits at 1, so delay can buy at most one whole turn of
        /// tempo against it and never a lock.
        /// </summary>
        public const float MaxDelayedTurns = 2f;

        /// <summary>
        /// Pushes <paramref name="unit"/>'s next turn back by <paramref name="fractionOfTurn"/> of its
        /// own full turn at its current Agility (0.5 = half a turn), capped at
        /// <see cref="MaxDelayedTurns"/>. Returns how far it actually moved, as a fraction of a turn:
        /// 0 when it was already at the cap, or is not on the clock at all (suspended, gone).
        /// </summary>
        public float Delay(ICombatUnit unit, float fractionOfTurn)
        {
            if (unit == null || fractionOfTurn <= 0f || !_ticksUntilTurn.TryGetValue(unit, out float ticks))
            {
                return 0f;
            }
            float turn = BASE_TICKS / Mathf.Max(1, GetEffectiveAgility(unit));
            float delayed = Mathf.Min(ticks + fractionOfTurn * turn, MaxDelayedTurns * turn);
            if (delayed <= ticks)
            {
                return 0f;
            }
            _ticksUntilTurn[unit] = delayed;
            return (delayed - ticks) / turn;
        }

        public bool IsSuspended(ICombatUnit unit)
        {
            return unit != null && _suspended.ContainsKey(unit);
        }

        public List<ICombatUnit> GetTurnOrder(int count)
        {
            // Preview the next N turns without modifying state
            var snapshot = new Dictionary<ICombatUnit, float>(_ticksUntilTurn);
            var order = new List<ICombatUnit>();

            // The inserted unit leads the preview, exactly as GetNextUnit will take it.
            if (_actsNext != null && _actsNext.IsAlive && snapshot.ContainsKey(_actsNext) && count > 0)
            {
                order.Add(_actsNext);
                snapshot[_actsNext] = BASE_TICKS / Mathf.Max(1, GetEffectiveAgility(_actsNext));
            }

            for (int i = order.Count; i < count; i++)
            {
                ICombatUnit next = null;
                float lowest = float.MaxValue;

                foreach (var kvp in snapshot)
                {
                    if (kvp.Key.IsAlive && kvp.Value < lowest)
                    {
                        lowest = kvp.Value;
                        next = kvp.Key;
                    }
                }

                if (next == null)
                {
                    break;
                }

                var keys = snapshot.Keys.ToList();
                foreach (var unit in keys)
                {
                    snapshot[unit] -= lowest;
                }

                float agility = Mathf.Max(1, GetEffectiveAgility(next));
                snapshot[next] = BASE_TICKS / agility;
                order.Add(next);
            }

            return order;
        }

        private int GetEffectiveAgility(ICombatUnit unit)
        {
            // Item/level bonuses fold in via the unit's own effective stat; combat buffs/debuffs
            // stack on top of that.
            int baseAgility = unit.GetEffectiveStat(StatType.Agility);
            if (_buffTracker != null)
            {
                baseAgility += _buffTracker.GetBuffAmount(unit, StatType.Agility);
            }

            return baseAgility;
        }
    }
}
