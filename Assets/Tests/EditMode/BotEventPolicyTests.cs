using System.Collections.Generic;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.Sandbox.Bot;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class BotEventPolicyTests
    {
        private static RoomEventOption Option(RoomEventOptionKind kind, int goldCost = 0)
        {
            return new RoomEventOption { Kind = kind, GoldCost = goldCost };
        }

        private static readonly List<RoomEventOption> Gamble = new List<RoomEventOption>
        {
            Option(RoomEventOptionKind.StatCheck),
            Option(RoomEventOptionKind.Decline),
        };

        private static readonly List<RoomEventOption> GambleOrSure = new List<RoomEventOption>
        {
            Option(RoomEventOptionKind.Guaranteed),
            Option(RoomEventOptionKind.StatCheck),
            Option(RoomEventOptionKind.Decline),
        };

        [Test]
        public void Choose_Everything_GamblesAtAnyOdds()
        {
            Assert.AreEqual(0, BotEventPolicy.Choose(Gamble, 0.05f, BotEventHabit.Everything, null));
        }

        [Test]
        public void Choose_Everything_TakesTheGambleOverTheSureThing()
        {
            Assert.AreEqual(1, BotEventPolicy.Choose(GambleOrSure, 0.2f, BotEventHabit.Everything, null));
        }

        [Test]
        public void Choose_OddsOnly_DeclinesUnderEvenOdds()
        {
            Assert.AreEqual(1, BotEventPolicy.Choose(Gamble, 0.49f, BotEventHabit.OddsOnly, null));
        }

        [Test]
        public void Choose_OddsOnly_GamblesAtEvenOdds()
        {
            Assert.AreEqual(0, BotEventPolicy.Choose(Gamble, 0.5f, BotEventHabit.OddsOnly, null));
        }

        [Test]
        public void Choose_Safe_TakesTheSureThingOverAGoodGamble()
        {
            Assert.AreEqual(0, BotEventPolicy.Choose(GambleOrSure, 0.7f, BotEventHabit.Safe, null));
        }

        [Test]
        public void Choose_Safe_DeclinesWhatCostsGold()
        {
            var options = new List<RoomEventOption>
            {
                Option(RoomEventOptionKind.Guaranteed, goldCost: 10),
                Option(RoomEventOptionKind.Decline),
            };

            Assert.AreEqual(1, BotEventPolicy.Choose(options, 0.5f, BotEventHabit.Safe, null));
        }

        [Test]
        public void Choose_SkipsWhatTheWindowDoesNotOffer()
        {
            var blocked = GambleOrSure[1];

            Assert.AreEqual(0, BotEventPolicy.Choose(GambleOrSure, 0.9f, BotEventHabit.Everything, o => o != blocked));
        }

        [Test]
        public void Choose_NoDeclineAndNothingWanted_ReturnsMinusOne()
        {
            var options = new List<RoomEventOption> { Option(RoomEventOptionKind.StatCheck) };

            Assert.AreEqual(-1, BotEventPolicy.Choose(options, 0.1f, BotEventHabit.Safe, null));
        }
    }
}
