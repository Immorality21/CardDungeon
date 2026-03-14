using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    public class TurnManager
    {
        private const float BASE_TICKS = 100f;

        private Dictionary<ICombatUnit, float> _ticksUntilTurn = new Dictionary<ICombatUnit, float>();

        public void Initialize(List<ICombatUnit> units)
        {
            _ticksUntilTurn.Clear();

            foreach (var unit in units)
            {
                float agility = Mathf.Max(1, unit.Stats.Agility);
                _ticksUntilTurn[unit] = BASE_TICKS / agility;
            }
        }

        public ICombatUnit GetNextUnit()
        {
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

            // The acting unit gets a new turn timer based on their agility
            float agility = Mathf.Max(1, next.Stats.Agility);
            _ticksUntilTurn[next] = BASE_TICKS / agility;

            return next;
        }

        public void RemoveUnit(ICombatUnit unit)
        {
            _ticksUntilTurn.Remove(unit);
        }

        public List<ICombatUnit> GetTurnOrder(int count)
        {
            // Preview the next N turns without modifying state
            var snapshot = new Dictionary<ICombatUnit, float>(_ticksUntilTurn);
            var order = new List<ICombatUnit>();

            for (int i = 0; i < count; i++)
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

                float agility = Mathf.Max(1, next.Stats.Agility);
                snapshot[next] = BASE_TICKS / agility;
                order.Add(next);
            }

            return order;
        }
    }
}
