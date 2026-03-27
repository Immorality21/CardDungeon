using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ComboDetectorTests
    {
        private CardTagTracker _tagTracker;
        private MockCombatUnit _target;

        private CardComboSO CreateCombo(string name, List<string> requiredTags,
            CardEffectType effect = CardEffectType.Damage, int power = 10)
        {
            var combo = ScriptableObject.CreateInstance<CardComboSO>();
            combo.ComboName = name;
            combo.RequiredTags = requiredTags;
            combo.BonusEffect = effect;
            combo.BonusPower = power;
            return combo;
        }

        [SetUp]
        public void SetUp()
        {
            _tagTracker = new CardTagTracker();
            _target = new MockCombatUnit("Enemy", attack: 5, defense: 3, health: 50, isHero: false);
        }

        [Test]
        public void DetectCombo_NoIncomingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(null, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyIncomingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(new List<string>(), _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NoExistingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            var result = detector.DetectCombo(new List<string> { "Fire", "Oil" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_AllTagsFromIncoming_NoneExisting_ReturnsNull()
        {
            // Combo requires Fire + Oil, but both come from the same card — no existing tags
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            // No pre-existing tags on target
            var result = detector.DetectCombo(new List<string> { "Fire", "Oil" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_MatchingTags_ReturnsCombo()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ReversedTagOrder_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            // Oil is the existing tag, Fire is incoming — same combo
            _tagTracker.ApplyTags(_target, new List<string> { "Fire" }, 3);

            var result = detector.DetectCombo(new List<string> { "Oil" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_PartialMatch_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil", "Wind" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ThreeTagCombo_AllPresent_Triggers()
        {
            var combo = CreateCombo("Storm", new List<string> { "Fire", "Oil", "Wind" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil", "Wind" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Storm", result.ComboName);
        }

        [Test]
        public void DetectCombo_MultipleCombos_ReturnsFirst()
        {
            var ignite = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var freeze = CreateCombo("Freeze", new List<string> { "Ice", "Water" });
            var detector = new ComboDetector(new List<CardComboSO> { ignite, freeze });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_WrongComboTags_ReturnsNull()
        {
            var ignite = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { ignite });

            _tagTracker.ApplyTags(_target, new List<string> { "Ice" }, 3);

            var result = detector.DetectCombo(new List<string> { "Water" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyCombos_ReturnsNull()
        {
            var detector = new ComboDetector(new List<CardComboSO>());

            _tagTracker.ApplyTags(_target, new List<string> { "Fire" }, 3);

            var result = detector.DetectCombo(new List<string> { "Oil" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NullCombos_ReturnsNull()
        {
            var detector = new ComboDetector(null);

            _tagTracker.ApplyTags(_target, new List<string> { "Fire" }, 3);

            var result = detector.DetectCombo(new List<string> { "Oil" }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ComboWithEmptyRequiredTags_Skipped()
        {
            var broken = CreateCombo("Broken", new List<string>());
            var ignite = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { broken, ignite });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ExtraTags_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<string> { "Oil", "Poison" }, 3);

            var result = detector.DetectCombo(new List<string> { "Fire", "Ice" }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_RequiresMixOfExistingAndIncoming()
        {
            // Even though all required tags are present in incoming only, combo should NOT fire
            // because there must be at least one tag from existing
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            // Existing tags are unrelated
            _tagTracker.ApplyTags(_target, new List<string> { "Poison" }, 3);

            // Incoming has both Fire and Oil
            var result = detector.DetectCombo(new List<string> { "Fire", "Oil" }, _target, _tagTracker);

            Assert.IsNull(result);
        }
    }
}
