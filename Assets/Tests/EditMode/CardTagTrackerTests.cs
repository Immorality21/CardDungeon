using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CardTagTrackerTests
    {
        private CardTagTracker _tracker;
        private MockCombatUnit _unit;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CardTagTracker();
            _unit = new MockCombatUnit("Target", attack: 5, defense: 3, health: 50, isHero: false);
        }

        [Test]
        public void GetTagsOnUnit_NoTags_ReturnsEmptySet()
        {
            var tags = _tracker.GetTagsOnUnit(_unit);

            Assert.IsEmpty(tags);
        }

        [Test]
        public void ApplyTags_SingleTag_IsRetrievable()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 3);

            var tags = _tracker.GetTagsOnUnit(_unit);

            Assert.IsTrue(tags.Contains(CardTag.Fire));
            Assert.AreEqual(1, tags.Count);
        }

        [Test]
        public void ApplyTags_MultipleTags_AllPresent()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire, CardTag.Oil }, 3);

            var tags = _tracker.GetTagsOnUnit(_unit);

            Assert.IsTrue(tags.Contains(CardTag.Fire));
            Assert.IsTrue(tags.Contains(CardTag.Oil));
            Assert.AreEqual(2, tags.Count);
        }

        [Test]
        public void ApplyTags_NullList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.ApplyTags(_unit, null, 3));
            Assert.IsEmpty(_tracker.GetTagsOnUnit(_unit));
        }

        [Test]
        public void ApplyTags_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.ApplyTags(_unit, new List<CardTag>(), 3));
            Assert.IsEmpty(_tracker.GetTagsOnUnit(_unit));
        }

        [Test]
        public void ApplyTags_DuplicateTag_RefreshesToLongerDuration()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 2);
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 5);

            // Tick 4 times — should still exist because it was refreshed to 5
            _tracker.TickTags(_unit);
            _tracker.TickTags(_unit);
            _tracker.TickTags(_unit);
            _tracker.TickTags(_unit);

            var tags = _tracker.GetTagsOnUnit(_unit);
            Assert.IsTrue(tags.Contains(CardTag.Fire));
        }

        [Test]
        public void ApplyTags_DuplicateTag_DoesNotDowngrade()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 5);
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 1);

            // Tick 3 times — should still exist because the shorter duration didn't overwrite
            _tracker.TickTags(_unit);
            _tracker.TickTags(_unit);
            _tracker.TickTags(_unit);

            var tags = _tracker.GetTagsOnUnit(_unit);
            Assert.IsTrue(tags.Contains(CardTag.Fire));
        }

        [Test]
        public void ApplyTags_DifferentUnits_TrackedSeparately()
        {
            var unit2 = new MockCombatUnit("Other", attack: 1, defense: 1, health: 10, isHero: false);

            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 3);
            _tracker.ApplyTags(unit2, new List<CardTag> { CardTag.Ice }, 3);

            Assert.IsTrue(_tracker.GetTagsOnUnit(_unit).Contains(CardTag.Fire));
            Assert.IsFalse(_tracker.GetTagsOnUnit(_unit).Contains(CardTag.Ice));
            Assert.IsTrue(_tracker.GetTagsOnUnit(unit2).Contains(CardTag.Ice));
            Assert.IsFalse(_tracker.GetTagsOnUnit(unit2).Contains(CardTag.Fire));
        }

        [Test]
        public void TickTags_DecrementsDurationAndExpires()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 2);

            _tracker.TickTags(_unit);
            Assert.IsTrue(_tracker.GetTagsOnUnit(_unit).Contains(CardTag.Fire));

            _tracker.TickTags(_unit);
            Assert.IsEmpty(_tracker.GetTagsOnUnit(_unit));
        }

        [Test]
        public void TickTags_MixedDurations_OnlyShorterExpires()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 1);
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Ice }, 3);

            _tracker.TickTags(_unit);

            var tags = _tracker.GetTagsOnUnit(_unit);
            Assert.IsFalse(tags.Contains(CardTag.Fire));
            Assert.IsTrue(tags.Contains(CardTag.Ice));
        }

        [Test]
        public void TickTags_OnlyAffectsTargetUnit()
        {
            var unit2 = new MockCombatUnit("Other", attack: 1, defense: 1, health: 10, isHero: false);
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire }, 1);
            _tracker.ApplyTags(unit2, new List<CardTag> { CardTag.Fire }, 1);

            _tracker.TickTags(_unit);

            Assert.IsEmpty(_tracker.GetTagsOnUnit(_unit));
            Assert.IsTrue(_tracker.GetTagsOnUnit(unit2).Contains(CardTag.Fire));
        }

        [Test]
        public void TickTags_UntrackedUnit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.TickTags(_unit));
        }

        [Test]
        public void Clear_RemovesAllTags()
        {
            _tracker.ApplyTags(_unit, new List<CardTag> { CardTag.Fire, CardTag.Ice }, 3);

            _tracker.Clear();

            Assert.IsEmpty(_tracker.GetTagsOnUnit(_unit));
        }
    }
}
