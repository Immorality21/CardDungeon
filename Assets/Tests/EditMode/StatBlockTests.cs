using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="StatBlock"/> is the storage every stat now goes through, so its edge cases matter
    /// more than any single consumer's: sparse reads, duplicate handling, and the None guard.
    /// </summary>
    public class StatBlockTests
    {
        [Test]
        public void Indexer_AbsentStat_ReadsZero()
        {
            var block = new StatBlock();

            Assert.AreEqual(0, block[StatType.Strength],
                "A sparse block must read absent stats as 0, or every asset would need back-filling.");
        }

        [Test]
        public void Indexer_None_AlwaysReadsZeroAndIgnoresWrites()
        {
            var block = new StatBlock();

            block[StatType.None] = 99;

            Assert.AreEqual(0, block[StatType.None]);
            Assert.IsEmpty(block.Values, "None is 'unset' and must never become a stored entry.");
        }

        [Test]
        public void Indexer_Write_AddsThenUpdatesInPlace()
        {
            var block = new StatBlock();

            block[StatType.Luck] = 4;
            Assert.AreEqual(1, block.Values.Count);

            block[StatType.Luck] = 7;
            Assert.AreEqual(7, block[StatType.Luck]);
            Assert.AreEqual(1, block.Values.Count, "Re-writing a stat must not append a second entry.");
        }

        [Test]
        public void Indexer_DuplicateEntries_Sum()
        {
            var block = new StatBlock(
                new UnitStat(StatType.Strength, 3),
                new UnitStat(StatType.Strength, 4));

            Assert.AreEqual(7, block[StatType.Strength],
                "Duplicates sum, matching how DamageCalculator treats duplicate resistances, so "
                + "concatenating blocks is safe without de-duplicating first.");
        }

        [Test]
        public void Indexer_Write_CollapsesDuplicates()
        {
            var block = new StatBlock(
                new UnitStat(StatType.Agility, 3),
                new UnitStat(StatType.Agility, 4));

            block[StatType.Agility] = 5;

            Assert.AreEqual(5, block[StatType.Agility]);
            Assert.AreEqual(1, block.Values.Count, "A write normalises the stat to a single entry.");
        }

        [Test]
        public void Add_Block_SumsEveryStatAndCreatesMissingOnes()
        {
            var block = new StatBlock(new UnitStat(StatType.Strength, 5));
            var gains = new StatBlock(
                new UnitStat(StatType.Strength, 2),
                new UnitStat(StatType.MaxHealth, 10));

            block.Add(gains);

            Assert.AreEqual(7, block[StatType.Strength]);
            Assert.AreEqual(10, block[StatType.MaxHealth],
                "A level gain for a stat the hero did not have yet must create it.");
        }

        [Test]
        public void Add_NullBlock_IsIgnored()
        {
            var block = new StatBlock(new UnitStat(StatType.Spirit, 2));

            block.Add(null);

            Assert.AreEqual(2, block[StatType.Spirit]);
        }

        [Test]
        public void Clone_IsDeep()
        {
            var original = new StatBlock(new UnitStat(StatType.Intelligence, 6));
            var copy = original.Clone();

            copy[StatType.Intelligence] = 99;

            Assert.AreEqual(6, original[StatType.Intelligence],
                "SimUnit clones one block per simulated battle; a shallow copy would let battles "
                + "mutate each other.");
        }

        [Test]
        public void NonZero_SkipsZeroAndNoneEntries()
        {
            var block = new StatBlock(
                new UnitStat(StatType.Strength, 4),
                new UnitStat(StatType.Luck, 0),
                new UnitStat(StatType.None, 9));

            var listed = new System.Collections.Generic.List<UnitStat>(block.NonZero());

            Assert.AreEqual(1, listed.Count);
            Assert.AreEqual(StatType.Strength, listed[0].Type);
        }

        [Test]
        public void Add_ZeroAmount_IsANoOpAndCreatesNoEntry()
        {
            var block = new StatBlock();

            block.Add(StatType.Luck, 0);

            Assert.IsEmpty(block.Values,
                "Add(stat, 0) deliberately skips, unlike the indexer which stores an explicit zero. "
                + "Pinned because it is the kind of asymmetry a later tidy-up would 'simplify' away.");
        }

        [Test]
        public void Add_SelfIsRejectedRatherThanThrowing()
        {
            var block = new StatBlock(new UnitStat(StatType.Strength, 4));

            Assert.DoesNotThrow(() => block.Add(block),
                "The setter can remove entries, so enumerating self would invalidate the enumerator.");
            Assert.AreEqual(4, block[StatType.Strength], "Self-add must not double the values either.");
        }

        [Test]
        public void SurvivesUnitySerializationRoundTrip()
        {
            var block = new StatBlock(
                new UnitStat(StatType.Strength, 7),
                new UnitStat(StatType.MaxHealth, 22));

            var restored = UnityEngine.JsonUtility.FromJson<StatBlock>(UnityEngine.JsonUtility.ToJson(block));

            Assert.AreEqual(7, restored[StatType.Strength]);
            Assert.AreEqual(22, restored[StatType.MaxHealth],
                "Stat data is serialized by enum ordinal into every hero, enemy and item asset, so a "
                + "round-trip failure here would be a silent, project-wide data loss.");
        }

    }
}
