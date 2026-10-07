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

        /// <summary>The unit whose turn is open, or null between turns. See <see cref="BeginTurn"/>.</summary>
        private ICombatUnit _actingUnit;

        /// <summary>
        /// The fight's event stream, raised when an over-time tick moves health. Set by whoever owns
        /// the fight (<c>CombatManager</c>, the simulator's encounter loop); null in tests and room
        /// events, where ticks still land but nobody is told.
        /// </summary>
        public CombatEvents Events { get; set; }

        /// <summary>
        /// Opens <paramref name="unit"/>'s turn. Anything applied to that same unit before its
        /// <see cref="TickBuffs"/> is marked <see cref="CombatBuff.SkipNextUpkeep"/>, so the upkeep at
        /// the end of the turn it was cast in does not eat one of its turns. Without this a 3-turn
        /// self-buff (War Cry, the Bloodfang Boar on its own summoner) lasted only two of the caster's
        /// turns while lasting three of everyone else's. Both the live loop and
        /// <c>EncounterSimulator</c> call this; a caller that never does gets the old behaviour.
        /// </summary>
        public void BeginTurn(ICombatUnit unit)
        {
            _actingUnit = unit;
        }

        /// <summary>Marks an entry that was just applied or refreshed, if it landed on the acting unit.</summary>
        private void MarkIfOwnTurn(ICombatUnit unit, CombatBuff buff)
        {
            if (buff != null && unit != null && ReferenceEquals(unit, _actingUnit))
            {
                buff.SkipNextUpkeep = true;
            }
        }

        public void ApplyBuff(ICombatUnit unit, StatType stat, int amount, int duration)
        {
            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            var buff = new CombatBuff
            {
                Stat = stat,
                Amount = amount,
                TurnsRemaining = duration
            };
            _activeBuffs[unit].Add(buff);
            MarkIfOwnTurn(unit, buff);
        }

        /// <summary>
        /// Applies a stat change that was authored as a percentage of the target's own stat. The
        /// <paramref name="amount"/> is the already-resolved delta. <b>A newer percentage buff on the
        /// same stat replaces the older one</b> — re-summoning the Bloodfang Boar refreshes its +50%
        /// rather than doubling it — while flat buffs (<see cref="ApplyBuff"/>) keep stacking.
        /// </summary>
        public void ApplyPercentBuff(ICombatUnit unit, StatType stat, int amount, int duration)
        {
            if (unit == null || duration <= 0)
            {
                return;
            }
            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                buffs = new List<CombatBuff>();
                _activeBuffs[unit] = buffs;
            }

            buffs.RemoveAll(b => b.IsPercent && !b.IsStatusEffect && !b.IsResistance && b.Stat == stat);
            var buff = new CombatBuff
            {
                Stat = stat,
                Amount = amount,
                TurnsRemaining = duration,
                IsPercent = true
            };
            buffs.Add(buff);
            MarkIfOwnTurn(unit, buff);
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
            // An immune unit never carries the status at all - no icon, no turn skipped, no tick.
            if (unit == null || duration <= 0 || StatusImmunity.IsImmune(unit, type))
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
                MarkIfOwnTurn(unit, existing);
                return;
            }

            var buff = new CombatBuff
            {
                BuffType = type,
                IsStatusEffect = true,
                TurnsRemaining = duration
            };
            _activeBuffs[unit].Add(buff);
            MarkIfOwnTurn(unit, buff);
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
        /// <param name="source">Who applied it, credited with what its ticks do - a poison kill is the
        /// poisoner's. Defaults to the unit whose turn is open (<see cref="BeginTurn"/>), which is the
        /// caster for every cast, combo and summon; pass it when something else is the author.</param>
        public void ApplyOverTime(ICombatUnit unit, BuffType type, int amountPerTurn, int duration,
            ICombatUnit source = null)
        {
            if (unit == null || amountPerTurn <= 0 || duration <= 0 || StatusImmunity.IsImmune(unit, type))
            {
                return;
            }

            if (!_activeBuffs.ContainsKey(unit))
            {
                _activeBuffs[unit] = new List<CombatBuff>();
            }

            var existing = _activeBuffs[unit]
                .FirstOrDefault(b => b.IsStatusEffect && b.BuffType == type);

            source = source ?? _actingUnit;
            if (existing != null)
            {
                // The latest application is the one credited: it is the one keeping it going.
                existing.Amount = Mathf.Max(existing.Amount, amountPerTurn);
                existing.TurnsRemaining = Mathf.Max(existing.TurnsRemaining, duration);
                existing.Source = source ?? existing.Source;
                MarkIfOwnTurn(unit, existing);
                return;
            }

            var buff = new CombatBuff
            {
                BuffType = type,
                IsStatusEffect = true,
                Amount = amountPerTurn,
                TurnsRemaining = duration,
                Source = source
            };
            _activeBuffs[unit].Add(buff);
            MarkIfOwnTurn(unit, buff);
        }

        /// <summary>
        /// Fires every over-time effect on <paramref name="unit"/> and <b>applies the result to its
        /// health</b>, returning one entry per tick that actually moved the bar.
        ///
        /// <para>Ticks fire on the unit's own turn — damage at its start, healing at its end (see
        /// <see cref="TickTiming"/>) — and always before <see cref="TickBuffs"/>, so a buff with one
        /// turn left ticks once more before it expires. Per-victim-turn rather than on a global clock is deliberate — the turn <i>is</i>
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
            var ticks = ResolveOverTime(unit, TickTiming.StartOfTurn);
            ticks.AddRange(ResolveOverTime(unit, TickTiming.EndOfTurn));
            return ticks;
        }

        /// <summary>
        /// Fires only the over-time effects that tick at <paramref name="timing"/> — the entry point
        /// the live loop and <c>EncounterSimulator</c> use: <see cref="TickTiming.StartOfTurn"/> right
        /// after the turn opens, before the unit acts or a skip is checked, and
        /// <see cref="TickTiming.EndOfTurn"/> just before <see cref="TickBuffs"/>. The overload without
        /// a timing resolves both, in that order, and exists for callers that only want the
        /// arithmetic.
        /// </summary>
        public List<OverTimeTick> ResolveOverTime(ICombatUnit unit, TickTiming timing)
        {
            var ticks = new List<OverTimeTick>();
            if (unit == null || !unit.IsAlive || !_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return ticks;
            }

            // Copied, because a tick can kill the unit and a killing blow may prune the list.
            foreach (var buff in buffs.ToList())
            {
                if (!buff.IsStatusEffect || buff.Amount <= 0 || buff.SkipNextUpkeep)
                {
                    // SkipNextUpkeep: applied this very turn, so its first tick is the next turn's.
                    continue;
                }

                var overTime = BuffHandlerRegistry.Get(buff.BuffType) as IOverTimeBuffHandler;
                if (overTime == null || overTime.Timing != timing)
                {
                    continue;
                }

                int moved = overTime.Heals
                    ? ApplyHealTick(unit, buff.Amount, buff.Source)
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
        private int ApplyHealTick(ICombatUnit unit, int amount, ICombatUnit source)
        {
            return HealthOps.Heal(unit, RunFear.Current.ScaleHealing(unit, amount),
                new HealthSource(source, HealthCause.OverTime), Events).Healed;
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
                return ApplyHealTick(unit, -damage, buff.Source);
            }

            if (damage == 0)
            {
                return 0;
            }

            HealthOps.Damage(unit, damage,
                new HealthSource(buff.Source, HealthCause.OverTime, overTime.TickDamageType), Events);
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

            var buff = new CombatBuff
            {
                IsResistance = true,
                ResistanceType = type,
                Amount = percent,
                TurnsRemaining = duration
            };
            _activeBuffs[unit].Add(buff);
            MarkIfOwnTurn(unit, buff);
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
            if (ReferenceEquals(unit, _actingUnit))
            {
                _actingUnit = null;   // the turn is over
            }

            if (!_activeBuffs.TryGetValue(unit, out var buffs))
            {
                return;
            }

            foreach (var buff in buffs)
            {
                if (buff.SkipNextUpkeep)
                {
                    buff.SkipNextUpkeep = false;
                    continue;
                }
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
            _actingUnit = null;
        }
    }
}
