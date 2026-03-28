using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DungeonDeckStateTests
    {
        private DungeonDeckState _state;

        [SetUp]
        public void SetUp()
        {
            _state = new DungeonDeckState();
        }

        // ---- MarkCardUsed / IsCardAvailable ----
        // Note: We cannot call Initialize (requires Hero MonoBehaviour + CardCollectionManager singleton),
        // but we can test MarkCardUsed and IsCardAvailable/GetSaveData independently since
        // MarkCardUsed creates the hero entry if missing.

        [Test]
        public void MarkCardUsed_CreatesEntryForNewHero()
        {
            _state.MarkCardUsed("Warrior", "Fireball");

            var saveData = _state.GetSaveData();

            Assert.AreEqual(1, saveData.Count);
            Assert.AreEqual("Warrior", saveData[0].HeroKey);
            Assert.Contains("Fireball", saveData[0].UsedCardKeys);
        }

        [Test]
        public void MarkCardUsed_MultipleCards_AllTracked()
        {
            _state.MarkCardUsed("Warrior", "Fireball");
            _state.MarkCardUsed("Warrior", "IceShard");

            var saveData = _state.GetSaveData();

            Assert.AreEqual(1, saveData.Count);
            Assert.AreEqual(2, saveData[0].UsedCardKeys.Count);
            Assert.Contains("Fireball", saveData[0].UsedCardKeys);
            Assert.Contains("IceShard", saveData[0].UsedCardKeys);
        }

        [Test]
        public void MarkCardUsed_DuplicateCard_OnlyStoredOnce()
        {
            _state.MarkCardUsed("Warrior", "Fireball");
            _state.MarkCardUsed("Warrior", "Fireball");

            var saveData = _state.GetSaveData();

            Assert.AreEqual(1, saveData[0].UsedCardKeys.Count);
        }

        [Test]
        public void MarkCardUsed_DifferentHeroes_TrackedSeparately()
        {
            _state.MarkCardUsed("Warrior", "Fireball");
            _state.MarkCardUsed("Tank", "Heal");

            var saveData = _state.GetSaveData();

            Assert.AreEqual(2, saveData.Count);
            var warrior = saveData.Find(d => d.HeroKey == "Warrior");
            var tank = saveData.Find(d => d.HeroKey == "Tank");
            Assert.Contains("Fireball", warrior.UsedCardKeys);
            Assert.Contains("Heal", tank.UsedCardKeys);
        }

        // ---- IsCardAvailable ----
        // Without Initialize, _heroDecks is empty so IsCardAvailable returns false for unknown heroes.

        [Test]
        public void IsCardAvailable_UnknownHero_ReturnsFalse()
        {
            Assert.IsFalse(_state.IsCardAvailable("Nobody", "Fireball"));
        }

        // ---- RestoreUsedCards ----

        [Test]
        public void RestoreUsedCards_NullData_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _state.RestoreUsedCards(null));
        }

        [Test]
        public void RestoreUsedCards_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _state.RestoreUsedCards(new List<DeckSaveData>()));
        }

        [Test]
        public void RestoreUsedCards_UnknownHero_Ignored()
        {
            var data = new List<DeckSaveData>
            {
                new DeckSaveData
                {
                    HeroKey = "UnknownHero",
                    UsedCardKeys = new List<string> { "Fireball" }
                }
            };

            // Should not throw — hero not in _usedCards so it's silently skipped
            Assert.DoesNotThrow(() => _state.RestoreUsedCards(data));

            // Save data should be empty since the hero was never initialized
            var saveData = _state.GetSaveData();
            Assert.AreEqual(0, saveData.Count);
        }

        // ---- GetSaveData ----

        [Test]
        public void GetSaveData_NoUsedCards_ReturnsEmptyList()
        {
            var saveData = _state.GetSaveData();

            Assert.IsNotNull(saveData);
            Assert.AreEqual(0, saveData.Count);
        }

        [Test]
        public void GetSaveData_SkipsHeroesWithNoUsedCards()
        {
            // MarkCardUsed creates entry, but if we could clear it the entry would be skipped.
            // Instead test that only heroes WITH used cards appear.
            _state.MarkCardUsed("Warrior", "Fireball");

            var saveData = _state.GetSaveData();

            Assert.AreEqual(1, saveData.Count);
            Assert.AreEqual("Warrior", saveData[0].HeroKey);
        }

        [Test]
        public void GetSaveData_RoundTrip_PreservesData()
        {
            _state.MarkCardUsed("Warrior", "Fireball");
            _state.MarkCardUsed("Warrior", "IceShard");
            _state.MarkCardUsed("Tank", "Heal");

            var saveData = _state.GetSaveData();

            // Simulate restoring into a new state
            var newState = new DungeonDeckState();
            // MarkCardUsed first to create entries (since we can't Initialize)
            newState.MarkCardUsed("Warrior", "placeholder");
            newState.MarkCardUsed("Tank", "placeholder");
            newState.RestoreUsedCards(saveData);

            var restored = newState.GetSaveData();
            var warrior = restored.Find(d => d.HeroKey == "Warrior");
            var tank = restored.Find(d => d.HeroKey == "Tank");

            Assert.IsTrue(warrior.UsedCardKeys.Contains("Fireball"));
            Assert.IsTrue(warrior.UsedCardKeys.Contains("IceShard"));
            Assert.IsTrue(tank.UsedCardKeys.Contains("Heal"));
        }
    }
}
