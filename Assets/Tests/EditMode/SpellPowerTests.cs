using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="PowerMode"/> and the health-cost arithmetic: what a spell's Power means, and what
    /// it charges the caster to say it.
    ///
    /// <para>The load-bearing assertion in here is BasePower reproducing today's numbers exactly —
    /// the whole mode is meant to be additive, so an existing asset must not move.</para>
    /// </summary>
    public class SpellPowerTests
    {
        private CombatBuffTracker _buffTracker;
        private MockCombatUnit _caster;
        private MockCombatUnit _target;

        [SetUp]
        public void SetUp()
        {
            _buffTracker = new CombatBuffTracker();
            _caster = new MockCombatUnit("Caster", strength: 10, endurance: 5, health: 30);
            _target = new MockCombatUnit("Target", strength: 8, endurance: 4, health: 100, isHero: false);
        }

        private static SpellEffect Effect(
            SpellEffectType type, int power, PowerMode mode, StatType scaling = StatType.None)
        {
            return new SpellEffect
            {
                EffectType = type,
                Power = power,
                PowerMode = mode,
                ScalingStat = scaling,
                Duration = 3
            };
        }

        private static MagicSO Magic(string key, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            magic.DisplayName = key;
            magic.TargetType = MagicTargetType.Self;
            magic.Effects = new List<SpellEffect>(effects);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        // ---------------------------------------------------------------- PowerMode

        [Test]
        public void Resolve_BasePower_AddsCasterScalingStat()
        {
            var effect = Effect(SpellEffectType.Damage, 4, PowerMode.BasePower, StatType.Strength);

            Assert.AreEqual(14, SpellPower.Resolve(effect, _caster, _target, _buffTracker));
        }

        [Test]
        public void Resolve_Flat_IgnoresCasterStat()
        {
            var effect = Effect(SpellEffectType.Damage, 4, PowerMode.Flat, StatType.Strength);

            Assert.AreEqual(4, SpellPower.Resolve(effect, _caster, _target, _buffTracker));
        }

        [Test]
        public void Resolve_PercentOfMaxHealth_ReadsTheTargetsBar()
        {
            var effect = Effect(SpellEffectType.Damage, 20, PowerMode.PercentOfMaxHealth, StatType.Strength);

            // 20% of the target's 100, and the caster's Strength contributes nothing.
            Assert.AreEqual(20, SpellPower.Resolve(effect, _caster, _target, _buffTracker));
        }

        [Test]
        public void Resolve_FlatPowerArgument_DoesNotOverridePercentage()
        {
            var effect = Effect(SpellEffectType.Heal, 10, PowerMode.PercentOfMaxHealth);

            // flatPower means "no caster contribution", which a percentage already has none of.
            Assert.AreEqual(10, SpellPower.Resolve(effect, _caster, _target, _buffTracker, flatPower: true));
        }

        [Test]
        public void PercentOfMaxHealth_RoundsDown()
        {
            var hero = new MockCombatUnit("Warrior", strength: 10, endurance: 5, health: 13);

            // 10% of 13 is 1.3 -> 1, not 2.
            Assert.AreEqual(1, SpellPower.PercentOfMaxHealth(10, hero));
        }

        [Test]
        public void PercentOfMaxHealth_UnderOne_FloorsToOne()
        {
            var tiny = new MockCombatUnit("Sprite", strength: 1, endurance: 0, health: 5);

            // 10% of 5 is 0.5, which would round down to a free spell.
            Assert.AreEqual(1, SpellPower.PercentOfMaxHealth(10, tiny));
        }

        [Test]
        public void PercentOfMaxHealth_ZeroPercent_IsNothing()
        {
            Assert.AreEqual(0, SpellPower.PercentOfMaxHealth(0, _caster));
        }

        [Test]
        public void PercentOfMaxHealth_ReadsEffectiveMaxHealth_NotTheBaseStat()
        {
            // +MaxHealth gear has to count, the same rule every other health ceiling follows.
            _caster.EffectiveOverrides[StatType.MaxHealth] = 60;

            Assert.AreEqual(6, SpellPower.PercentOfMaxHealth(10, _caster));
        }

        // ---------------------------------------------------------------- health cost

        [Test]
        public void ResolveHealthCost_Percentage_IsAShareOfTheCastersBar()
        {
            var effect = Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth);

            Assert.AreEqual(3, SpellPower.ResolveHealthCost(effect, _caster));
        }

        [Test]
        public void ResolveHealthCost_Flat_IsTheAuthoredNumber()
        {
            var effect = Effect(SpellEffectType.HealthCost, 4, PowerMode.Flat);

            Assert.AreEqual(4, SpellPower.ResolveHealthCost(effect, _caster));
        }

        [Test]
        public void ResolveHealthCost_NonCostEffect_IsFree()
        {
            var effect = Effect(SpellEffectType.Damage, 10, PowerMode.Flat);

            Assert.AreEqual(0, SpellPower.ResolveHealthCost(effect, _caster));
        }

        [Test]
        public void TotalHealthCost_SumsEveryCostEffect()
        {
            var magic = Magic("DoubleCost",
                Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth),
                Effect(SpellEffectType.HealthCost, 2, PowerMode.Flat));

            Assert.AreEqual(5, SpellPower.TotalHealthCost(magic, _caster));
        }

        [Test]
        public void TotalHealthCost_SkipsEffectsTheUpgradeLevelHasNotUnlocked()
        {
            var locked = Effect(SpellEffectType.HealthCost, 2, PowerMode.Flat);
            locked.UnlockLevel = 3;
            var magic = Magic("Gated",
                Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth),
                locked);

            // The quoted price has to be the price paid, so it reads the same gate the resolver does.
            Assert.AreEqual(3, SpellPower.TotalHealthCost(magic, _caster, magicUpgradeLevel: 0));
            Assert.AreEqual(5, SpellPower.TotalHealthCost(magic, _caster, magicUpgradeLevel: 3));
        }

        [Test]
        public void CanAfford_CostBelowHealth_IsAllowed()
        {
            var magic = Magic("Cloak", Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth));

            Assert.IsTrue(SpellPower.CanAfford(magic, _caster));
        }

        [Test]
        public void CanAfford_CostEqualsHealth_IsRefused()
        {
            var magic = Magic("Cloak", Effect(SpellEffectType.HealthCost, 3, PowerMode.Flat));
            _caster.Stats.Health = 3;

            // Exactly lethal is still lethal: the cast is gated rather than allowed to kill.
            Assert.IsFalse(SpellPower.CanAfford(magic, _caster));
        }

        [Test]
        public void CanAfford_FreeMagic_IsAlwaysAllowed()
        {
            var magic = Magic("Fireball", Effect(SpellEffectType.Damage, 4, PowerMode.BasePower));
            _caster.Stats.Health = 1;

            Assert.IsTrue(SpellPower.CanAfford(magic, _caster));
        }

        // ---------------------------------------------------------------- the executor

        [Test]
        public void HealthCostExecutor_ChargesTheCasterNotTheTarget()
        {
            var executor = new HealthCostEffectExecutor();
            var result = new EffectResult();

            executor.Execute(
                Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth),
                _caster,
                new List<ICombatUnit> { _target },
                _buffTracker,
                result);

            Assert.AreEqual(27, _caster.Stats.Health);
            Assert.AreEqual(100, _target.Stats.Health);
            Assert.AreEqual(_caster, result.Entries[0].Target);
        }

        [Test]
        public void HealthCostExecutor_IgnoresDefenseAndResistance()
        {
            var executor = new HealthCostEffectExecutor();
            _caster.Resistances.Add(new Resistance { DamageType = DamageType.Normal, Percent = 100f });
            _caster.EffectiveOverrides[StatType.Endurance] = 999;

            executor.Execute(
                Effect(SpellEffectType.HealthCost, 5, PowerMode.Flat),
                _caster,
                new List<ICombatUnit>(),
                _buffTracker,
                new EffectResult());

            Assert.AreEqual(25, _caster.Stats.Health);
        }

        [Test]
        public void HealthCostExecutor_NeverKillsTheCaster()
        {
            // The UI gates the cast, so this is the safety net: a bug must not be able to kill a hero
            // through their own spell, because the cast path has no death handling to run them through.
            var executor = new HealthCostEffectExecutor();
            _caster.Stats.Health = 2;

            executor.Execute(
                Effect(SpellEffectType.HealthCost, 50, PowerMode.Flat),
                _caster,
                new List<ICombatUnit>(),
                _buffTracker,
                new EffectResult());

            Assert.AreEqual(1, _caster.Stats.Health);
            Assert.IsTrue(_caster.IsAlive);
        }

        // ---------------------------------------------------------------- resolver ordering

        [Test]
        public void Resolver_CostAuthoredFirst_StillAppliesTheBuffItPaidFor()
        {
            // BuffEffectExecutor skips dead targets, so a cost resolved first could take a caster to
            // 1 HP and, in the limit, fizzle the buff. Costs are resolved last whatever the order.
            var magic = Magic("Cloak",
                Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth),
                new SpellEffect
                {
                    EffectType = SpellEffectType.Buff,
                    BuffType = BuffType.FireResistance,
                    Power = 40,
                    Duration = 3
                });

            var resolver = new EffectResolver();
            resolver.Execute(
                new SpellcastAction
                {
                    Magic = magic,
                    Caster = _caster,
                    Targets = new List<ICombatUnit> { _caster }
                },
                _buffTracker);

            Assert.AreEqual(40f, _buffTracker.GetResistanceBonus(_caster, DamageType.Fire));
            Assert.AreEqual(27, _caster.Stats.Health);
        }

        [Test]
        public void Resolver_UpgradeBonus_DoesNotRaiseAHealthCost()
        {
            var magic = Magic("Cloak", Effect(SpellEffectType.HealthCost, 10, PowerMode.PercentOfMaxHealth));

            var resolver = new EffectResolver();
            resolver.Execute(
                new SpellcastAction
                {
                    Magic = magic,
                    Caster = _caster,
                    Targets = new List<ICombatUnit> { _caster }
                },
                _buffTracker,
                powerBonus: 10,
                magicUpgradeLevel: 5);

            // Upgrading a spell must never raise what it charges.
            Assert.AreEqual(27, _caster.Stats.Health);
        }

        [Test]
        public void Resolver_UpgradeBonus_SkipsPercentageEffects()
        {
            var magic = Magic("Drain", Effect(SpellEffectType.Damage, 20, PowerMode.PercentOfMaxHealth));
            _target.EffectiveOverrides[StatType.Endurance] = 0;

            var resolver = new EffectResolver();
            resolver.Execute(
                new SpellcastAction
                {
                    Magic = magic,
                    Caster = _caster,
                    Targets = new List<ICombatUnit> { _target }
                },
                _buffTracker,
                powerBonus: 10,
                magicUpgradeLevel: 5);

            // +2 per upgrade level on a percentage would read as percentage points and double the
            // spell at max upgrade. 20% of 100, defense 0, is 20 either way.
            Assert.AreEqual(80, _target.Stats.Health);
        }

        [Test]
        public void Resolver_PercentageDamage_ScalesWithTheTargetsBar()
        {
            var magic = Magic("Drain", Effect(SpellEffectType.Damage, 10, PowerMode.PercentOfMaxHealth));
            var small = new MockCombatUnit("Imp", strength: 3, endurance: 0, health: 20, isHero: false);
            var large = new MockCombatUnit("Boss", strength: 3, endurance: 0, health: 200, isHero: false);

            var resolver = new EffectResolver();
            resolver.Execute(
                new SpellcastAction
                {
                    Magic = magic,
                    Caster = _caster,
                    Targets = new List<ICombatUnit> { small, large }
                },
                _buffTracker);

            Assert.AreEqual(18, small.Stats.Health);
            Assert.AreEqual(180, large.Stats.Health);
        }
    }
}
