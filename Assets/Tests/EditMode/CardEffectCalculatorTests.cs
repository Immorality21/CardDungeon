using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CardEffectCalculatorTests
    {
        private CardEffectCalculator _calculator;
        private CombatBuffTracker _buffTracker;
        private CardTagTracker _tagTracker;
        private MockCombatUnit _hero;
        private MockCombatUnit _enemy;

        private CardSO CreateCard(
            string key,
            CardEffectType effect,
            int power,
            List<string> tags = null,
            int tagDuration = 3)
        {
            var card = ScriptableObject.CreateInstance<CardSO>();
            card.Key = key;
            card.DisplayName = key;
            card.EffectType = effect;
            card.Power = power;
            card.Tags = tags ?? new List<string>();
            card.TagDuration = tagDuration;
            return card;
        }

        private CardComboSO CreateCombo(
            string name,
            List<string> requiredTags,
            CardEffectType effect,
            int power)
        {
            var combo = ScriptableObject.CreateInstance<CardComboSO>();
            combo.ComboName = name;
            combo.RequiredTags = requiredTags;
            combo.BonusEffect = effect;
            combo.BonusPower = power;
            return combo;
        }

        private CardAction MakeAction(CardSO card, MockCombatUnit caster, params MockCombatUnit[] targets)
        {
            return new CardAction
            {
                Card = card,
                Caster = caster,
                Targets = new List<Assets.Scripts.Combat.ICombatUnit>(targets)
            };
        }

        [SetUp]
        public void SetUp()
        {
            _calculator = new CardEffectCalculator();
            _buffTracker = new CombatBuffTracker();
            _tagTracker = new CardTagTracker();
            _hero = new MockCombatUnit("Hero", attack: 10, defense: 5, health: 100);
            _enemy = new MockCombatUnit("Goblin", attack: 6, defense: 3, health: 50, isHero: false);
        }

        // ---- Damage ----

        [Test]
        public void Damage_ReducesTargetHealth()
        {
            var card = CreateCard("Slash", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            // damage = Max(1, 10 + 5 - 3) = 12
            Assert.AreEqual(50 - 12, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_MinimumOneDamage()
        {
            // Enemy has massive defense
            var tank = new MockCombatUnit("Tank", attack: 1, defense: 999, health: 100, isHero: false);
            var card = CreateCard("Poke", CardEffectType.Damage, power: 0);
            var action = MakeAction(card, _hero, tank);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100 - 1, tank.Stats.Health);
        }

        [Test]
        public void Damage_IncludesAttackBuff()
        {
            _buffTracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            var card = CreateCard("Slash", CardEffectType.Damage, power: 0);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            // damage = Max(1, (10+5) + 0 - 3) = 12
            Assert.AreEqual(50 - 12, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_IncludesDefenseBuff()
        {
            _buffTracker.ApplyBuff(_enemy, StatType.Defense, 10, 3);
            var card = CreateCard("Slash", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            // damage = Max(1, 10 + 5 - (3+10)) = Max(1, 2) = 2
            Assert.AreEqual(50 - 2, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_MultipleTargets()
        {
            var enemy2 = new MockCombatUnit("Orc", attack: 4, defense: 2, health: 40, isHero: false);
            var card = CreateCard("Cleave", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy, enemy2);

            _calculator.Execute(action, _buffTracker);

            // Goblin: Max(1, 10+5-3) = 12
            Assert.AreEqual(50 - 12, _enemy.Stats.Health);
            // Orc: Max(1, 10+5-2) = 13
            Assert.AreEqual(40 - 13, enemy2.Stats.Health);
        }

        [Test]
        public void Damage_SkipsDeadTargets()
        {
            _enemy.Stats.Health = 0;
            var card = CreateCard("Slash", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(0, _enemy.Stats.Health);
            Assert.AreEqual(0, result.Entries.Count);
        }

        // ---- Heal ----

        [Test]
        public void Heal_IncreasesHealth()
        {
            _hero.Stats.Health = 60;
            var card = CreateCard("Heal", CardEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(80, _hero.Stats.Health);
        }

        [Test]
        public void Heal_ClampsToMaxHealth()
        {
            _hero.Stats.Health = 95;
            var card = CreateCard("Heal", CardEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100, _hero.Stats.Health);
        }

        [Test]
        public void Heal_AtFullHealth_HealsZero()
        {
            var card = CreateCard("Heal", CardEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100, _hero.Stats.Health);
            Assert.AreEqual("0", result.Entries[0].Text);
        }

        [Test]
        public void Heal_SkipsDeadTargets()
        {
            _hero.Stats.Health = 0;
            var card = CreateCard("Heal", CardEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(0, _hero.Stats.Health);
            Assert.AreEqual(0, result.Entries.Count);
        }

        // ---- Buff ----

        [Test]
        public void BuffAttack_AppliesBuff()
        {
            var card = CreateCard("WarCry", CardEffectType.BuffAttack, power: 7);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(7, _buffTracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void BuffDefense_AppliesBuff()
        {
            var card = CreateCard("ShieldUp", CardEffectType.BuffDefense, power: 4);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(4, _buffTracker.GetBuffAmount(_hero, StatType.Defense));
        }

        [Test]
        public void Buff_GeneratesCorrectEntryText()
        {
            var card = CreateCard("WarCry", CardEffectType.BuffAttack, power: 7);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(1, result.Entries.Count);
            Assert.AreEqual("+7 Attack", result.Entries[0].Text);
        }

        // ---- Debuff ----

        [Test]
        public void Debuff_ReducesBothStats()
        {
            var card = CreateCard("Curse", CardEffectType.Debuff, power: 3);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Attack));
            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        [Test]
        public void Debuff_GeneratesCorrectEntryText()
        {
            var card = CreateCard("Curse", CardEffectType.Debuff, power: 3);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(1, result.Entries.Count);
            Assert.AreEqual("-3 Stats", result.Entries[0].Text);
        }

        // ---- Combos ----

        [Test]
        public void Combo_DamageBonus_Applied()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 15);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            // Pre-apply Oil tag
            _tagTracker.ApplyTags(_enemy, new List<string> { "Oil" }, 3);

            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            int healthBefore = _enemy.Stats.Health;
            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            // Card damage: Max(1, 10+5-3) = 12
            // Combo damage: 15
            // Total: 27
            Assert.AreEqual(healthBefore - 27, _enemy.Stats.Health);
        }

        [Test]
        public void Combo_HealBonus_HealsCaster()
        {
            var combo = CreateCombo("LifeDrain", new List<string> { "Dark", "Blood" }, CardEffectType.Heal, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Blood" }, 3);

            _hero.Stats.Health = 70;
            var card = CreateCard("DarkBolt", CardEffectType.Damage, power: 5, tags: new List<string> { "Dark" });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(80, _hero.Stats.Health);
        }

        [Test]
        public void Combo_HealBonus_ClampsToMaxHealth()
        {
            var combo = CreateCombo("LifeDrain", new List<string> { "Dark", "Blood" }, CardEffectType.Heal, 50);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Blood" }, 3);

            _hero.Stats.Health = 90;
            var card = CreateCard("DarkBolt", CardEffectType.Damage, power: 5, tags: new List<string> { "Dark" });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(100, _hero.Stats.Health);
        }

        [Test]
        public void Combo_BuffAttackBonus_BuffsCaster()
        {
            var combo = CreateCombo("Empower", new List<string> { "Fire", "Wind" }, CardEffectType.BuffAttack, 8);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Wind" }, 3);

            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(8, _buffTracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void Combo_DebuffBonus_DebuffsTarget()
        {
            var combo = CreateCombo("Weaken", new List<string> { "Ice", "Water" }, CardEffectType.Debuff, 5);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Water" }, 3);

            var card = CreateCard("IceShard", CardEffectType.Damage, power: 3, tags: new List<string> { "Ice" });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(-5, _buffTracker.GetBuffAmount(_enemy, StatType.Attack));
            Assert.AreEqual(-5, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        [Test]
        public void Combo_SetsComboNameInResult()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Oil" }, 3);

            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void Combo_TagsAppliedAfterDetection()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            // No existing tags — card has Fire, but combo needs Oil too
            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            // No combo should trigger
            Assert.IsNull(result.ComboName);
            // But Fire tag should now be on the enemy
            Assert.IsTrue(_tagTracker.GetTagsOnUnit(_enemy).Contains("Fire"));
        }

        [Test]
        public void Combo_SkipsDeadTargets()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Oil" }, 3);

            // Card will kill the enemy (massive power)
            var card = CreateCard("Nuke", CardEffectType.Damage, power: 999, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            // Enemy died from damage, so combo should not trigger
            Assert.IsNull(result.ComboName);
        }

        // ---- No combo when tag tracker/detector not provided ----

        [Test]
        public void Execute_WithoutTagTracker_SkipsCombos()
        {
            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.IsNull(result.ComboName);
            // Only the damage entry
            Assert.AreEqual(1, result.Entries.Count);
        }

        // ---- Result / Log ----

        [Test]
        public void BuildLog_BasicFormat()
        {
            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual("Hero plays Fireball!", result.BuildLog(action));
        }

        [Test]
        public void BuildLog_IncludesComboName()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Oil" }, 3);

            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual("Hero plays Fireball! COMBO: Ignite!", result.BuildLog(action));
        }

        // ---- Entry count ----

        [Test]
        public void Result_HasCorrectEntryCount_DamageWithCombo()
        {
            var combo = CreateCombo("Ignite", new List<string> { "Fire", "Oil" }, CardEffectType.Damage, 10);
            var detector = new ComboDetector(new List<CardComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<string> { "Oil" }, 3);

            var card = CreateCard("Fireball", CardEffectType.Damage, power: 5, tags: new List<string> { "Fire" });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            // 1 damage entry + 1 combo name entry + 1 combo damage entry = 3
            Assert.AreEqual(3, result.Entries.Count);
        }
    }
}
