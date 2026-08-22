using System.Collections.Generic;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The roll that decides an event is in the room at all. Pinned down because rarity is the whole
    /// point of room events - a find that turns up every time is furniture.
    /// </summary>
    public class RoomEventSpawnTests
    {
        [Test]
        public void ChancePercent_NoModifier_IsTheBaseChance()
        {
            Assert.AreEqual(5f, RoomEventSpawn.ChancePercent(5f, statValue: 10, modifierRate: 0f), 0.0001f,
                "A rate of 0 means the stat does not enter into it.");
            Assert.AreEqual(5f, RoomEventSpawn.ChancePercent(5f, statValue: 0, modifierRate: 1.5f), 0.0001f,
                "Neither does a stat of 0.");
        }

        [Test]
        public void ChancePercent_MatchesTheAuthoredFormula()
        {
            // base 5, Luck 10, rate 1.5 -> 5 + 5 * (10 * 1.5 / 100) = 5 + 5 * 0.15 = 5.75
            Assert.AreEqual(5.75f, RoomEventSpawn.ChancePercent(5f, statValue: 10, modifierRate: 1.5f), 0.0001f);
        }

        [Test]
        public void ChancePercent_ModifierIsRelativeToTheBase()
        {
            // The same stat and rate scale a rare find and a common one by the same proportion.
            float rare = RoomEventSpawn.ChancePercent(5f, 10, 5f);
            float common = RoomEventSpawn.ChancePercent(50f, 10, 5f);

            Assert.AreEqual(7.5f, rare, 0.0001f);
            Assert.AreEqual(75f, common, 0.0001f);
            Assert.AreEqual(rare / 5f, common / 50f, 0.0001f,
                "A relative modifier must not swamp a deliberately rare event.");
        }

        [Test]
        public void ChancePercent_RisesWithTheStat()
        {
            float low = RoomEventSpawn.ChancePercent(10f, 4, 5f);
            float high = RoomEventSpawn.ChancePercent(10f, 12, 5f);

            Assert.Less(low, high, "Investing in the modifier stat has to show up in the odds.");
        }

        [Test]
        public void ChancePercent_ZeroBase_NeverAppears()
        {
            Assert.AreEqual(0f, RoomEventSpawn.ChancePercent(0f, 99, 99f), 0.0001f,
                "A base of 0 is how an event is switched off; no stat should override that.");
        }

        [Test]
        public void ChancePercent_NegativeBase_IsTreatedAsOff()
        {
            Assert.AreEqual(0f, RoomEventSpawn.ChancePercent(-5f, 10, 1.5f), 0.0001f);
        }

        [Test]
        public void ChancePercent_ClampsAtCertainty()
        {
            Assert.AreEqual(RoomEventSpawn.MaxChancePercent,
                RoomEventSpawn.ChancePercent(80f, statValue: 40, modifierRate: 10f), 0.0001f,
                "A high enough stat must not push the chance past 100%.");
        }

        [Test]
        public void ChancePercent_HundredBase_StaysGuaranteed()
        {
            Assert.AreEqual(100f, RoomEventSpawn.ChancePercent(100f, 0, 0f), 0.0001f,
                "100 is how a room whose identity IS the interaction - a treasury - is authored.");
        }

        // ---- The stat gate: at least one hero meets at least one requirement ----

        private static StatBlock Party(StatType stat, int value)
        {
            var block = new StatBlock();
            block[stat] = value;
            return block;
        }

        /// <summary>
        /// Party-best per stat, which is what placement passes in - the maximum over heroes, checked
        /// stat by stat, so a requirement is covered when *somebody* covers it.
        /// </summary>
        private static StatBlock PartyBest(params UnitStat[] stats)
        {
            var block = new StatBlock();
            foreach (var stat in stats)
            {
                block[stat.Type] = stat.Amount;
            }
            return block;
        }

        [Test]
        public void MeetsRequirements_EmptyList_IsNoGate()
        {
            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(new List<UnitStat>(), new StatBlock()));
            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(null, new StatBlock()));
        }

        [Test]
        public void MeetsRequirements_SingleRequirement_IsCheckedAgainstThePartyBest()
        {
            var requirement = new List<UnitStat> { new UnitStat(StatType.Intelligence, 6) };

            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(requirement, Party(StatType.Intelligence, 10)));
            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(requirement, Party(StatType.Intelligence, 6)),
                "The threshold is inclusive.");
            Assert.IsFalse(RoomEventSpawn.MeetsRequirements(requirement, Party(StatType.Intelligence, 5)));
        }

        [Test]
        public void MeetsRequirements_SeveralRequirements_AllMustBeMet()
        {
            var requirements = new List<UnitStat>
            {
                new UnitStat(StatType.Intelligence, 8),
                new UnitStat(StatType.Spirit, 8)
            };

            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(requirements,
                    PartyBest(new UnitStat(StatType.Intelligence, 10), new UnitStat(StatType.Spirit, 12))),
                "Both covered.");
            Assert.IsFalse(RoomEventSpawn.MeetsRequirements(requirements, Party(StatType.Spirit, 12)),
                "Spirit alone is not enough - the list is an AND.");
        }

        [Test]
        public void MeetsRequirements_RequirementsMayBeCoveredByDifferentHeroes()
        {
            // 10 Strength and 15 Intelligence. A Warrior at 11 Strength and an Acolyte at 20
            // Intelligence cover one each, which passes - the same hero does not have to do both.
            var requirements = new List<UnitStat>
            {
                new UnitStat(StatType.Strength, 10),
                new UnitStat(StatType.Intelligence, 15)
            };

            var oneEach = PartyBest(new UnitStat(StatType.Strength, 11), new UnitStat(StatType.Intelligence, 20));
            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(requirements, oneEach));

            // A stronger Warrior does not make up for nobody reaching the Intelligence line.
            var noScholar = PartyBest(new UnitStat(StatType.Strength, 15), new UnitStat(StatType.Intelligence, 14));
            Assert.IsFalse(RoomEventSpawn.MeetsRequirements(requirements, noScholar));
        }

        [Test]
        public void MeetsRequirements_MissingStat_ReadsAsZero()
        {
            var requirement = new List<UnitStat> { new UnitStat(StatType.Luck, 1) };

            Assert.IsFalse(RoomEventSpawn.MeetsRequirements(requirement, new StatBlock()));
        }

        [Test]
        public void MeetsRequirements_UnconfiguredRows_AreIgnoredRatherThanImpossible()
        {
            var onlyBlank = new List<UnitStat> { new UnitStat(StatType.None, 5) };

            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(onlyBlank, new StatBlock()),
                "A freshly added inspector row defaults to None; it must not delete the event.");

            var mixed = new List<UnitStat>
            {
                new UnitStat(StatType.None, 5),
                new UnitStat(StatType.Luck, 10)
            };
            Assert.IsFalse(RoomEventSpawn.MeetsRequirements(mixed, Party(StatType.Luck, 4)),
                "A configured row still gates, even beside a blank one.");
            Assert.IsTrue(RoomEventSpawn.MeetsRequirements(mixed, Party(StatType.Luck, 10)),
                "And a blank row must not block a party that meets every configured one.");
        }

        [Test]
        public void Spawns_UsesTheSuppliedRoll()
        {
            Assert.IsTrue(RoomEventSpawn.Spawns(5.75f, 5.74f));
            Assert.IsFalse(RoomEventSpawn.Spawns(5.75f, 5.75f));
            Assert.IsFalse(RoomEventSpawn.Spawns(5.75f, 90f));
        }

        [Test]
        public void Spawns_ZeroChance_NeverSpawnsEvenOnAZeroRoll()
        {
            Assert.IsFalse(RoomEventSpawn.Spawns(0f, 0f));
        }

        [Test]
        public void Spawns_Certainty_AlwaysSpawns()
        {
            Assert.IsTrue(RoomEventSpawn.Spawns(100f, 99.999f));
        }
    }
}
