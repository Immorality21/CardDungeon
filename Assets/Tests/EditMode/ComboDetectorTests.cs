using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ComboDetectorTests
    {
        private MagicTagTracker _tagTracker;
        private MockCombatUnit _target;

        private MagicComboSO CreateCombo(string name, List<MagicTag> requiredTags,
            SpellEffectType effect = SpellEffectType.Damage, int power = 10)
        {
            var combo = ScriptableObject.CreateInstance<MagicComboSO>();
            combo.ComboName = name;
            combo.RequiredTags = requiredTags;
            combo.BonusEffects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = effect, Power = power }
            };
            return combo;
        }

        [SetUp]
        public void SetUp()
        {
            _tagTracker = new MagicTagTracker();
            _target = new MockCombatUnit("Enemy", strength: 5, endurance: 3, health: 50, isHero: false);
        }

        [Test]
        public void DetectCombo_NoIncomingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(null, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyIncomingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(new List<MagicTag>(), _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NoExistingTags_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_AllTagsFromIncoming_NoneExisting_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_MatchingTags_ReturnsCombo()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ReversedTagOrder_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Fire }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_PartialMatch_ReturnsNull()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil, MagicTag.Wind });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ThreeTagCombo_AllPresent_Triggers()
        {
            var combo = CreateCombo("Storm", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil, MagicTag.Wind });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil, MagicTag.Wind }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Storm", result.ComboName);
        }

        [Test]
        public void DetectCombo_MultipleCombos_ReturnsFirst()
        {
            var ignite = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var freeze = CreateCombo("Freeze", new List<MagicTag> { MagicTag.Ice, MagicTag.Water });
            var detector = new ComboDetector(new List<MagicComboSO> { ignite, freeze });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_WrongComboTags_ReturnsNull()
        {
            var ignite = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { ignite });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Ice }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Water }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_EmptyCombos_ReturnsNull()
        {
            var detector = new ComboDetector(new List<MagicComboSO>());

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Fire }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_NullCombos_ReturnsNull()
        {
            var detector = new ComboDetector(null);

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Fire }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectCombo_ComboWithEmptyRequiredTags_Skipped()
        {
            var broken = CreateCombo("Broken", new List<MagicTag>());
            var ignite = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { broken, ignite });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_ExtraTags_StillTriggers()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Oil, MagicTag.Poison }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire, MagicTag.Ice }, _target, _tagTracker);

            Assert.IsNotNull(result);
            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void DetectCombo_RequiresMixOfExistingAndIncoming()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil });
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_target, new List<MagicTag> { MagicTag.Poison }, 3);

            var result = detector.DetectCombo(new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, _target, _tagTracker);

            Assert.IsNull(result);
        }
    }
}
