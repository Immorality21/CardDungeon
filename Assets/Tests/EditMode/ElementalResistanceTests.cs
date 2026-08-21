using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for the elemental layer: resistances summing across sources, gear granting resistance, and
    /// physical attacks carrying an element. Absorption (&gt;100%) is deliberately reachable, so the
    /// boundaries around 100% are pinned here.
    /// </summary>
    public class ElementalResistanceTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _created.Clear();
        }

        private static List<Resistance> Resist(params object[] pairs)
        {
            var list = new List<Resistance>();
            for (int i = 0; i < pairs.Length; i += 2)
            {
                list.Add(new Resistance { DamageType = (DamageType)pairs[i], Percent = (float)pairs[i + 1] });
            }
            return list;
        }

        // ---------------------------------------------------------------- summing

        [Test]
        public void GetResistance_SumsEveryMatchingEntry()
        {
            var resistances = Resist(
                DamageType.Fire, 25f,
                DamageType.Ice, 90f,
                DamageType.Fire, 20f);

            Assert.AreEqual(45f, DamageCalculator.GetResistance(DamageType.Fire, resistances), 0.001f);
            Assert.AreEqual(90f, DamageCalculator.GetResistance(DamageType.Ice, resistances), 0.001f);
            Assert.AreEqual(0f, DamageCalculator.GetResistance(DamageType.Shadow, resistances), 0.001f);
        }

        [Test]
        public void GetResistance_SumsPositiveAndNegativeTogether()
        {
            var resistances = Resist(DamageType.Fire, 60f, DamageType.Fire, -20f);
            Assert.AreEqual(40f, DamageCalculator.GetResistance(DamageType.Fire, resistances), 0.001f);
        }

        [Test]
        public void Calculate_StackedResistancesReachImmunity()
        {
            // Two 50% entries sum to exactly 100%: immune, and defense never enters the picture.
            var resistances = Resist(DamageType.Fire, 50f, DamageType.Fire, 50f);
            Assert.AreEqual(0, DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances));
        }

        [Test]
        public void Calculate_AboveOneHundredPercent_AbsorbsAsNegativeDamage()
        {
            // 150% resistance = the hit heals for half of what it would have dealt.
            var resistances = Resist(DamageType.Fire, 100f, DamageType.Fire, 50f);
            int damage = DamageCalculator.Calculate(100, 30, DamageType.Fire, resistances);

            Assert.AreEqual(-50, damage, "150% resistance should heal for 50% of the raw hit.");
            Assert.AreEqual(DamageEffectiveness.Absorbed,
                DamageCalculator.Classify(DamageType.Fire, resistances));
        }

        [Test]
        public void Calculate_AbsorptionIgnoresDefense()
        {
            var resistances = Resist(DamageType.Fire, 150f);

            int withoutDefense = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);
            int withDefense = DamageCalculator.Calculate(100, 40, DamageType.Fire, resistances);

            Assert.AreEqual(withoutDefense, withDefense,
                "Absorption bypasses the defense curve, so defense must not change the heal.");
        }

        [Test]
        public void Calculate_ResistanceIsClampedAtTwoHundredPercent()
        {
            var atCap = Resist(DamageType.Fire, 200f);
            var pastCap = Resist(DamageType.Fire, 400f);

            Assert.AreEqual(
                DamageCalculator.Calculate(100, 0, DamageType.Fire, atCap),
                DamageCalculator.Calculate(100, 0, DamageType.Fire, pastCap),
                "Healing can never exceed the damage the hit would have dealt.");
        }

        // ---------------------------------------------------------------- temporary bonus

        [Test]
        public void Calculate_BonusStacksOnTopOfInnateResistance()
        {
            var resistances = Resist(DamageType.Fire, 50f);

            int innateOnly = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);
            int withBuff = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances, 40f);

            Assert.AreEqual(50, innateOnly);
            Assert.AreEqual(10, withBuff, "50% innate plus a 40% buff should leave 10% of the hit.");
        }

        [Test]
        public void Classify_AgreesWithCalculateAcrossTheBoundaries()
        {
            var cases = new Dictionary<float, DamageEffectiveness>
            {
                { -50f, DamageEffectiveness.Weak },
                { 0f, DamageEffectiveness.Normal },
                { 50f, DamageEffectiveness.Resisted },
                { 100f, DamageEffectiveness.Immune },
                { 150f, DamageEffectiveness.Absorbed }
            };

            foreach (var kvp in cases)
            {
                var resistances = Resist(DamageType.Fire, kvp.Key);
                int damage = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);

                Assert.AreEqual(kvp.Value, DamageCalculator.Classify(DamageType.Fire, resistances),
                    $"Classification disagreed at {kvp.Key}%.");

                switch (kvp.Value)
                {
                    case DamageEffectiveness.Weak:
                        Assert.Greater(damage, 100);
                        break;
                    case DamageEffectiveness.Normal:
                        Assert.AreEqual(100, damage);
                        break;
                    case DamageEffectiveness.Resisted:
                        Assert.That(damage, Is.GreaterThan(0).And.LessThan(100));
                        break;
                    case DamageEffectiveness.Immune:
                        Assert.AreEqual(0, damage);
                        break;
                    case DamageEffectiveness.Absorbed:
                        Assert.Less(damage, 0);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- gear

        [Test]
        public void ComputeResistances_SumsAcrossEquippedGear()
        {
            var amulet = Make<ItemSO>();
            amulet.Resistances = Resist(DamageType.Fire, 25f);

            var shield = Make<ItemSO>();
            shield.Resistances = Resist(DamageType.Fire, 10f, DamageType.Ice, 20f);

            var totals = InventoryOperations.ComputeResistances(new List<ItemSO> { amulet, shield });

            Assert.AreEqual(35f, DamageCalculator.GetResistance(DamageType.Fire, totals), 0.001f);
            Assert.AreEqual(20f, DamageCalculator.GetResistance(DamageType.Ice, totals), 0.001f);
        }

        [Test]
        public void ComputeResistances_HandlesNullAndEmptyInput()
        {
            Assert.IsEmpty(InventoryOperations.ComputeResistances(null));
            Assert.IsEmpty(InventoryOperations.ComputeResistances(new List<ItemSO>()));

            var bare = Make<ItemSO>();
            Assert.IsEmpty(InventoryOperations.ComputeResistances(new List<ItemSO> { bare }));
        }

        [Test]
        public void ComputeResistances_PlusBuff_CanReachAbsorption()
        {
            // The intended FFVIII-style build: gear gets you most of the way, a buff finishes the job.
            var amulet = Make<ItemSO>();
            amulet.Resistances = Resist(DamageType.Fire, 25f);
            var shield = Make<ItemSO>();
            shield.Resistances = Resist(DamageType.Fire, 10f);
            var cloak = Make<ItemSO>();
            cloak.Resistances = Resist(DamageType.Fire, 70f);

            var gear = InventoryOperations.ComputeResistances(new List<ItemSO> { amulet, shield, cloak });

            // 105% from gear, plus a 40% temporary buff.
            int damage = DamageCalculator.Calculate(100, 0, DamageType.Fire, gear, 40f);
            Assert.Less(damage, 0, "145% total resistance should absorb.");
        }

        // ---------------------------------------------------------------- elemental attacks

        [Test]
        public void EnemySO_DefaultsToPhysicalAttacks()
        {
            var enemy = Make<EnemySO>();
            Assert.AreEqual(DamageType.Normal, enemy.AttackDamageType,
                "Normal must stay the default so existing enemies bypass the elemental layer.");
        }

        [Test]
        public void SimUnit_CarriesTheDefinitionsAttackElement()
        {
            var enemy = Make<EnemySO>();
            enemy.DisplayName = "Cinder Imp";
            enemy.Strength = 6;
            enemy.Health = 10;
            enemy.AttackDamageType = DamageType.Fire;

            var unit = SimUnit.FromEnemy(enemy);
            Assert.AreEqual(DamageType.Fire, unit.AttackDamageType);
            Assert.AreEqual(DamageType.Fire, unit.Clone().AttackDamageType);
        }

        [Test]
        public void BalanceMath_BasicAttackUsesTheAttackersElement()
        {
            var fireEnemy = Make<EnemySO>();
            fireEnemy.Strength = 20;
            fireEnemy.Health = 10;
            fireEnemy.AttackDamageType = DamageType.Fire;
            var attacker = SimUnit.FromEnemy(fireEnemy);

            var target = new SimUnit
            {
                DisplayName = "hero",
                IsHero = true,
                Stats = new Stats(0, 0, 100, 5),
                EffectiveAttackPower = 0,
                EffectiveEndurance = 0,
                EffectiveAgility = 5,
                Resistances = Resist(DamageType.Fire, 50f)
            };

            float dealt = BalanceMath.DamagePerTick(attacker, new List<SimUnit> { target }, 1);

            var physical = SimUnit.FromEnemy(fireEnemy);
            physical.AttackDamageType = DamageType.Normal;
            float dealtPhysical = BalanceMath.DamagePerTick(physical, new List<SimUnit> { target }, 1);

            Assert.Less(dealt, dealtPhysical,
                "The target's 50% fire resistance must reduce a fire attack but not a physical one.");
        }

        [Test]
        public void Simulator_AbsorbedBasicAttackHealsWithoutExceedingMaxHealth()
        {
            var attacker = new SimUnit
            {
                DisplayName = "flamer",
                IsHero = false,
                Stats = new Stats(50, 0, 100, 5),
                EffectiveAttackPower = 50,
                EffectiveEndurance = 0,
                EffectiveAgility = 5,
                AttackDamageType = DamageType.Fire
            };

            var target = new SimUnit
            {
                DisplayName = "absorber",
                IsHero = true,
                Stats = new Stats(0, 0, 100, 5) { Health = 95 },
                EffectiveAttackPower = 0,
                EffectiveEndurance = 0,
                EffectiveAgility = 5,
                Resistances = Resist(DamageType.Fire, 200f)
            };

            int result = EncounterSimulator.ResolveAttack(attacker, target, new Assets.Scripts.Cards.CombatBuffTracker());

            Assert.AreEqual(100, target.Stats.MaxHealth);
            Assert.AreEqual(100, target.Stats.Health, "Absorption must clamp to max health, not overheal.");
            Assert.AreEqual(-5, result, "The reported figure should be the health actually restored.");
        }
    }
}
