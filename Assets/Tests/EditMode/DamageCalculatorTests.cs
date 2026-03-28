using System.Collections.Generic;
using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DamageCalculatorTests
    {
        // ---- Basic defense diminishing returns ----

        [Test]
        public void Calculate_ZeroDefense_FullDamage()
        {
            int result = DamageCalculator.Calculate(100, 0, DamageType.Normal, null);

            Assert.AreEqual(100, result);
        }

        [Test]
        public void Calculate_DefenseEqualToConstant_HalfDamage()
        {
            // At defense == K (20), reduction = 20/(20+20) = 50%
            int result = DamageCalculator.Calculate(100, 20, DamageType.Normal, null);

            Assert.AreEqual(50, result);
        }

        [Test]
        public void Calculate_HighDefense_DiminishingReturns()
        {
            // defense=60 => reduction = 60/(60+20) = 75% => 25 damage
            int result = DamageCalculator.Calculate(100, 60, DamageType.Normal, null);

            Assert.AreEqual(25, result);
        }

        [Test]
        public void Calculate_VeryHighDefense_NeverReachesZero()
        {
            // defense=1000 => reduction = 1000/1020 ≈ 98% => ~2 damage, min 1
            int result = DamageCalculator.Calculate(100, 1000, DamageType.Normal, null);

            Assert.GreaterOrEqual(result, 1);
        }

        [Test]
        public void Calculate_MinimumOneDamage()
        {
            int result = DamageCalculator.Calculate(1, 999, DamageType.Normal, null);

            Assert.AreEqual(1, result);
        }

        [Test]
        public void Calculate_ZeroRawDamage_ReturnsZero()
        {
            int result = DamageCalculator.Calculate(0, 10, DamageType.Normal, null);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void Calculate_NegativeRawDamage_ReturnsZero()
        {
            int result = DamageCalculator.Calculate(-5, 10, DamageType.Normal, null);

            Assert.AreEqual(0, result);
        }

        // ---- Resistance: no resistance ----

        [Test]
        public void Calculate_NoResistances_NormalDamage()
        {
            int result = DamageCalculator.Calculate(100, 0, DamageType.Fire, new List<Resistance>());

            Assert.AreEqual(100, result);
        }

        [Test]
        public void Calculate_NullResistances_NormalDamage()
        {
            int result = DamageCalculator.Calculate(100, 0, DamageType.Fire, null);

            Assert.AreEqual(100, result);
        }

        // ---- Resistance: partial ----

        [Test]
        public void Calculate_50PercentResistance_HalfDamage()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 50f }
            };

            // 100 * 0.5 = 50, no defense => 50
            int result = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);

            Assert.AreEqual(50, result);
        }

        [Test]
        public void Calculate_34PercentResistance_34PercentReduction()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Ice, Percent = 34f }
            };

            // 100 * 0.66 = 66
            int result = DamageCalculator.Calculate(100, 0, DamageType.Ice, resistances);

            Assert.AreEqual(66, result);
        }

        // ---- Resistance: immune ----

        [Test]
        public void Calculate_100PercentResistance_Immune()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Holy, Percent = 100f }
            };

            int result = DamageCalculator.Calculate(100, 0, DamageType.Holy, resistances);

            Assert.AreEqual(0, result);
        }

        // ---- Resistance: absorption (>100%) ----

        [Test]
        public void Calculate_200PercentResistance_FullAbsorption()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Shadow, Percent = 200f }
            };

            // 100 * (1 - 2.0) = -100 => negative = healing
            int result = DamageCalculator.Calculate(100, 0, DamageType.Shadow, resistances);

            Assert.AreEqual(-100, result);
        }

        [Test]
        public void Calculate_150PercentResistance_PartialAbsorption()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 150f }
            };

            // 100 * (1 - 1.5) = -50
            int result = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);

            Assert.AreEqual(-50, result);
        }

        [Test]
        public void Calculate_Absorption_IgnoresDefense()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 150f }
            };

            // Even with high defense, absorption bypasses it
            int withoutDefense = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);
            int withDefense = DamageCalculator.Calculate(100, 50, DamageType.Fire, resistances);

            Assert.AreEqual(withoutDefense, withDefense);
        }

        // ---- Resistance: negative (vulnerability) ----

        [Test]
        public void Calculate_Negative100Resistance_DoubleDamage()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Lightning, Percent = -100f }
            };

            // 100 * (1 - (-1.0)) = 100 * 2.0 = 200, no defense => 200
            int result = DamageCalculator.Calculate(100, 0, DamageType.Lightning, resistances);

            Assert.AreEqual(200, result);
        }

        [Test]
        public void Calculate_Negative50Resistance_50PercentMoreDamage()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Ice, Percent = -50f }
            };

            // 100 * 1.5 = 150
            int result = DamageCalculator.Calculate(100, 0, DamageType.Ice, resistances);

            Assert.AreEqual(150, result);
        }

        // ---- Resistance + Defense combined ----

        [Test]
        public void Calculate_ResistanceAppliedBeforeDefense()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 50f }
            };

            // Raw 100, 50% resist => 50 after resist
            // Defense 20 => reduction = 20/40 = 50% => 50 * 0.5 = 25
            int result = DamageCalculator.Calculate(100, 20, DamageType.Fire, resistances);

            Assert.AreEqual(25, result);
        }

        [Test]
        public void Calculate_NegativeResistance_WithDefense()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Ice, Percent = -100f }
            };

            // Raw 100, -100% resist => 200 after resist
            // Defense 20 => reduction = 50% => 200 * 0.5 = 100
            int result = DamageCalculator.Calculate(100, 20, DamageType.Ice, resistances);

            Assert.AreEqual(100, result);
        }

        // ---- Wrong damage type resistance doesn't apply ----

        [Test]
        public void Calculate_ResistanceForWrongType_NoEffect()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 100f }
            };

            // Fire resistance shouldn't affect Ice damage
            int result = DamageCalculator.Calculate(100, 0, DamageType.Ice, resistances);

            Assert.AreEqual(100, result);
        }

        // ---- Multiple resistances ----

        [Test]
        public void Calculate_MultipleResistances_CorrectOneApplied()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 75f },
                new Resistance { DamageType = DamageType.Ice, Percent = -50f },
                new Resistance { DamageType = DamageType.Normal, Percent = 25f }
            };

            // Fire: 100 * 0.25 = 25
            int fire = DamageCalculator.Calculate(100, 0, DamageType.Fire, resistances);
            Assert.AreEqual(25, fire);

            // Ice: 100 * 1.5 = 150
            int ice = DamageCalculator.Calculate(100, 0, DamageType.Ice, resistances);
            Assert.AreEqual(150, ice);

            // Normal: 100 * 0.75 = 75
            int normal = DamageCalculator.Calculate(100, 0, DamageType.Normal, resistances);
            Assert.AreEqual(75, normal);

            // Lightning (no entry): 100 * 1.0 = 100
            int lightning = DamageCalculator.Calculate(100, 0, DamageType.Lightning, resistances);
            Assert.AreEqual(100, lightning);
        }

        // ---- GetResistance ----

        [Test]
        public void GetResistance_FoundEntry_ReturnsPercent()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 42f }
            };

            Assert.AreEqual(42f, DamageCalculator.GetResistance(DamageType.Fire, resistances), 0.001f);
        }

        [Test]
        public void GetResistance_NoEntry_ReturnsZero()
        {
            var resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Ice, Percent = 50f }
            };

            Assert.AreEqual(0f, DamageCalculator.GetResistance(DamageType.Fire, resistances), 0.001f);
        }

        [Test]
        public void GetResistance_NullList_ReturnsZero()
        {
            Assert.AreEqual(0f, DamageCalculator.GetResistance(DamageType.Fire, null), 0.001f);
        }
    }
}
