using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class SlowHasteBuffTests
    {
        private CombatBuffTracker _tracker;
        private MockCombatUnit _unit;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CombatBuffTracker();
            _unit = new MockCombatUnit("Hero", attack: 10, defense: 5, health: 100, agility: 10);
        }

        // ---- SlowBuffHandler ----

        [Test]
        public void SlowHandler_Apply_AppliesNegativeAgilityBuff()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);

            handler.Apply(_unit, 5, 3, _tracker);

            Assert.AreEqual(-5, _tracker.GetBuffAmount(_unit, StatType.Agility));
        }

        [Test]
        public void SlowHandler_Apply_TracksAsStatusEffect()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);

            handler.Apply(_unit, 5, 3, _tracker);

            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Slow));
        }

        [Test]
        public void SlowHandler_DoesNotSkipTurn()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);

            Assert.IsFalse(handler.SkipsTurn);
        }

        [Test]
        public void SlowHandler_GetDisplayText_ReturnsSlow()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);

            Assert.AreEqual("Slow!", handler.GetDisplayText(5));
        }

        // ---- HasteBuffHandler ----

        [Test]
        public void HasteHandler_Apply_AppliesPositiveAgilityBuff()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);

            handler.Apply(_unit, 5, 3, _tracker);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_unit, StatType.Agility));
        }

        [Test]
        public void HasteHandler_Apply_TracksAsStatusEffect()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);

            handler.Apply(_unit, 5, 3, _tracker);

            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Haste));
        }

        [Test]
        public void HasteHandler_DoesNotSkipTurn()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);

            Assert.IsFalse(handler.SkipsTurn);
        }

        [Test]
        public void HasteHandler_GetDisplayText_ReturnsHaste()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);

            Assert.AreEqual("Haste!", handler.GetDisplayText(5));
        }

        // ---- Expiration ----

        [Test]
        public void SlowHandler_BuffAndStatusExpireAfterDuration()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);

            handler.Apply(_unit, 5, 2, _tracker);

            _tracker.TickBuffs(_unit);
            Assert.AreEqual(-5, _tracker.GetBuffAmount(_unit, StatType.Agility));
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Slow));

            _tracker.TickBuffs(_unit);
            Assert.AreEqual(0, _tracker.GetBuffAmount(_unit, StatType.Agility));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Slow));
        }

        [Test]
        public void HasteHandler_BuffAndStatusExpireAfterDuration()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);

            handler.Apply(_unit, 5, 2, _tracker);

            _tracker.TickBuffs(_unit);
            Assert.AreEqual(5, _tracker.GetBuffAmount(_unit, StatType.Agility));
            Assert.IsTrue(_tracker.HasStatusEffect(_unit, BuffType.Haste));

            _tracker.TickBuffs(_unit);
            Assert.AreEqual(0, _tracker.GetBuffAmount(_unit, StatType.Agility));
            Assert.IsFalse(_tracker.HasStatusEffect(_unit, BuffType.Haste));
        }

        // ---- TurnManager integration ----

        [Test]
        public void TurnManager_SlowBuff_ReducesTurnFrequency()
        {
            var fast = new MockCombatUnit("Fast", attack: 1, defense: 1, health: 10, agility: 20);
            var normal = new MockCombatUnit("Normal", attack: 1, defense: 1, health: 10, agility: 10);

            // Apply slow to the fast unit, reducing agility by 15 (effective: 5)
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);
            handler.Apply(fast, 15, 99, _tracker);

            var turnManager = new TurnManager();
            turnManager.SetBuffTracker(_tracker);
            turnManager.Initialize(new List<ICombatUnit> { fast, normal });

            // Normal (agi 10) should now go before Fast (effective agi 5)
            var first = turnManager.GetNextUnit();
            Assert.AreEqual("Normal", first.DisplayName);
        }

        [Test]
        public void TurnManager_HasteBuff_IncreasesTurnFrequency()
        {
            var slow = new MockCombatUnit("Slow", attack: 1, defense: 1, health: 10, agility: 5);
            var normal = new MockCombatUnit("Normal", attack: 1, defense: 1, health: 10, agility: 10);

            // Apply haste to the slow unit, boosting agility by 10 (effective: 15)
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);
            handler.Apply(slow, 10, 99, _tracker);

            var turnManager = new TurnManager();
            turnManager.SetBuffTracker(_tracker);
            turnManager.Initialize(new List<ICombatUnit> { slow, normal });

            // Slow (effective agi 15) should now go before Normal (agi 10)
            var first = turnManager.GetNextUnit();
            Assert.AreEqual("Slow", first.DisplayName);
        }

        [Test]
        public void TurnManager_WithoutBuffTracker_UsesBaseAgility()
        {
            var fast = new MockCombatUnit("Fast", attack: 1, defense: 1, health: 10, agility: 20);
            var slow = new MockCombatUnit("Slow", attack: 1, defense: 1, health: 10, agility: 5);

            var turnManager = new TurnManager();
            // No SetBuffTracker call
            turnManager.Initialize(new List<ICombatUnit> { slow, fast });

            var first = turnManager.GetNextUnit();
            Assert.AreEqual("Fast", first.DisplayName);
        }

        [Test]
        public void TurnManager_AgilityBuffClampsToMinimumOne()
        {
            var unit = new MockCombatUnit("Debuffed", attack: 1, defense: 1, health: 10, agility: 5);

            // Slow reduces agility by 100 — effective would be -95, clamped to 1
            var handler = BuffHandlerRegistry.Get(BuffType.Slow);
            handler.Apply(unit, 100, 99, _tracker);

            var turnManager = new TurnManager();
            turnManager.SetBuffTracker(_tracker);
            turnManager.Initialize(new List<ICombatUnit> { unit });

            // Should not throw — agility clamped to 1
            var next = turnManager.GetNextUnit();
            Assert.AreEqual("Debuffed", next.DisplayName);
        }

        [Test]
        public void TurnManager_GetTurnOrder_ReflectsAgilityBuffs()
        {
            var hero = new MockCombatUnit("Hero", attack: 1, defense: 1, health: 10, agility: 10);
            var enemy = new MockCombatUnit("Enemy", attack: 1, defense: 1, health: 10, agility: 10);

            // Haste the hero to agility 20
            var handler = BuffHandlerRegistry.Get(BuffType.Haste);
            handler.Apply(hero, 10, 99, _tracker);

            var turnManager = new TurnManager();
            turnManager.SetBuffTracker(_tracker);
            turnManager.Initialize(new List<ICombatUnit> { hero, enemy });

            var order = turnManager.GetTurnOrder(3);

            // Hero (agi 20) should get 2 of the first 3 turns
            int heroTurns = 0;
            foreach (var unit in order)
            {
                if (unit.DisplayName == "Hero")
                {
                    heroTurns++;
                }
            }

            Assert.AreEqual(2, heroTurns);
        }
    }
}
