using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

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

        /// <summary>
        /// Applies a plain status effect — Frozen, Slow, Haste, Silenced.
        ///
        /// <para><b>Reapplying refreshes rather than duplicating</b>, matching
        /// <see cref="ApplyOverTime"/>. It used to append unconditionally, so a unit Silenced on two
        /// consecutive turns carried two identical records: two mute icons side by side on the HP
        /// bar, the type listed twice by <see cref="GetActiveStatusEffects"/>, and a cure reporting
        /// "clearing Silenced, Silenced". Nothing read the count, so the duplicate was pure
        /// presentation damage.</para>
        /// </summary>
        public void ApplyStatusEffect(ICombatUnit unit, BuffType type, int duration)
        {
            if (unit == null || duration <= 0)
            {
                return;
            }

            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            var existing = _activeBuffs[unit]
                .FirstOrDefault(b => b.IsStatusEffect && b.BuffType == type);

            if (existing != null)
            {
                existing.TurnsRemaining = Mathf.Max(existing.TurnsRemaining, duration);
                return;
            }

            _activeBuffs[unit].Add(new CombatBuff
            {
                BuffType = type,
                IsStatusEffect = true,
                TurnsRemaining = duration
            });
        }

        /// <summary>
        /// Applies an over-time status effect — burn, poison, bleed, regeneration — carrying
        /// <paramref name="amountPerTurn"/> in the record's <c>Amount</c>. It is an ordinary status
        /// effect otherwise, so it expires through the same <see cref="TickBuffs"/> path and shows on
        /// the same status strip; what makes it tick is that its handler implements
        /// <see cref="IOverTimeBuffHandler"/>, which <see cref="ResolveOverTime"/> asks for.
        ///
        /// <para><b>Reapplying refreshes, it does not stack.</b> The stronger per-turn amount and the
        /// longer remaining duration both win, so a second poison on an already-poisoned target
        /// upgrades it or tops it up but never doubles it. Stacking magnitude would turn every fight
        /// into a race the closed-form balance model cannot price — <c>BalanceMath</c> needs an
        /// expected damage per application, and an unbounded stack has none.</para>
        /// </summary>
        public void ApplyOverTime(ICombatUnit unit, BuffType type, int amountPerTurn, int duration)
        {
            if (unit == null || amountPerTurn <= 0 || duration <= 0)
            {
                return;
            }

            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            var existing = _activeBuffs[unit]
                .FirstOrDefault(b => b.IsStatusEffect && b.BuffType == type);

            if (existing != null)
            {
                existing.Amount = Mathf.Max(existing.Amount, amountPerTurn);
                existing.TurnsRemaining = Mathf.Max(existing.TurnsRemaining, duration);
                return;
            }

            _activeBuffs[unit].Add(new CombatBuff
            {
                BuffType = type,
                IsStatusEffect = true,
                Amount = amountPerTurn,
                TurnsRemaining = duration
            });
        }

        /// <summary>
        /// Fires every over-time effect on <paramref name="unit"/> and <b>applies the result to its
        /// health</b>, returning one entry per tick that actually moved the bar.
        ///
        /// <para>Call it at the end of the unit's own turn, immediately before
        /// <see cref="TickBuffs"/>: a buff with one turn left has to tick once more before it
        /// expires. Per-victim-turn rather than on a global clock is deliberate — the turn <i>is</i>
        /// the unit of time in a CTB system, so Haste and Slow change how often a target burns for
        /// free, and a unit that never gets a turn never takes a tick.</para>
        ///
        /// <para>Damage runs the full <see cref="DamageCalculator"/> pipeline, so resistances,
        /// weaknesses and absorption all apply exactly as they do to a cast — an Ice-resistant unit
        /// is no better off against poison than against a sword, and a Fire-absorbing one is
        /// <i>healed</i> by a burn. Endurance applies unless the handler says it does not.</para>
        ///
        /// <para>The arithmetic lives here and nowhere else: the live turn loop
        /// (<c>CombatManager</c>) and <c>EncounterSimulator</c> both call this rather than
        /// re-deriving a tick, so the balance model cannot drift from the game.</para>
        /// </summary>
        public List<OverTimeTick> ResolveOverTime(ICombatUnit unit)
        {
            var ticks = new List<OverTimeTick>();
            if (unit == null || !unit.IsAlive || !_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return ticks;
            }

            // Copied, because a tick can kill the unit and a killing blow may prune the list.
            foreach (var buff in buffs.ToList())
            {
                if (!buff.IsStatusEffect || buff.Amount <= 0)
                {
                    continue;
                }

                var overTime = BuffHandlerRegistry.Get(buff.BuffType) as IOverTimeBuffHandler;
                if (overTime == null)
                {
                    continue;
                }

                int moved = overTime.Heals
                    ? ApplyHealTick(unit, buff.Amount)
                    : ApplyDamageTick(unit, buff, overTime);

                if (moved == 0)
                {
                    // Fully resisted, or already at full health. Nothing to show — a floating "0"
                    // reads as a bug rather than as immunity.
                    continue;
                }

                ticks.Add(new OverTimeTick
                {
                    BuffType = buff.BuffType,
                    Amount = Mathf.Abs(moved),
                    Heals = moved > 0,
                    Label = overTime.TickLabel
                });

                if (!unit.IsAlive)
                {
                    // A dead unit takes no further ticks this turn.
                    break;
                }
            }

            return ticks;
        }

        /// <summary>Returns the health moved: positive healed, negative damaged, 0 for nothing.</summary>
        private int ApplyHealTick(ICombatUnit unit, int amount)
        {
            int room = unit.GetEffectiveStat(StatType.MaxHealth) - unit.Stats.Health;
            int healed = Mathf.Clamp(amount, 0, Mathf.Max(0, room));
            unit.Stats.Health += healed;
            return healed;
        }

        /// <summary>Returns the health moved: negative damaged, positive absorbed, 0 for immune.</summary>
        private int ApplyDamageTick(ICombatUnit unit, CombatBuff buff, IOverTimeBuffHandler overTime)
        {
            int defense = overTime.IgnoresDefense
                ? 0
                : unit.GetEffectiveStat(StatType.Endurance) + GetBuffAmount(unit, StatType.Endurance);

            float resistanceBonus = GetResistanceBonus(unit, overTime.TickDamageType);
            int damage = DamageCalculator.Calculate(
                buff.Amount, defense, overTime.TickDamageType, unit.Resistances, resistanceBonus);

            if (damage < 0)
            {
                // Absorbed: the element heals this target. Same rule the cast path already follows.
                return ApplyHealTick(unit, -damage);
            }

            if (damage == 0)
            {
                return 0;
            }

            unit.Stats.Health -= damage;
            return -damage;
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

        /// <summary>
        /// Removes every curable status effect from <paramref name="unit"/> and returns what was
        /// removed, so the caller can name them. See <see cref="BuffHandlerRegistry.IsCurable"/> for
        /// why the list is a design judgement rather than a handler property — a cure must not strip
        /// the party's own Haste or Regeneration.
        /// </summary>
        public List<BuffType> CureStatusEffects(ICombatUnit unit)
        {
            var cured = new List<BuffType>();
            if (unit == null || !_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return cured;
            }

            foreach (var buff in buffs)
            {
                if (buff.IsStatusEffect && BuffHandlerRegistry.IsCurable(buff.BuffType))
                {
                    cured.Add(buff.BuffType);
                }
            }

            buffs.RemoveAll(b => b.IsStatusEffect && BuffHandlerRegistry.IsCurable(b.BuffType));

            if (buffs.Count == 0)
            {
                _activeBuffs.Remove(unit);
            }

            return cured;
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
