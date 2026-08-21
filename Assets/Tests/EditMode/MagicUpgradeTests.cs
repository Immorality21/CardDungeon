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
            DamageType damageType = DamageType.Normal, BuffType buffType = BuffType.Strength)
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
            _hero.Stats[StatType.MaxHealth] = 50;
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
