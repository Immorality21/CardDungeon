using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class EffectResolverTests
    {
        private EffectResolver _calculator;
        private CombatBuffTracker _buffTracker;
        private MagicTagTracker _tagTracker;
        private MockCombatUnit _hero;
        private MockCombatUnit _enemy;

        private MagicSO CreateCard(
            string key,
            SpellEffectType effect,
            int power,
            DamageType damageType = DamageType.Normal,
            BuffType buffType = BuffType.Attack,
            int duration = 3,
            List<MagicTag> tags = null,
            int tagDuration = 3)
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
                    Duration = duration
                }
            };
            card.Tags = tags ?? new List<MagicTag>();
            card.TagDuration = tagDuration;
            return card;
        }

        private MagicSO CreateMultiEffectCard(
            string key,
            List<SpellEffect> effects,
            List<MagicTag> tags = null,
            int tagDuration = 3)
        {
            var card = ScriptableObject.CreateInstance<MagicSO>();
            card.Key = key;
            card.DisplayName = key;
            card.Effects = effects;
            card.Tags = tags ?? new List<MagicTag>();
            card.TagDuration = tagDuration;
            return card;
        }

        private MagicComboSO CreateCombo(
            string name,
            List<MagicTag> requiredTags,
            SpellEffectType effect,
            int power,
            DamageType damageType = DamageType.Normal,
            BuffType buffType = BuffType.Attack,
            int duration = 3)
        {
            var combo = ScriptableObject.CreateInstance<MagicComboSO>();
            combo.ComboName = name;
            combo.RequiredTags = requiredTags;
            combo.BonusEffects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = effect,
                    Power = power,
                    DamageType = damageType,
                    BuffType = buffType,
                    Duration = duration
                }
            };
            return combo;
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

        [SetUp]
        public void SetUp()
        {
            _calculator = new EffectResolver();
            _buffTracker = new CombatBuffTracker();
            _tagTracker = new MagicTagTracker();
            _hero = new MockCombatUnit("Hero", attack: 10, defense: 5, health: 100);
            _enemy = new MockCombatUnit("Goblin", attack: 6, defense: 3, health: 50, isHero: false);
        }

        // ---- Damage ----

        private int ExpectedDamage(int rawDamage, int defense, DamageType type = DamageType.Normal,
            List<Resistance> resistances = null)
        {
            return DamageCalculator.Calculate(rawDamage, defense, type, resistances);
        }

        [Test]
        public void Damage_ReducesTargetHealth()
        {
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            int expected = ExpectedDamage(10 + 5, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_MinimumOneDamage()
        {
            var tank = new MockCombatUnit("Tank", attack: 1, defense: 999, health: 100, isHero: false);
            var card = CreateCard("Poke", SpellEffectType.Damage, power: 0);
            var action = MakeAction(card, _hero, tank);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100 - 1, tank.Stats.Health);
        }

        [Test]
        public void Damage_IncludesAttackBuff()
        {
            _buffTracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 0);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            int expected = ExpectedDamage(10 + 5, 3);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_IncludesDefenseBuff()
        {
            _buffTracker.ApplyBuff(_enemy, StatType.Defense, 10, 3);
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            int expected = ExpectedDamage(10 + 5, 3 + 10);
            Assert.AreEqual(50 - expected, _enemy.Stats.Health);
        }

        [Test]
        public void Damage_MultipleTargets()
        {
            var enemy2 = new MockCombatUnit("Orc", attack: 4, defense: 2, health: 40, isHero: false);
            var card = CreateCard("Cleave", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy, enemy2);

            _calculator.Execute(action, _buffTracker);

            int goblinDmg = ExpectedDamage(10 + 5, 3);
            int orcDmg = ExpectedDamage(10 + 5, 2);
            Assert.AreEqual(50 - goblinDmg, _enemy.Stats.Health);
            Assert.AreEqual(40 - orcDmg, enemy2.Stats.Health);
        }

        [Test]
        public void Damage_SkipsDeadTargets()
        {
            _enemy.Stats.Health = 0;
            var card = CreateCard("Slash", SpellEffectType.Damage, power: 5);
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
            var card = CreateCard("Heal", SpellEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(80, _hero.Stats.Health);
        }

        [Test]
        public void Heal_ClampsToMaxHealth()
        {
            _hero.Stats.Health = 95;
            var card = CreateCard("Heal", SpellEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100, _hero.Stats.Health);
        }

        [Test]
        public void Heal_AtFullHealth_HealsZero()
        {
            var card = CreateCard("Heal", SpellEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(100, _hero.Stats.Health);
            Assert.AreEqual("0", result.Entries[0].Text);
        }

        [Test]
        public void Heal_SkipsDeadTargets()
        {
            _hero.Stats.Health = 0;
            var card = CreateCard("Heal", SpellEffectType.Heal, power: 20);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(0, _hero.Stats.Health);
            Assert.AreEqual(0, result.Entries.Count);
        }

        // ---- Buff ----

        [Test]
        public void BuffAttack_AppliesBuff()
        {
            var card = CreateCard("WarCry", SpellEffectType.Buff, power: 7, buffType: BuffType.Attack);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(7, _buffTracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void BuffDefense_AppliesBuff()
        {
            var card = CreateCard("ShieldUp", SpellEffectType.Buff, power: 4, buffType: BuffType.Defense);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(4, _buffTracker.GetBuffAmount(_hero, StatType.Defense));
        }

        [Test]
        public void Buff_GeneratesCorrectEntryText()
        {
            var card = CreateCard("WarCry", SpellEffectType.Buff, power: 7, buffType: BuffType.Attack);
            var action = MakeAction(card, _hero, _hero);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(1, result.Entries.Count);
            Assert.AreEqual("+7 Attack", result.Entries[0].Text);
        }

        // ---- Debuff ----

        [Test]
        public void Debuff_ReducesStat()
        {
            var card = CreateCard("Curse", SpellEffectType.Debuff, power: 3, buffType: BuffType.Attack);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void Debuff_GeneratesCorrectEntryText()
        {
            var card = CreateCard("Curse", SpellEffectType.Debuff, power: 3, buffType: BuffType.Attack);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(1, result.Entries.Count);
            Assert.AreEqual("-3 Attack", result.Entries[0].Text);
        }

        // ---- Multi-Effect ----

        [Test]
        public void MultiEffect_DamageAndDebuff_BothApply()
        {
            var card = CreateMultiEffectCard("LightningBolt", new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 5, DamageType = DamageType.Lightning },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Defense, Duration = 3 }
            });
            var action = MakeAction(card, _hero, _enemy);

            int healthBefore = _enemy.Stats.Health;
            _calculator.Execute(action, _buffTracker);

            int expectedDmg = ExpectedDamage(10 + 5, 3, DamageType.Lightning);
            Assert.AreEqual(healthBefore - expectedDmg, _enemy.Stats.Health);
            Assert.AreEqual(-2, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        [Test]
        public void MultiEffect_EffectsExecuteInOrder()
        {
            // Debuff defense first, then damage — but damage should NOT benefit from
            // the debuff applied in the same card (debuff hasn't been factored into the
            // defense query yet because it's applied via buff tracker in the same action)
            var card = CreateMultiEffectCard("Combo", new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 3, BuffType = BuffType.Defense, Duration = 3 },
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 5, DamageType = DamageType.Normal }
            });
            var action = MakeAction(card, _hero, _enemy);

            int healthBefore = _enemy.Stats.Health;
            _calculator.Execute(action, _buffTracker);

            // Defense debuff IS already applied when damage calculates (same buff tracker)
            int expectedDmg = ExpectedDamage(10 + 5, 3 + (-3));
            Assert.AreEqual(healthBefore - expectedDmg, _enemy.Stats.Health);
            Assert.AreEqual(-3, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        [Test]
        public void MultiEffect_DebuffTwoStats()
        {
            // Old-style debuff that hits both Attack and Defense
            var card = CreateMultiEffectCard("OilSlick", new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Attack, Duration = 3 },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Defense, Duration = 3 }
            });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(-2, _buffTracker.GetBuffAmount(_enemy, StatType.Attack));
            Assert.AreEqual(-2, _buffTracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        [Test]
        public void MultiEffect_BuffAgility()
        {
            var card = CreateCard("Haste", SpellEffectType.Buff, power: 3, buffType: BuffType.Agility);
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker);

            Assert.AreEqual(3, _buffTracker.GetBuffAmount(_hero, StatType.Agility));
        }

        // ---- Combos ----

        [Test]
        public void Combo_DamageBonus_Applied()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 15);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            int healthBefore = _enemy.Stats.Health;
            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            int cardDmg = ExpectedDamage(10 + 5, 3);
            int comboDmg = ExpectedDamage(15, 3);
            Assert.AreEqual(healthBefore - cardDmg - comboDmg, _enemy.Stats.Health);
        }

        [Test]
        public void Combo_HealBonus_HealsCaster()
        {
            var combo = CreateCombo("LifeDrain", new List<MagicTag> { MagicTag.Dark, MagicTag.Blood }, SpellEffectType.Heal, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Blood }, 3);

            _hero.Stats.Health = 70;
            var card = CreateCard("DarkBolt", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Dark });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(80, _hero.Stats.Health);
        }

        [Test]
        public void Combo_HealBonus_ClampsToMaxHealth()
        {
            var combo = CreateCombo("LifeDrain", new List<MagicTag> { MagicTag.Dark, MagicTag.Blood }, SpellEffectType.Heal, 50);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Blood }, 3);

            _hero.Stats.Health = 90;
            var card = CreateCard("DarkBolt", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Dark });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(100, _hero.Stats.Health);
        }

        [Test]
        public void Combo_BuffAttackBonus_BuffsCaster()
        {
            var combo = CreateCombo("Empower", new List<MagicTag> { MagicTag.Fire, MagicTag.Wind }, SpellEffectType.Buff, 8, buffType: BuffType.Attack);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Wind }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(8, _buffTracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void Combo_DebuffBonus_DebuffsTarget()
        {
            var combo = CreateCombo("Weaken", new List<MagicTag> { MagicTag.Ice, MagicTag.Water }, SpellEffectType.Debuff, 5, buffType: BuffType.Attack);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Water }, 3);

            var card = CreateCard("IceShard", SpellEffectType.Damage, power: 3, tags: new List<MagicTag> { MagicTag.Ice });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(-5, _buffTracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void Combo_SetsComboNameInResult()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual("Ignite", result.ComboName);
        }

        [Test]
        public void Combo_TagsAppliedAfterDetection()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.IsNull(result.ComboName);
            Assert.IsTrue(_tagTracker.GetTagsOnUnit(_enemy).Contains(MagicTag.Fire));
        }

        [Test]
        public void Combo_SkipsDeadTargets()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Nuke", SpellEffectType.Damage, power: 999, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.IsNull(result.ComboName);
        }

        // ---- No combo when tag tracker/detector not provided ----

        [Test]
        public void Execute_WithoutTagTracker_SkipsCombos()
        {
            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.IsNull(result.ComboName);
            Assert.AreEqual(1, result.Entries.Count);
        }

        // ---- Result / Log ----

        [Test]
        public void BuildLog_BasicFormat()
        {
            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker);

            Assert.AreEqual("Hero plays Fireball!", result.BuildLog(action));
        }

        [Test]
        public void BuildLog_IncludesComboName()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual("Hero plays Fireball! COMBO: Ignite!", result.BuildLog(action));
        }

        [Test]
        public void Combo_BuffDefenseBonus_BuffsCaster()
        {
            var combo = CreateCombo("Fortify", new List<MagicTag> { MagicTag.Earth, MagicTag.Iron }, SpellEffectType.Buff, 6, buffType: BuffType.Defense);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Iron }, 3);

            var card = CreateCard("Quake", SpellEffectType.Damage, power: 3, tags: new List<MagicTag> { MagicTag.Earth });
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(6, _buffTracker.GetBuffAmount(_hero, StatType.Defense));
        }

        [Test]
        public void Combo_MultiTarget_TriggersOnEachEligible()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var enemy2 = new MockCombatUnit("Orc", attack: 4, defense: 2, health: 40, isHero: false);

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);
            _tagTracker.ApplyTags(enemy2, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy, enemy2);

            int goblinBefore = _enemy.Stats.Health;
            int orcBefore = enemy2.Stats.Health;
            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            int goblinCardDmg = ExpectedDamage(10 + 5, 3);
            int goblinComboDmg = ExpectedDamage(10, 3);
            Assert.AreEqual(goblinBefore - goblinCardDmg - goblinComboDmg, _enemy.Stats.Health);
            int orcCardDmg = ExpectedDamage(10 + 5, 2);
            int orcComboDmg = ExpectedDamage(10, 2);
            Assert.AreEqual(orcBefore - orcCardDmg - orcComboDmg, enemy2.Stats.Health);
        }

        [Test]
        public void Combo_MultiTarget_OnlyTriggersWhereTagsPresent()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var enemy2 = new MockCombatUnit("Orc", attack: 4, defense: 2, health: 40, isHero: false);

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy, enemy2);

            int goblinBefore = _enemy.Stats.Health;
            int orcBefore = enemy2.Stats.Health;
            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            int goblinCardDmg = ExpectedDamage(10 + 5, 3);
            int goblinComboDmg = ExpectedDamage(10, 3);
            Assert.AreEqual(goblinBefore - goblinCardDmg - goblinComboDmg, _enemy.Stats.Health);
            int orcCardDmg = ExpectedDamage(10 + 5, 2);
            Assert.AreEqual(orcBefore - orcCardDmg, enemy2.Stats.Health);
        }

        [Test]
        public void Combo_HealCard_WithCombo_BothApply()
        {
            var combo = CreateCombo("Rejuvenate", new List<MagicTag> { MagicTag.Nature, MagicTag.Water }, SpellEffectType.Heal, 15);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_hero, new List<MagicTag> { MagicTag.Water }, 3);

            _hero.Stats.Health = 50;
            var card = CreateCard("Bloom", SpellEffectType.Heal, power: 10, tags: new List<MagicTag> { MagicTag.Nature });
            var action = MakeAction(card, _hero, _hero);

            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.AreEqual(75, _hero.Stats.Health);
        }

        [Test]
        public void Combo_NoTagsOnCard_NoComboPossible()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("BasicSlash", SpellEffectType.Damage, power: 5);
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.IsNull(result.ComboName);
        }

        [Test]
        public void Combo_ChainedCombos_SecondCardTriggersAfterFirstAppliedTags()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            var oilCard = CreateMultiEffectCard("OilSlick", new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 1, BuffType = BuffType.Attack, Duration = 3 }
            }, tags: new List<MagicTag> { MagicTag.Oil });
            var oilAction = MakeAction(oilCard, _hero, _enemy);
            var result1 = _calculator.Execute(oilAction, _buffTracker, _tagTracker, detector);
            Assert.IsNull(result1.ComboName);

            var fireCard = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var fireAction = MakeAction(fireCard, _hero, _enemy);
            var result2 = _calculator.Execute(fireAction, _buffTracker, _tagTracker, detector);

            Assert.AreEqual("Ignite", result2.ComboName);
        }

        [Test]
        public void Combo_OilThenFire_DealsCardDamagePlusComboDamage()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 20);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            // Turn 1: OilSlick applies "Oil" tag and debuffs
            var oilCard = CreateMultiEffectCard("OilSlick", new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Attack, Duration = 3 },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Defense, Duration = 3 }
            }, tags: new List<MagicTag> { MagicTag.Oil });
            var oilAction = MakeAction(oilCard, _hero, _enemy);
            _calculator.Execute(oilAction, _buffTracker, _tagTracker, detector);

            int healthAfterOil = _enemy.Stats.Health;

            // Turn 2: Fireball deals card damage AND triggers Ignite combo
            var fireCard = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var fireAction = MakeAction(fireCard, _hero, _enemy);
            var result = _calculator.Execute(fireAction, _buffTracker, _tagTracker, detector);

            // Defense debuffed by -2: effective defense = 3 + (-2) = 1
            int expectedCardDmg = ExpectedDamage(10 + 5, 1);
            int expectedComboDmg = ExpectedDamage(20, 1);

            Assert.AreEqual("Ignite", result.ComboName);
            Assert.AreEqual(healthAfterOil - expectedCardDmg - expectedComboDmg, _enemy.Stats.Health);
        }

        [Test]
        public void Combo_DamageWithAttackDebuff_ComboUsesOwnPower()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 20);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            _buffTracker.ApplyBuff(_hero, StatType.Attack, -100, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 0, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            int healthBefore = _enemy.Stats.Health;
            _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            Assert.IsTrue(_enemy.Stats.Health < healthBefore, "Enemy should take at least combo damage");
        }

        // ---- Entry count ----

        [Test]
        public void Result_HasCorrectEntryCount_DamageWithCombo()
        {
            var combo = CreateCombo("Ignite", new List<MagicTag> { MagicTag.Fire, MagicTag.Oil }, SpellEffectType.Damage, 10);
            var detector = new ComboDetector(new List<MagicComboSO> { combo });

            _tagTracker.ApplyTags(_enemy, new List<MagicTag> { MagicTag.Oil }, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, tags: new List<MagicTag> { MagicTag.Fire });
            var action = MakeAction(card, _hero, _enemy);

            var result = _calculator.Execute(action, _buffTracker, _tagTracker, detector);

            // 1 damage entry + 1 combo name entry + 1 combo damage entry = 3
            Assert.AreEqual(3, result.Entries.Count);
        }

        // ---- Frozen status effect ----

        [Test]
        public void Frozen_AppliedViaDebuff()
        {
            var card = CreateCard("FrostNova", SpellEffectType.Debuff, power: 0, buffType: BuffType.Frozen, duration: 2);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.IsTrue(_buffTracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void Frozen_RemovedByFireDamage()
        {
            _buffTracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            var card = CreateCard("Fireball", SpellEffectType.Damage, power: 5, damageType: DamageType.Fire);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.IsFalse(_buffTracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void Frozen_NotRemovedByNonFireDamage()
        {
            _buffTracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            var card = CreateCard("IceShard", SpellEffectType.Damage, power: 5, damageType: DamageType.Ice);
            var action = MakeAction(card, _hero, _enemy);

            _calculator.Execute(action, _buffTracker);

            Assert.IsTrue(_buffTracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }
    }
}
