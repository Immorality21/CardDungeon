using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The physical/magic defence split and Luck dodging (<see cref="DefenseRules"/>): Spirit meets
    /// Intelligence- and Spirit-scaled damage, Endurance meets the rest, and a dodge makes a whole
    /// physical ability miss its target while magic always lands.
    /// </summary>
    public class DefenseRulesTests
    {
        private EffectResolver _resolver;
        private CombatBuffTracker _buffTracker;
        private MockCombatUnit _caster;
        private MockCombatUnit _target;

        [SetUp]
        public void SetUp()
        {
            _resolver = new EffectResolver();
            _buffTracker = new CombatBuffTracker();
            _caster = new MockCombatUnit("Caster", strength: 10, endurance: 0, health: 50);
            _caster.EffectiveOverrides[StatType.Intelligence] = 10;
            // A wall against physical damage and nothing against magic.
            _target = new MockCombatUnit("Target", strength: 5, endurance: 20, health: 100, isHero: false);
            _target.EffectiveOverrides[StatType.Spirit] = 0;
        }

        [TearDown]
        public void TearDown()
        {
            DefenseRules.Roll = () => Random.Range(0f, 1f);
        }

        private static MagicSO Ability(StatType scaling, int power, params SpellEffect[] extra)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test-" + scaling;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = power, ScalingStat = scaling }
            };
            magic.Effects.AddRange(extra);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        private EffectResult Cast(MagicSO magic)
        {
            var action = new SpellcastAction
            {
                Magic = magic,
                Caster = _caster,
                Targets = new List<ICombatUnit> { _target }
            };
            return _resolver.Execute(action, _buffTracker);
        }

        [TestCase(StatType.Intelligence, true)]
        [TestCase(StatType.Spirit, true)]
        [TestCase(StatType.Strength, false)]
        [TestCase(StatType.Agility, false)]
        [TestCase(StatType.None, false)]
        public void IsMagic_ByScalingStat(StatType stat, bool magic)
        {
            Assert.AreEqual(magic, DefenseRules.IsMagic(stat));
            Assert.AreEqual(magic ? StatType.Spirit : StatType.Endurance, DefenseRules.DefenseStatFor(stat));
        }

        [Test]
        public void DodgeChance_NoLuck_IsZero_AndFollowsTheCurve()
        {
            Assert.AreEqual(0f, DefenseRules.DodgeChanceFor(_target));
            _target.EffectiveOverrides[StatType.Luck] = 20;
            Assert.AreEqual(DefenseRules.MaxLuckDodge / 2f, DefenseRules.DodgeChanceFor(_target), 1e-5f);
        }

        [Test]
        public void MagicDamage_IsReducedBySpirit_NotEndurance()
        {
            Cast(Ability(StatType.Intelligence, 10));
            // 10 power + 10 INT, no Spirit: the full 20 lands despite 20 Endurance.
            Assert.AreEqual(80, _target.Stats.Health);
        }

        [Test]
        public void PhysicalDamage_IsReducedByEndurance()
        {
            Cast(Ability(StatType.Strength, 10));
            // 20 raw against 20 Endurance halves it.
            Assert.AreEqual(90, _target.Stats.Health);
        }

        [Test]
        public void MagicDamage_SpiritReducesIt()
        {
            _target.EffectiveOverrides[StatType.Spirit] = 20;
            Cast(Ability(StatType.Intelligence, 10));
            Assert.AreEqual(90, _target.Stats.Health);
        }

        [Test]
        public void PhysicalAbility_Dodged_MissesEntirely_DebuffIncluded()
        {
            _target.EffectiveOverrides[StatType.Luck] = 20;
            DefenseRules.Roll = () => 0f;
            var debuff = new SpellEffect
            {
                EffectType = SpellEffectType.Debuff, Power = 3, BuffType = BuffType.Endurance, Duration = 2
            };

            var result = Cast(Ability(StatType.Strength, 10, debuff));

            Assert.AreEqual(100, _target.Stats.Health);
            Assert.AreEqual(0, _buffTracker.GetBuffAmount(_target, StatType.Endurance));
            Assert.IsTrue(result.Entries.Any(e => e.Target == _target && e.Text == "Dodge"));
        }

        [Test]
        public void MagicAbility_CannotBeDodged()
        {
            _target.EffectiveOverrides[StatType.Luck] = 20;
            DefenseRules.Roll = () => 0f;

            Cast(Ability(StatType.Intelligence, 10));

            Assert.AreEqual(80, _target.Stats.Health);
        }

        [Test]
        public void PhysicalAbility_NoLuck_NeverDodges()
        {
            DefenseRules.Roll = () => 0f;
            Cast(Ability(StatType.Strength, 10));
            Assert.AreEqual(90, _target.Stats.Health);
        }
    }
}
