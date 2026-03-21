using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards
{
    public class CombatBuffTracker
    {
        private Dictionary<ICombatUnit, List<CombatBuff>> _activeBuffs = new Dictionary<ICombatUnit, List<CombatBuff>>();

        public void ApplyBuff(ICombatUnit unit, StatType stat, int amount, int duration)
        {
            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            _activeBuffs[unit].Add(new CombatBuff
            {
                Stat = stat,
                Amount = amount,
                TurnsRemaining = duration
            });
        }

        public int GetBuffAmount(ICombatUnit unit, StatType stat)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return 0;
            }

            return buffs.Where(b => b.Stat == stat).Sum(b => b.Amount);
        }

        public void TickBuffs(ICombatUnit unit)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return;
            }

            foreach (var buff in buffs)
            {
                buff.TurnsRemaining--;
            }

            buffs.RemoveAll(b => b.TurnsRemaining <= 0);

            if (buffs.Count == 0)
            {
                _activeBuffs.Remove(unit);
            }
        }

        public List<string> GetActiveTagsOnUnit(ICombatUnit unit)
        {
            // Tags are tracked separately via CardTagTracker, not here
            return new List<string>();
        }

        public void Clear()
        {
            _activeBuffs.Clear();
        }
    }
}
