using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The one formatter every hub screen uses to show what a change does to a stat: only the
    /// change, signed, with a better / worse class (playtest 4 findings 41, 42, 43, 65).
    /// </summary>
    public class StatDeltaTests
    {
        private static StatBlock Block(params (StatType stat, int amount)[] values)
        {
            var block = new StatBlock();
            foreach (var value in values)
            {
                block[value.stat] = value.amount;
            }
            return block;
        }

        [Test]
        public void Format_Gain_HasPlusSign()
        {
            Assert.AreEqual("+3", StatDelta.Format(3));
        }

        [Test]
        public void Format_Loss_UsesARealMinusSign()
        {
            Assert.AreEqual("−6", StatDelta.Format(-6));
        }

        [Test]
        public void Format_NoChange_IsZero()
        {
            Assert.AreEqual("0", StatDelta.Format(0));
        }

        [Test]
        public void FormatPercent_RoundsToWholePoints()
        {
            Assert.AreEqual("+10%", StatDelta.FormatPercent(9.6f));
            Assert.AreEqual("−5%", StatDelta.FormatPercent(-5f));
            Assert.AreEqual("0%", StatDelta.FormatPercent(0.2f));
        }

        [Test]
        public void ClassFor_GainLossNone_BetterWorseNull()
        {
            Assert.AreEqual(StatDelta.BetterClass, StatDelta.ClassFor(1));
            Assert.AreEqual(StatDelta.WorseClass, StatDelta.ClassFor(-1));
            Assert.IsNull(StatDelta.ClassFor(0));
        }

        [Test]
        public void Changes_LeavesOutUnchangedStats()
        {
            var before = Block((StatType.Strength, 10), (StatType.MaxHealth, 50), (StatType.Luck, 4));
            var after = Block((StatType.Strength, 13), (StatType.MaxHealth, 44), (StatType.Luck, 4));

            var changes = StatDelta.Changes(before, after);

            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(StatType.Strength, changes[0].Stat);
            Assert.AreEqual(3, changes[0].Amount);
            Assert.AreEqual(StatType.MaxHealth, changes[1].Stat);
            Assert.AreEqual(-6, changes[1].Amount);
        }

        [Test]
        public void Changes_IdenticalBlocks_IsEmpty()
        {
            var block = Block((StatType.Agility, 7));
            Assert.IsEmpty(StatDelta.Changes(block, Block((StatType.Agility, 7))));
        }

        [Test]
        public void Changes_NullBlock_ReadsAsZeros()
        {
            var changes = StatDelta.Changes(null, Block((StatType.Spirit, 2)));
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(2, changes[0].Amount);
        }

        [Test]
        public void Biggest_SortsBySize_AndCaps()
        {
            var before = Block((StatType.Strength, 10), (StatType.Intelligence, 5), (StatType.MaxHealth, 40));
            var after = Block((StatType.Strength, 6), (StatType.Intelligence, 10), (StatType.MaxHealth, 41));

            var biggest = StatDelta.Biggest(before, after, 2);

            Assert.AreEqual(2, biggest.Count);
            Assert.AreEqual(StatType.Intelligence, biggest[0].Stat);
            Assert.AreEqual(StatType.Strength, biggest[1].Stat);
            Assert.AreEqual(-4, biggest[1].Amount);
        }

        [Test]
        public void Biggest_TiesKeepCatalogOrder()
        {
            var before = new StatBlock();
            var after = Block((StatType.Luck, 2), (StatType.Strength, -2));

            var biggest = StatDelta.Biggest(before, after, 2);

            Assert.AreEqual(StatType.Strength, biggest[0].Stat);
            Assert.AreEqual(StatType.Luck, biggest[1].Stat);
        }
    }
}
