using System.Collections.Generic;
using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class TurnManagerTests
    {
        private TurnManager _turnManager;

        [SetUp]
        public void SetUp()
        {
            _turnManager = new TurnManager();
        }

        [Test]
        public void GetNextUnit_HighestAgility_GoesFirst()
        {
            var fast = new MockCombatUnit("Fast", attack: 1, defense: 1, health: 10, agility: 20);
            var slow = new MockCombatUnit("Slow", attack: 1, defense: 1, health: 10, agility: 5);

            _turnManager.Initialize(new List<ICombatUnit> { slow, fast });

            var next = _turnManager.GetNextUnit();

            Assert.AreEqual("Fast", next.DisplayName);
        }

        [Test]
        public void GetNextUnit_EqualAgility_BothGetTurns()
        {
            var a = new MockCombatUnit("A", attack: 1, defense: 1, health: 10, agility: 10);
            var b = new MockCombatUnit("B", attack: 1, defense: 1, health: 10, agility: 10);

            _turnManager.Initialize(new List<ICombatUnit> { a, b });

            var first = _turnManager.GetNextUnit();
            var second = _turnManager.GetNextUnit();

            // Both should get a turn (order may vary, but both should appear)
            Assert.IsTrue(
                (first.DisplayName == "A" && second.DisplayName == "B") ||
                (first.DisplayName == "B" && second.DisplayName == "A"));
        }

        [Test]
        public void GetNextUnit_HighAgilityUnit_GetsManyTurns()
        {
            var fast = new MockCombatUnit("Fast", attack: 1, defense: 1, health: 10, agility: 20);
            var slow = new MockCombatUnit("Slow", attack: 1, defense: 1, health: 10, agility: 5);

            _turnManager.Initialize(new List<ICombatUnit> { slow, fast });

            // Over 5 turns, Fast (4x agility) should appear much more often
            int fastCount = 0;
            for (int i = 0; i < 5; i++)
            {
                var unit = _turnManager.GetNextUnit();
                if (unit.DisplayName == "Fast")
                {
                    fastCount++;
                }
            }

            Assert.GreaterOrEqual(fastCount, 3, "Fast unit with 4x agility should get at least 3 of 5 turns");
        }

        [Test]
        public void GetNextUnit_DeadUnitSkipped()
        {
            var alive = new MockCombatUnit("Alive", attack: 1, defense: 1, health: 10, agility: 5);
            var dead = new MockCombatUnit("Dead", attack: 1, defense: 0, health: 10, agility: 100);

            _turnManager.Initialize(new List<ICombatUnit> { alive, dead });

            // Kill the fast unit
            dead.Stats.Health = 0;

            var next = _turnManager.GetNextUnit();

            Assert.AreEqual("Alive", next.DisplayName);
        }

        [Test]
        public void GetNextUnit_AllDead_ReturnsNull()
        {
            var unit = new MockCombatUnit("Dead", attack: 1, defense: 1, health: 10, agility: 5);

            _turnManager.Initialize(new List<ICombatUnit> { unit });

            unit.Stats.Health = 0;

            var next = _turnManager.GetNextUnit();

            Assert.IsNull(next);
        }

        [Test]
        public void RemoveUnit_RemovedUnitNeverActsAgain()
        {
            var hero = new MockCombatUnit("Hero", attack: 1, defense: 1, health: 10, agility: 10);
            var enemy = new MockCombatUnit("Enemy", attack: 1, defense: 1, health: 10, agility: 10);

            _turnManager.Initialize(new List<ICombatUnit> { hero, enemy });

            _turnManager.RemoveUnit(enemy);

            for (int i = 0; i < 5; i++)
            {
                var next = _turnManager.GetNextUnit();
                Assert.AreEqual("Hero", next.DisplayName);
            }
        }

        [Test]
        public void GetTurnOrder_PreviewsWithoutModifyingState()
        {
            var fast = new MockCombatUnit("Fast", attack: 1, defense: 1, health: 10, agility: 20);
            var slow = new MockCombatUnit("Slow", attack: 1, defense: 1, health: 10, agility: 5);

            _turnManager.Initialize(new List<ICombatUnit> { slow, fast });

            var preview = _turnManager.GetTurnOrder(3);

            // Now actually get the next unit — it should still be the same as preview[0]
            var actual = _turnManager.GetNextUnit();

            Assert.AreEqual(3, preview.Count);
            Assert.AreEqual(preview[0].DisplayName, actual.DisplayName);
        }

        [Test]
        public void GetTurnOrder_ReturnsRequestedCount()
        {
            var a = new MockCombatUnit("A", attack: 1, defense: 1, health: 10, agility: 10);
            var b = new MockCombatUnit("B", attack: 1, defense: 1, health: 10, agility: 5);

            _turnManager.Initialize(new List<ICombatUnit> { a, b });

            var order = _turnManager.GetTurnOrder(6);

            Assert.AreEqual(6, order.Count);
        }

        [Test]
        public void GetTurnOrder_DeadUnitExcluded()
        {
            var alive = new MockCombatUnit("Alive", attack: 1, defense: 1, health: 10, agility: 10);
            var dead = new MockCombatUnit("Dead", attack: 1, defense: 1, health: 10, agility: 10);

            _turnManager.Initialize(new List<ICombatUnit> { alive, dead });

            dead.Stats.Health = 0;

            var order = _turnManager.GetTurnOrder(3);

            foreach (var unit in order)
            {
                Assert.AreEqual("Alive", unit.DisplayName);
            }
        }

        [Test]
        public void Initialize_ZeroAgility_ClampedToOne()
        {
            var unit = new MockCombatUnit("Zero", attack: 1, defense: 1, health: 10, agility: 0);

            _turnManager.Initialize(new List<ICombatUnit> { unit });

            // Should not throw — agility clamped to 1
            var next = _turnManager.GetNextUnit();

            Assert.AreEqual("Zero", next.DisplayName);
        }
    }
}
