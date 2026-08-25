using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Temporary elemental resistance — the defensive half of the elemental layer, and a no-op for
    /// as long as the five resistance <see cref="BuffType"/>s existed: the popup read
    /// "+40 FireResistance" and nothing changed.
    ///
    /// <para>These tests cover the whole chain a cloak walks: handler → tracker → the bonus the
    /// damage pipeline reads, including the point where stacking crosses 100% and starts healing.</para>
    /// </summary>
    public class ResistanceBuffTests
    {
        private CombatBuffTracker _tracker;
        private MockCombatUnit _hero;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CombatBuffTracker();
            _hero = new MockCombatUnit("Hero", strength: 10, endurance: 0, health: 100);
        }

        private static MagicSO Cloak(BuffType resistance, int percent, int duration, int costPercent)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "Cloak-" + resistance;
            magic.DisplayName = "Cloak";
            magic.TargetType = MagicTargetType.Self;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = SpellEffectType.Buff,
                    BuffType = resistance,
                    Power = percent,
                    Duration = duration
                },
                new SpellEffect
                {
                    EffectType = SpellEffectType.HealthCost,
                    Power = costPercent,
                    PowerMode = PowerMode.PercentOfMaxHealth
                }
            };
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        private void Cast(MagicSO magic, ICombatUnit unit)
        {
            new EffectResolver().Execute(
                new SpellcastAction
                {
                    Magic = magic,
                    Caster = unit,
                    Targets = new List<ICombatUnit> { unit }
                },
                _tracker);
        }

        // ---------------------------------------------------------------- the tracker

        [Test]
        public void ApplyResistance_NoBuff_IsZero()
        {
            Assert.AreEqual(0f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        [Test]
        public void ApplyResistance_Stacks_Uncapped()
        {
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);

            // Uncapped on purpose: three cloaks reach 120%, which is how absorption is reachable.
            // The clamp lives in DamageCalculator, over innate + gear + this together.
            Assert.AreEqual(120f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        [Test]
        public void ApplyResistance_IsPerDamageType()
        {
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);

            Assert.AreEqual(40f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
            Assert.AreEqual(0f, _tracker.GetResistanceBonus(_hero, DamageType.Ice));
        }

        [Test]
        public void ApplyResistance_TicksDownAndExpires()
        {
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 2);

            _tracker.TickBuffs(_hero);
            Assert.AreEqual(40f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));

            _tracker.TickBuffs(_hero);
            Assert.AreEqual(0f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        [Test]
        public void ApplyResistance_IsNotAStatBuff()
        {
            // Resistance entries share the CombatBuff record with stat buffs, so the stat query has to
            // exclude them or a resistance would read as a buff to whatever StatType.None maps to.
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.None));
            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Endurance));
        }

        [Test]
        public void ApplyResistance_Negative_IsAVulnerability()
        {
            _tracker.ApplyResistance(_hero, DamageType.Fire, -50, 3);

            Assert.AreEqual(-50f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        // ---------------------------------------------------------------- the handler

        [Test]
        public void Handler_IsNoLongerAnInertBuffType()
        {
            var handler = BuffHandlerRegistry.Get(BuffType.FireResistance);

            Assert.IsNotNull(handler);
            handler.Apply(_hero, 40, 3, _tracker);

            Assert.AreEqual(40f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        [Test]
        public void Handler_EveryResistanceBuffTypeMapsToItsElement()
        {
            var expected = new Dictionary<BuffType, DamageType>
            {
                { BuffType.FireResistance, DamageType.Fire },
                { BuffType.IceResistance, DamageType.Ice },
                { BuffType.LightningResistance, DamageType.Lightning },
                { BuffType.HolyResistance, DamageType.Holy },
                { BuffType.ShadowResistance, DamageType.Shadow }
            };

            foreach (var pair in expected)
            {
                var tracker = new CombatBuffTracker();
                BuffHandlerRegistry.Get(pair.Key).Apply(_hero, 25, 3, tracker);

                Assert.AreEqual(25f, tracker.GetResistanceBonus(_hero, pair.Value),
                    pair.Key + " should grant " + pair.Value + " resistance");
            }
        }

        [Test]
        public void Handler_ZeroPower_AppliesNothing()
        {
            BuffHandlerRegistry.Get(BuffType.FireResistance).Apply(_hero, 0, 3, _tracker);

            Assert.AreEqual(0f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
        }

        // ---------------------------------------------------------------- end to end

        [Test]
        public void Cloak_ReducesIncomingElementalDamage()
        {
            int before = DamageCalculator.Calculate(
                100, 0, DamageType.Fire, _hero.Resistances,
                _tracker.GetResistanceBonus(_hero, DamageType.Fire));

            Cast(Cloak(BuffType.FireResistance, 40, 3, 10), _hero);

            int after = DamageCalculator.Calculate(
                100, 0, DamageType.Fire, _hero.Resistances,
                _tracker.GetResistanceBonus(_hero, DamageType.Fire));

            Assert.AreEqual(100, before);
            Assert.AreEqual(60, after);
            Assert.AreEqual(90, _hero.Stats.Health, "the cloak costs 10% of the caster's max health");
        }

        [Test]
        public void Cloak_DoesNotDefendAgainstAnotherElement()
        {
            Cast(Cloak(BuffType.FireResistance, 40, 3, 10), _hero);

            int ice = DamageCalculator.Calculate(
                100, 0, DamageType.Ice, _hero.Resistances,
                _tracker.GetResistanceBonus(_hero, DamageType.Ice));

            Assert.AreEqual(100, ice);
        }

        [Test]
        public void StackedCloaksAndGear_CrossIntoAbsorption()
        {
            // The FFVIII behaviour: a deliberately assembled defence passes 100% and the hit heals.
            _hero.Resistances.Add(new Resistance { DamageType = DamageType.Fire, Percent = 50f });
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);
            _tracker.ApplyResistance(_hero, DamageType.Fire, 40, 3);

            float bonus = _tracker.GetResistanceBonus(_hero, DamageType.Fire);
            int damage = DamageCalculator.Calculate(100, 0, DamageType.Fire, _hero.Resistances, bonus);

            Assert.AreEqual(130f, 50f + bonus);
            Assert.Less(damage, 0, "over 100% resistance heals instead of hurting");
            Assert.AreEqual(
                DamageEffectiveness.Absorbed,
                DamageCalculator.Classify(DamageType.Fire, _hero.Resistances, bonus));
        }

        [Test]
        public void Ward_CoversThreeElementsAtOnce()
        {
            var ward = ScriptableObject.CreateInstance<MagicSO>();
            ward.Key = "Ward";
            ward.DisplayName = "Ward";
            ward.TargetType = MagicTargetType.Self;
            ward.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Buff, BuffType = BuffType.FireResistance, Power = 20, Duration = 2 },
                new SpellEffect { EffectType = SpellEffectType.Buff, BuffType = BuffType.IceResistance, Power = 20, Duration = 2 },
                new SpellEffect { EffectType = SpellEffectType.Buff, BuffType = BuffType.LightningResistance, Power = 20, Duration = 2 },
                new SpellEffect { EffectType = SpellEffectType.HealthCost, Power = 20, PowerMode = PowerMode.PercentOfMaxHealth }
            };
            ward.Tags = new List<MagicTag>();

            Cast(ward, _hero);

            Assert.AreEqual(20f, _tracker.GetResistanceBonus(_hero, DamageType.Fire));
            Assert.AreEqual(20f, _tracker.GetResistanceBonus(_hero, DamageType.Ice));
            Assert.AreEqual(20f, _tracker.GetResistanceBonus(_hero, DamageType.Lightning));
            Assert.AreEqual(0f, _tracker.GetResistanceBonus(_hero, DamageType.Shadow));
            Assert.AreEqual(80, _hero.Stats.Health);
        }
    }
}
