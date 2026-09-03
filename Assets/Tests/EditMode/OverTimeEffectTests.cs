using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The over-time layer: burn, poison, bleed and regeneration, plus the Silence gate that ships
    /// alongside them.
    ///
    /// <para>Everything here is deliberately driven through <see cref="CombatBuffTracker"/> rather
    /// than through a hand-rolled tick, because that resolver is the single arithmetic both the live
    /// turn loop and <c>EncounterSimulator</c> call. A second implementation in a test would pin the
    /// test, not the game.</para>
    /// </summary>
    public class OverTimeEffectTests
    {
        private CombatBuffTracker _tracker;
        private MockCombatUnit _unit;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CombatBuffTracker();
            // Endurance 20 is the defense curve's own K, so it halves anything that honours defense —
            // which makes "does this tick bypass Endurance" visible as an exact 2x in these tests.
            _unit = new MockCombatUnit("Target", strength: 10, endurance: 20, health: 100, agility: 10);
        }

        // ------------------------------------------------------------------ ticking

        [Test]
        public void ResolveOverTime_Poison_DamagesTheUnit()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 6, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(1, ticks.Count);
            Assert.AreEqual(BuffType.Poisoned, ticks[0].BuffType);
            Assert.IsFalse(ticks[0].Heals);
            Assert.AreEqual(94, _unit.Stats.Health, "Poison ignores Endurance, so all 6 lands.");
        }

        [Test]
        public void ResolveOverTime_Poison_IgnoresEndurance_ButBurnDoesNot()
        {
            var poisoned = new MockCombatUnit("A", 10, 20, 100, 10);
            var burning = new MockCombatUnit("B", 10, 20, 100, 10);
            _tracker.ApplyOverTime(poisoned, BuffType.Poisoned, 6, 3);
            _tracker.ApplyOverTime(burning, BuffType.Burning, 6, 3);

            _tracker.ResolveOverTime(poisoned);
            _tracker.ResolveOverTime(burning);

            // At Endurance 20 the defense curve is exactly 50%, so burn lands half of what poison does.
            Assert.AreEqual(94, poisoned.Stats.Health);
            Assert.AreEqual(97, burning.Stats.Health);
        }

        [Test]
        public void ResolveOverTime_Regeneration_HealsAndClampsToMaxHealth()
        {
            _unit.Stats.Health = 98;
            _tracker.ApplyOverTime(_unit, BuffType.Regenerating, 5, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(1, ticks.Count);
            Assert.IsTrue(ticks[0].Heals);
            Assert.AreEqual(2, ticks[0].Amount, "Only the missing 2 HP is reported, not the full 5.");
            Assert.AreEqual(100, _unit.Stats.Health);
        }

        [Test]
        public void ResolveOverTime_AtFullHealth_ReportsNoRegenerationTick()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Regenerating, 5, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            // Nothing moved, so there is nothing to float. A "+0" popup reads as a bug.
            CollectionAssert.IsEmpty(ticks);
        }

        [Test]
        public void ResolveOverTime_HonoursResistance()
        {
            _unit.Resistances.Add(new Resistance { DamageType = DamageType.Fire, Percent = 50 });
            _tracker.ApplyOverTime(_unit, BuffType.Burning, 8, 3);

            _tracker.ResolveOverTime(_unit);

            // 8 raw -> 50% Fire resistance -> 4 -> 50% defense curve at Endurance 20 -> 2.
            Assert.AreEqual(98, _unit.Stats.Health);
        }

        [Test]
        public void ResolveOverTime_AbsorbedElement_HealsInsteadOfDamaging()
        {
            _unit.Stats.Health = 50;
            _unit.Resistances.Add(new Resistance { DamageType = DamageType.Fire, Percent = 200 });
            _tracker.ApplyOverTime(_unit, BuffType.Burning, 8, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(1, ticks.Count);
            Assert.IsTrue(ticks[0].Heals, "Above 100% resistance the element heals, as it does for a cast.");
            Assert.Greater(_unit.Stats.Health, 50);
        }

        [Test]
        public void ResolveOverTime_CanKill()
        {
            _unit.Stats.Health = 3;
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 6, 3);

            _tracker.ResolveOverTime(_unit);

            Assert.IsFalse(_unit.IsAlive, "A tick has to be able to finish a unit off, or it is not damage.");
        }

        [Test]
        public void ResolveOverTime_OnADeadUnit_DoesNothing()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 6, 3);
            _unit.Stats.Health = 0;

            var ticks = _tracker.ResolveOverTime(_unit);

            CollectionAssert.IsEmpty(ticks);
        }

        [Test]
        public void ResolveOverTime_StacksSeparateEffectsOnOneUnit()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 4, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Bleeding, 4, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(2, ticks.Count, "Different over-time effects are independent.");
        }

        // ------------------------------------------------------------------ duration and refresh

        [Test]
        public void OverTime_TicksOncePerTurnForItsWholeDuration()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 5, 3);

            int ticks = 0;
            for (int turn = 0; turn < 6; turn++)
            {
                // The live loop's order: resolve, then tick durations down.
                ticks += _tracker.ResolveOverTime(_unit).Count;
                _tracker.TickBuffs(_unit);
            }

            Assert.AreEqual(3, ticks, "Exactly Duration ticks - the last one lands before it expires.");
            Assert.AreEqual(85, _unit.Stats.Health);
        }

        [Test]
        public void ApplyOverTime_Reapplied_RefreshesRatherThanStacking()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 5, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 5, 3);

            var ticks = _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(1, ticks.Count, "One poison, not two.");
            Assert.AreEqual(95, _unit.Stats.Health);
        }

        [Test]
        public void ApplyOverTime_Reapplied_KeepsTheStrongerAmountAndLongerDuration()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 9, 1);
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 3, 5);

            int ticks = 0;
            for (int turn = 0; turn < 6; turn++)
            {
                ticks += _tracker.ResolveOverTime(_unit).Count;
                _tracker.TickBuffs(_unit);
            }

            Assert.AreEqual(5, ticks, "The longer duration wins.");
            Assert.AreEqual(55, _unit.Stats.Health, "The stronger per-turn amount wins: 5 x 9.");
        }

        [Test]
        public void ApplyOverTime_ZeroAmountOrDuration_AppliesNothing()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 0, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Burning, 5, 0);

            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Poisoned));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Burning));
        }

        // ------------------------------------------------------------------ authoring routes

        [Test]
        public void DebuffExecutor_AppliesAPoisonThatDamages()
        {
            var caster = new MockCombatUnit("Caster", 10, 5, 50, 10);
            var effect = new SpellEffect
            {
                EffectType = SpellEffectType.Debuff,
                BuffType = BuffType.Poisoned,
                Power = 4,
                Duration = 3,
                ScalingStat = StatType.None
            };

            new DebuffEffectExecutor().Execute(
                effect, caster, new List<ICombatUnit> { _unit }, _tracker, new EffectResult());
            _tracker.ResolveOverTime(_unit);

            // The debuff executor negates the magnitude; the handler takes it as a magnitude, so a
            // poison authored as a Debuff can never come out healing the target.
            Assert.AreEqual(96, _unit.Stats.Health);
        }

        [Test]
        public void BuffExecutor_AppliesARegenerationThatHeals()
        {
            var caster = new MockCombatUnit("Caster", 10, 5, 50, 10);
            _unit.Stats.Health = 50;
            var effect = new SpellEffect
            {
                EffectType = SpellEffectType.Buff,
                BuffType = BuffType.Regenerating,
                Power = 4,
                Duration = 3,
                ScalingStat = StatType.None
            };

            new BuffEffectExecutor().Execute(
                effect, caster, new List<ICombatUnit> { _unit }, _tracker, new EffectResult());
            _tracker.ResolveOverTime(_unit);

            Assert.AreEqual(54, _unit.Stats.Health);
        }

        [Test]
        public void Burning_IsDousedByIce_AsFrozenIsThawedByFire()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Burning);

            Assert.IsTrue(handler.IsRemovedByDamageType(DamageType.Ice));
            Assert.IsFalse(handler.IsRemovedByDamageType(DamageType.Fire));
        }

        [Test]
        public void NoOverTimeEffect_SkipsTheTurn()
        {
            // Losing the turn *and* burning is two effects. Frozen is the one that stops a unit.
            foreach (var type in new[]
                     { BuffType.Burning, BuffType.Poisoned, BuffType.Bleeding, BuffType.Regenerating })
            {
                Assert.IsFalse(BuffHandlerRegistry.Get(type).SkipsTurn, type.ToString());
            }
        }

        // ------------------------------------------------------------------ silence

        [Test]
        public void Silence_IsAStatusEffectThatDoesNotSkipTheTurn()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Silenced);

            handler.Apply(_unit, 0, 3, _tracker);

            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Silenced));
            Assert.IsFalse(handler.SkipsTurn, "A silenced unit still attacks - it is not a stun.");
        }

        [Test]
        public void Silence_Reapplied_RefreshesRatherThanDuplicating()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Silenced);

            handler.Apply(_unit, 0, 2, _tracker);
            handler.Apply(_unit, 0, 4, _tracker);

            var active = _tracker.GetActiveStatusEffects(_unit);
            Assert.AreEqual(1, active.Count(t => t == BuffType.Silenced),
                "A duplicate record draws two icons and makes a cure report the status twice.");

            for (int turn = 0; turn < 3; turn++)
            {
                _tracker.TickBuffs(_unit);
            }
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Silenced), "The longer duration wins.");
        }

        [Test]
        public void Silence_TicksDownAndExpires()
        {
            BuffHandlerRegistry.Get(BuffType.Silenced).Apply(_unit, 0, 2, _tracker);

            _tracker.TickBuffs(_unit);
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Silenced));

            _tracker.TickBuffs(_unit);
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Silenced));
        }

        // ------------------------------------------------------------------ the cure

        [Test]
        public void CureStatusEffects_ClearsHarmfulStatusesOnly()
        {
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 4, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Burning, 4, 3);
            _tracker.ApplyStatusEffect(_unit, BuffType.Frozen, 3);
            _tracker.ApplyStatusEffect(_unit, BuffType.Silenced, 3);
            _tracker.ApplyStatusEffect(_unit, BuffType.Haste, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Regenerating, 4, 3);

            var cured = _tracker.CureStatusEffects(_unit);

            Assert.AreEqual(4, cured.Count);
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Poisoned));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Burning));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Frozen));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Silenced));
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Haste),
                "A cure that stripped the party's own buffs would be a trap.");
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Regenerating));
        }

        [Test]
        public void CureStatusEffects_LeavesStatBuffsAlone()
        {
            _tracker.ApplyBuff(_unit, StatType.Strength, 5, 3);
            _tracker.ApplyOverTime(_unit, BuffType.Poisoned, 4, 3);

            _tracker.CureStatusEffects(_unit);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_unit, StatType.Strength));
        }

        [Test]
        public void CureStatusEffects_OnACleanUnit_ReportsNothing()
        {
            CollectionAssert.IsEmpty(_tracker.CureStatusEffects(_unit));
        }

        // ------------------------------------------------------------------ registry contract

        [Test]
        public void EveryOverTimeHandler_IsRegisteredAndReachable()
        {
            var expected = new Dictionary<BuffType, bool>
            {
                { BuffType.Burning, false },
                { BuffType.Poisoned, false },
                { BuffType.Bleeding, false },
                { BuffType.Regenerating, true }
            };

            foreach (var pair in expected)
            {
                var overTime = BuffHandlerRegistry.Get(pair.Key) as IOverTimeBuffHandler;
                Assert.IsNotNull(overTime, $"{pair.Key} has no over-time handler.");
                Assert.AreEqual(pair.Value, overTime.Heals, pair.Key.ToString());
                Assert.IsNotEmpty(overTime.TickLabel, pair.Key.ToString());
            }
        }

        [Test]
        public void NoNewBuffType_IsUnhandled()
        {
            // The registry returns null for an unhandled type and every caller treats that as inert,
            // so a missing handler is silent. This is the thing that makes it loud.
            var unhandled = BuffHandlerRegistry.Unhandled();

            CollectionAssert.IsEmpty(unhandled, string.Join(", ", unhandled.Select(t => t.ToString())));
        }
    }
}
