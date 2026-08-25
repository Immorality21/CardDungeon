using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

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

        public void ApplyStatusEffect(ICombatUnit unit, BuffType type, int duration)
        {
            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            _activeBuffs[unit].Add(new CombatBuff
            {
                BuffType = type,
                IsStatusEffect = true,
                TurnsRemaining = duration
            });
        }

        /// <summary>
        /// Grants <paramref name="unit"/> <paramref name="percent"/> extra resistance to
        /// <paramref name="type"/> for <paramref name="duration"/> turns. Deliberately <b>not</b> a
        /// write into the unit's <c>Resistances</c> list: that list outlives combat (a hero's innate
        /// and gear resistance), so a temporary entry there would need its own expiry bookkeeping and
        /// would leak into the next fight. The damage path adds this bonus at the call site instead,
        /// exactly as it already does for stat buffs.
        /// </summary>
        public void ApplyResistance(ICombatUnit unit, DamageType type, int percent, int duration)
        {
            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            _activeBuffs[unit].Add(new CombatBuff
            {
                IsResistance = true,
                ResistanceType = type,
                Amount = percent,
                TurnsRemaining = duration
            });
        }

        /// <summary>
        /// Total temporary resistance to <paramref name="type"/>, summed and <b>uncapped</b> — three
        /// stacked 40% cloaks reach 120%, which is how absorption is meant to be reachable. The cap
        /// lives in <see cref="DamageCalculator.Calculate"/>, which clamps innate + gear + this
        /// together.
        /// </summary>
        public float GetResistanceBonus(ICombatUnit unit, DamageType type)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return 0f;
            }

            return buffs.Where(b => b.IsResistance && b.ResistanceType == type).Sum(b => b.Amount);
        }

        public bool HasStatusEffect(ICombatUnit unit, BuffType type)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return false;
            }

            return buffs.Any(b => b.IsStatusEffect && b.BuffType == type);
        }

        public void RemoveStatusEffect(ICombatUnit unit, BuffType type)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return;
            }

            buffs.RemoveAll(b => b.IsStatusEffect && b.BuffType == type);

            if (buffs.Count == 0)
            {
                _activeBuffs.Remove(unit);
            }
        }

        public int GetBuffAmount(ICombatUnit unit, StatType stat)
        {
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return 0;
            }

            return buffs.Where(b => !b.IsStatusEffect && !b.IsResistance && b.Stat == stat).Sum(b => b.Amount);
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

        public List<BuffType> GetActiveStatusEffects(ICombatUnit unit)
        {
            var result = new List<BuffType>();
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return result;
            }

            foreach (var buff in buffs)
            {
                if (buff.IsStatusEffect)
                {
                    result.Add(buff.BuffType);
                }
            }

            return result;
        }

        public List<string> GetActiveTagsOnUnit(ICombatUnit unit)
        {
            return new List<string>();
        }

        public void Clear()
        {
            _activeBuffs.Clear();
        }
    }
}
