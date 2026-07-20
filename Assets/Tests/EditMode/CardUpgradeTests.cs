using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CardUpgradeTests
    {
        private CardEffectCalculator _calculator;
        private CombatBuffTracker _buffTracker;
        private MockCombatUnit _hero;
        private MockCombatUnit _enemy;

        [SetUp]
        public void SetUp()
        {
            _calculator = new CardEffectCalculator();
            _buffTracker = new CombatBuffTracker();
            _hero = new MockCombatUnit("Hero", attack: 10, defense: 5, health: 100);
            _enemy = new MockCombatUnit("Goblin", attack: 6, defense: 3, health: 50, isHero: false);
        }

        private CardSO CreateCard(string key, CardEffectType effect, int power,
            DamageType damageType = DamageType.Normal, BuffType buffType = BuffType.Attack)
        {
            var card = ScriptableObject.CreateInstance<CardSO>();
            card.Key = key;
            card.DisplayName = key;
            card.Effects = new List<CardEffect>
            {
                new CardEffect
                {
                    EffectType = effect,
                    Power = power,
                    DamageType = damageType,
                    BuffType = buffType,
                    Duration = 3
                }
            };
            card.Tags = new List<CardTag>();
            return card;
        }

        private CardAction MakeAction(CardSO card, MockCombatUnit caster, params MockCombatUnit[] targets)
        {
            return new CardAction
            {
                Card = card,
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
            var card = CreateCard("Slash", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 4);

            // attack 10 + card power 5 + upgrade bonus 4 = 19 raw
            int expected = ExpectedDamage(10 + 5 + 4, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void PowerBonus_Zero_MatchesUnboostedDamage()
        {
            var card = CreateCard("Slash", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 0);

            int expected = ExpectedDamage(10 + 5, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void PowerBonus_IncreasesHeal()
        {
            _hero.Stats.Health = 50;
            var card = CreateCard("Heal", CardEffectType.Heal, power: 10);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker, powerBonus: 6);

            // 50 + (10 + 6) = 66
            Assert.AreEqual(66, _hero.Stats.Health);
        }

        [Test]
        public void PowerBonus_DoesNotAffectBuffPower()
        {
            var card = CreateCard("WarCry", CardEffectType.Buff, power: 7, buffType: BuffType.Attack);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker, powerBonus: 5);

            // Buff amount must remain the card's declared power, unaffected by the upgrade bonus.
            Assert.AreEqual(7, _buffTracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void PowerBonus_DoesNotAffectDebuffPower()
        {
            var card = CreateCard("Curse", CardEffectType.Debuff, power: 3, buffType: BuffType.Defense);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, powerBonus: 5);

            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        // ---- Economy math (pure helpers) ----

        [Test]
        public void CardPowerBonusForLevel_ScalesLinearly()
        {
            Assert.AreEqual(0, MetaProgressManager.CardPowerBonusForLevel(0));
            Assert.AreEqual(MetaProgressManager.PowerPerUpgradeLevel, MetaProgressManager.CardPowerBonusForLevel(1));
            Assert.AreEqual(3 * MetaProgressManager.PowerPerUpgradeLevel, MetaProgressManager.CardPowerBonusForLevel(3));
        }

        [Test]
        public void CardPowerBonusForLevel_NegativeClampsToZero()
        {
            Assert.AreEqual(0, MetaProgressManager.CardPowerBonusForLevel(-2));
        }

        [Test]
        public void CardUpgradeCost_IncreasesWithLevel()
        {
            int cost0 = MetaProgressManager.CardUpgradeCostForNextLevel(0);
            int cost1 = MetaProgressManager.CardUpgradeCostForNextLevel(1);
            int cost2 = MetaProgressManager.CardUpgradeCostForNextLevel(2);

            Assert.Greater(cost1, cost0);
            Assert.Greater(cost2, cost1);
            // Constant increment between levels
            Assert.AreEqual(cost1 - cost0, cost2 - cost1);
        }
    }
}
