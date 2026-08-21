using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class MagicUpgradeTests
    {
        private EffectResolver _calculator;
        private CombatBuffTracker _buffTracker;
        private MockCombatUnit _hero;
        private MockCombatUnit _enemy;

        [SetUp]
        public void SetUp()
        {
            _calculator = new EffectResolver();
            _buffTracker = new CombatBuffTracker();
            _hero = new MockCombatUnit("Hero", strength: 10, endurance: 5, health: 100);
            _enemy = new MockCombatUnit("Goblin", strength: 6, endurance: 3, health: 50, isHero: false);
        }

        private MagicSO CreateCard(string key, SpellEffectType effect, int power,
            DamageType damageType = DamageType.Normal, BuffType buffType = BuffType.Strength,
            StatType scalingStat = StatType.None)
        {
            var card = ScriptableObject.CreateInstance<MagicSO>();
            card.Key = key;
            card.DisplayName = key;
            card.Effects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = effect,
                    Power = power,
                    // Damage scaling is opt-in per effect. These tests assert "the hero's Strength
                    // plus the card's power" for damage and bare power for heals/buffs, so that is
                    // the default here; an explicit scalingStat still wins.
                    ScalingStat = scalingStat != StatType.None
                        ? scalingStat
                        : (effect == SpellEffectType.Damage ? StatType.Strength : StatType.None),
                    DamageType = damageType,
                    BuffType = buffType,
                    Duration = 3
                }
            };
            card.Tags = new List<MagicTag>();
            return card;
        }

        private SpellcastAction MakeAction(MagicSO card, MockCombatUnit caster, params MockCombatUnit[] targets)
        {
            return new SpellcastAction
            {
                Magic = card,
                Caster = caster,
                Targets = new List<ICombatUnit>(targets)
            };
        }

        private int ExpectedDamage(int rawDamage, int defense, DamageType type = DamageType.Normal)
        {
            return DamageCalculator.Calculate(rawDamage, defense, type, null);
        }

        // ---- Power bonus applied to damage ----

        [Test]
        public void PowerBonus_IncreasesDamage()
        {
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 4);

            // attack 10 + card power 5 + upgrade bonus 4 = 19 raw
            int expected = ExpectedDamage(10 + 5 + 4, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void PowerBonus_Zero_MatchesUnboostedDamage()
        {
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 0);

            int expected = ExpectedDamage(10 + 5, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void PowerBonus_IncreasesHeal()
        {
            // Health, not MaxHealth: the heal clamps to the bar, so shrinking the bar under a
            // full one heals nothing and lands the hero on 50.
            _hero.Stats.Health = 50;
            var card = CreateCard("Heal", SpellEffectType.Heal, power: 10);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker, powerBonus: 6);

            // 50 + (10 + 6) = 66
            Assert.AreEqual(66, _hero.Stats.Health);
        }

        [Test]
        public void PowerBonus_DoesNotAffectBuffPower()
        {
            var card = CreateCard("WarCry", SpellEffectType.Buff, power: 7, buffType: BuffType.Strength);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker, powerBonus: 5);

            // Buff amount must remain the card's declared power, unaffected by the upgrade bonus.
            Assert.AreEqual(7, _buffTracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void PowerBonus_DoesNotAffectDebuffPower()
        {
            var card = CreateCard("Curse", SpellEffectType.Debuff, power: 3, buffType: BuffType.Endurance);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 5);

            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Endurance));
        }

        /// <summary>
        /// The upgrade bonus is folded in by <b>copying</b> the effect, and the copy has to carry
        /// every other field. <c>ScalingStat</c> was the one that got dropped, and it defaults to
        /// <see cref="StatType.None"/> - so any upgrade level at all silently removed the caster's
        /// contribution. An upgraded caster spell hit for *less* than the same spell unupgraded, and
        /// still produced a plausible number, so nothing looked wrong.
        /// </summary>
        [Test]
        public void PowerBonus_PreservesScalingStat()
        {
            _hero.Stats[StatType.Intelligence] = 12;
            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5,
                scalingStat: StatType.Intelligence);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 4);

            int expected = ExpectedDamage(5 + 4 + 12, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health,
                "An upgraded magic must still scale off its caster.");
        }

        [Test]
        public void PowerBonus_ScalingStat_UpgradeNeverMakesASpellWeaker()
        {
            _hero.Stats[StatType.Intelligence] = 12;

            var plain = CreateCard("Fireball", SpellEffectType.Damage, power: 5,
                scalingStat: StatType.Intelligence);
            _calculator.Execute(MakeAction(plain, _hero, _enemy), _buffTracker, powerBonus: 0);
            int unupgraded = 50 - _enemy.Stats.Health;

            _enemy.Stats.Health = 50;
            var upgraded = CreateCard("Fireball", SpellEffectType.Damage, power: 5,
                scalingStat: StatType.Intelligence);
            _calculator.Execute(MakeAction(upgraded, _hero, _enemy), _buffTracker, powerBonus: 4);
            int withUpgrade = 50 - _enemy.Stats.Health;

            Assert.Greater(withUpgrade, unupgraded,
                "This is the shape of the bug: spending Essence on a caster's spell used to cost "
                + "more damage than the upgrade added.");
        }

        // ---- Economy math (pure helpers) ----

        [Test]
        public void MagicPowerBonusForLevel_ScalesLinearly()
        {
            Assert.AreEqual(0, MetaProgressManager.MagicPowerBonusForLevel(0));
            Assert.AreEqual(MetaProgressManager.PowerPerUpgradeLevel, MetaProgressManager.MagicPowerBonusForLevel(1));
            Assert.AreEqual(3 * MetaProgressManager.PowerPerUpgradeLevel, MetaProgressManager.MagicPowerBonusForLevel(3));
        }

        [Test]
        public void MagicPowerBonusForLevel_NegativeClampsToZero()
        {
            Assert.AreEqual(0, MetaProgressManager.MagicPowerBonusForLevel(-2));
        }

        [Test]
        public void MagicUpgradeCost_IncreasesWithLevel()
        {
            int cost0 = MetaProgressManager.MagicUpgradeCostForNextLevel(0);
            int cost1 = MetaProgressManager.MagicUpgradeCostForNextLevel(1);
            int cost2 = MetaProgressManager.MagicUpgradeCostForNextLevel(2);

            Assert.Greater(cost1, cost0);
            Assert.Greater(cost2, cost1);
            // Constant increment between levels
            Assert.AreEqual(cost1 - cost0, cost2 - cost1);
        }
    }
}
