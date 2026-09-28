using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Pins the threat formula's promises: a fight opens even, threat biases the pick, and - the
    /// owner's rule - it never decides it outright, however lopsided the table gets.
    /// </summary>
    public class ThreatTableTests
    {
        private static MockCombatUnit Hero(string name, int health = 30)
        {
            return new MockCombatUnit(name, 5, 2, health, 5, isHero: true);
        }

        [Test]
        public void Chances_NoThreatYet_AreEven()
        {
            var heroes = new List<ICombatUnit> { Hero("A"), Hero("B"), Hero("C") };

            var chances = ThreatTable.Chances(heroes, new ThreatTable());

            foreach (var chance in chances)
            {
                Assert.AreEqual(1f / 3f, chance, 1e-5f);
            }
        }

        [Test]
        public void Chances_NullTable_AreEven()
        {
            var heroes = new List<ICombatUnit> { Hero("A"), Hero("B") };

            var chances = ThreatTable.Chances(heroes, null);

            Assert.AreEqual(0.5f, chances[0], 1e-5f);
            Assert.AreEqual(0.5f, chances[1], 1e-5f);
        }

        [Test]
        public void Chances_MoreThreat_IsMoreLikely()
        {
            var a = Hero("A");
            var b = Hero("B");
            var table = new ThreatTable();
            table.Credit(a, 12, 0);
            table.Credit(b, 4, 0);

            var chances = ThreatTable.Chances(new List<ICombatUnit> { a, b }, table);

            // weights 22 and 14: 0.25 + 0.5 * 22/36 = 0.5556
            Assert.AreEqual(0.25f + 0.5f * 22f / 36f, chances[0], 1e-4f);
            Assert.Greater(chances[0], chances[1]);
        }

        [Test]
        public void Chances_OverwhelmingThreat_NeverPastTheCeilingOrUnderTheFloor()
        {
            var a = Hero("A");
            var b = Hero("B");
            var table = new ThreatTable();
            table.Credit(a, 100000, 0);

            var chances = ThreatTable.Chances(new List<ICombatUnit> { a, b }, table);

            Assert.LessOrEqual(chances[0], 0.75f + 1e-4f, "two heroes: nobody is ever more than 75%");
            Assert.GreaterOrEqual(chances[1], 0.25f - 1e-4f, "two heroes: nobody is ever under 25%");
        }

        [Test]
        public void Chances_FourHeroes_FloorIsAnEighth()
        {
            var heroes = new List<ICombatUnit> { Hero("A"), Hero("B"), Hero("C"), Hero("D") };
            var table = new ThreatTable();
            table.Credit(heroes[0], 100000, 0);

            var chances = ThreatTable.Chances(heroes, table);

            Assert.AreEqual(1f, chances.Sum(), 1e-4f);
            for (int i = 1; i < 4; i++)
            {
                Assert.GreaterOrEqual(chances[i], 0.125f - 1e-4f);
            }
        }

        [Test]
        public void Chances_DeadHero_IsNeverPicked()
        {
            var a = Hero("A");
            var b = Hero("B", health: 0);
            var table = new ThreatTable();
            table.Credit(b, 500, 0);

            var chances = ThreatTable.Chances(new List<ICombatUnit> { a, b }, table);

            Assert.AreEqual(1f, chances[0], 1e-5f);
            Assert.AreEqual(0f, chances[1]);
        }

        [Test]
        public void Pick_RollsAcrossTheWholeRange_ReachEveryHero()
        {
            var a = Hero("A");
            var b = Hero("B");
            var heroes = new List<ICombatUnit> { a, b };
            var table = new ThreatTable();
            table.Credit(a, 1000, 0);

            // a holds ~75%: rolls below that land on a, the rest on b.
            Assert.AreSame(a, ThreatTable.Pick(heroes, table, 0f));
            Assert.AreSame(a, ThreatTable.Pick(heroes, table, 0.7f));
            Assert.AreSame(b, ThreatTable.Pick(heroes, table, 0.8f));
            Assert.AreSame(b, ThreatTable.Pick(heroes, table, 0.9999f));
        }

        [Test]
        public void Pick_NobodyAlive_ReturnsNull()
        {
            var heroes = new List<ICombatUnit> { Hero("A", health: 0) };

            Assert.IsNull(ThreatTable.Pick(heroes, new ThreatTable(), 0.5f));
        }

        [Test]
        public void ThreatFor_HealingCountsHalf()
        {
            Assert.AreEqual(10f, ThreatTable.ThreatFor(10, 0));
            Assert.AreEqual(5f, ThreatTable.ThreatFor(0, 10));
        }

        [Test]
        public void ThreatFor_AbilityMultiplierAndBonus_Apply()
        {
            // (10 damage + 4 healing * 0.5) * 2 + 15
            Assert.AreEqual(39f, ThreatTable.ThreatFor(10, 4, multiplier: 2f, bonus: 15));
        }

        [Test]
        public void ThreatFor_BonusAlone_DrawsThreatWithoutDamage()
        {
            Assert.AreEqual(25f, ThreatTable.ThreatFor(0, 0, multiplier: 1f, bonus: 25));
        }

        [Test]
        public void ThreatFor_ZeroMultiplier_LandsUnnoticed()
        {
            Assert.AreEqual(0f, ThreatTable.ThreatFor(40, 0, multiplier: 0f));
        }

        [Test]
        public void Clear_ForgetsAFallenHero()
        {
            var a = Hero("A");
            var table = new ThreatTable();
            table.Credit(a, 50, 0);

            table.Clear(a);

            Assert.AreEqual(0f, table.Get(a));
        }
    }
}
