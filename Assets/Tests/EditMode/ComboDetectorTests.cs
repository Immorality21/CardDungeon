using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ComboDetectorTests
    {
        private CardTagTracker _tagTracker;
        private MockCombatUnit _target;

        private CardComboSO CreateCombo(string name, List<CardTag> requiredTags,
            CardEffectType effect = CardEffectType.Damage, int power = 10)
        {
            var combo = ScriptableObject.CreateInstance<CardComboSO>();
            combo.ComboName = name;
            combo.RequiredTags = requiredTags;
            combo.BonusEffects = new List<CardEffect>
            {
                new CardEffect { EffectType = effect, Power = power }
            };
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
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(null, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyIncomingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(new List<CardTag>(), _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NoExistingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire, CardTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_AllTagsFromIncoming_NoneExisting_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire, CardTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_MatchingTags_ReturnsCombo()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ReversedTagOrder_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Fire }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Oil }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_PartialMatch_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil, CardTag.Wind });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ThreeTagCombo_AllPresent_Triggers()
        {
            var combo = CreateCombo("Storm", new List<CardTag> { CardTag.Fire, CardTag.Oil, CardTag.Wind });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil, CardTag.Wind }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Storm", result.ComboName);
        }

        [Test]
        public void DetectCombo_MultipleCombos_ReturnsFirst()
        {
            var ignite = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var freeze = CreateCombo("Freeze", new List<CardTag> { CardTag.Ice, CardTag.Water });
            var detector = new ComboDetector(new List<CardComboSO> { ignite, freeze });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_WrongComboTags_ReturnsNull()
        {
            var ignite = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { ignite });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Ice }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Water }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyCombos_ReturnsNull()
        {
            var detector = new ComboDetector(new List<CardComboSO>());

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Fire }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NullCombos_ReturnsNull()
        {
            var detector = new ComboDetector(null);

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Fire }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ComboWithEmptyRequiredTags_Skipped()
        {
            var broken = CreateCombo("Broken", new List<CardTag>());
            var ignite = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { broken, ignite });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ExtraTags_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Oil, CardTag.Poison }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire, CardTag.Ice }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_RequiresMixOfExistingAndIncoming()
        {
            var combo = CreateCombo("Ignite", new List<CardTag> { CardTag.Fire, CardTag.Oil });
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<CardTag> { CardTag.Poison }, 3);

            var result = detector.DetectCombo(new List<CardTag> { CardTag.Fire, CardTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }
    }
}
