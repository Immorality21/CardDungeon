using System;
using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// These tests exist for one reason: adding a <see cref="StatType"/> and forgetting its
    /// <see cref="StatCatalog"/> row must fail here, not silently in play.
    ///
    /// <para>Every failure mode below has actually happened during development — a column headed "-",
    /// a caster priced as if Intelligence and Spirit did not exist, an enemy that spawned dead
    /// because its defaults were empty. The catalog collapsed those scattered per-stat lists into one
    /// table; this is the guard that keeps it complete.</para>
    /// </summary>
    public class StatCatalogTests
    {
        [Test]
        public void EveryStatTypeHasACatalogRow()
        {
            var missing = new List<StatType>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                if (stat == StatType.None)
                {
                    continue;
                }
                try
                {
                    StatCatalog.Of(stat);
                }
                catch (KeyNotFoundException)
                {
                    missing.Add(stat);
                }
            }

            Assert.IsEmpty(missing,
                "These stats have no StatCatalog row: " + string.Join(", ", missing.ConvertAll(s => s.ToString()))
                + ". Short names, recruit prices, power weights and authoring defaults all read from "
                + "the catalog, so each one would fail differently and silently.");
        }

        [Test]
        public void CatalogHasNoRowsForUnknownStatsAndNoDuplicates()
        {
            var seen = new List<StatType>();
            foreach (var definition in StatCatalog.All)
            {
                Assert.AreNotEqual(StatType.None, definition.Type, "None must not have a row.");
                Assert.IsFalse(seen.Contains(definition.Type),
                    definition.Type + " has two catalog rows; Of() would silently return only one.");
                seen.Add(definition.Type);
            }
        }

        /// <summary>
        /// The documented promise is that iteration order is <see cref="StatType"/>'s <i>declaration</i>
        /// order, so every generated table, stat line and sweep agrees without each caller deciding.
        ///
        /// <para>Comparing <c>Types</c> against <c>All</c> would prove nothing — both are built from
        /// the same array, so the assertion could not fail. The claim only has content against the
        /// enum itself.</para>
        /// </summary>
        [Test]
        public void TypesFollowTheEnumDeclarationOrderAndExcludeNone()
        {
            var declared = new List<StatType>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                if (stat != StatType.None)
                {
                    declared.Add(stat);
                }
            }

            Assert.That(StatCatalog.Types, Has.No.Member(StatType.None));
            Assert.AreEqual(declared, StatCatalog.Types,
                "Iteration order must match StatType's declaration order. A stat present in the enum "
                + "but absent here has no catalog row - see EveryStatTypeHasACatalogRow.");

            Assert.AreEqual(StatCatalog.All.Count, StatCatalog.Types.Count);
            for (int i = 0; i < StatCatalog.Types.Count; i++)
            {
                Assert.AreEqual(StatCatalog.All[i].Type, StatCatalog.Types[i]);
            }
        }

        [Test]
        public void EveryRowHasUsableLabels()
        {
            foreach (var definition in StatCatalog.All)
            {
                Assert.IsNotEmpty(definition.ShortName, definition.Type + " has no short name.");
                Assert.AreNotEqual("-", definition.ShortName,
                    definition.Type + " uses the placeholder short name, so analyzer columns would "
                    + "render as '-' with no error anywhere.");
                Assert.IsNotEmpty(definition.DisplayName, definition.Type + " has no display name.");
                Assert.IsNotEmpty(definition.Description,
                    definition.Type + " has no description, so its inspector tooltip would be blank.");
            }
        }

        /// <summary>
        /// Weights must be non-zero <i>in the catalog</i>, which is deliberately stricter than the
        /// game needs: the catalog is the seed and the fallback, and a row left at 0 is almost always
        /// a forgotten field rather than a decision. Saying "this stat contributes nothing to enemy
        /// threat" is still expressible — author a 0 row in the <c>BalanceRules</c> asset, which is
        /// the designer-facing place for tuning and which overrides the catalog.
        /// </summary>
        [Test]
        public void EveryRowIsPricedAndWeighted()
        {
            foreach (var definition in StatCatalog.All)
            {
                Assert.Greater(definition.RecruitWeight, 0f,
                    definition.Type + " has no recruit weight, so it would add nothing to a hero's "
                    + "derived price — the exact bug the catalog was introduced to prevent.");
                Assert.Greater(definition.PowerWeight, 0f,
                    definition.Type + " has no power weight, so an enemy carrying it would be scored "
                    + "as harmless by the balance model.");
            }
        }

        /// <summary>
        /// Pins the <i>rule</i> about pools, not how many there are. Asserting "exactly one" would
        /// make adding a second pool stat (Mana, Stamina) fail a test, which contradicts the whole
        /// point of the catalog: a stat should be one enum member plus one row.
        /// </summary>
        [Test]
        public void NoPoolStatCanBeASourceOfPower()
        {
            Assert.IsTrue(StatCatalog.Of(StatType.MaxHealth).IsPool,
                "MaxHealth is the health bar, not an output.");

            foreach (var definition in StatCatalog.All)
            {
                if (!definition.IsPool)
                {
                    Assert.IsTrue(StatCatalog.CanScalePower(definition.Type),
                        definition.Type + " is not a pool, so it must be usable as a power source.");
                    continue;
                }

                Assert.IsFalse(StatCatalog.CanScalePower(definition.Type),
                    definition.Type + " is a pool. Scaling off one means a spell adds the caster's "
                    + "whole resource bar to its power, and a hero swinging off one attacks with "
                    + "their health bar.");
            }
        }

        /// <summary>
        /// The two places that used to hand-list pool stats. Both now ask the catalog, so a second
        /// pool stat is covered without touching either.
        /// </summary>
        [Test]
        public void APoolStatIsRefusedAsAnAttackStatAndAsSpellScaling()
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            try
            {
                hero.AttackStat = StatType.MaxHealth;
                Assert.AreEqual(StatType.Strength, hero.ResolvedAttackStat,
                    "A hero must not swing off its own health bar.");

                hero.AttackStat = StatType.None;
                Assert.AreEqual(StatType.Strength, hero.ResolvedAttackStat, "Unset must mean Strength.");

                hero.AttackStat = StatType.Agility;
                Assert.AreEqual(StatType.Agility, hero.ResolvedAttackStat,
                    "A real output stat must be left alone.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hero);
            }

            var caster = new MockCombatUnit("Caster", strength: 6, endurance: 5, health: 40, agility: 5);
            Assert.AreEqual(0, SpellScaling.CasterContribution(caster, StatType.MaxHealth, null),
                "A spell scaling off MaxHealth would silently add the caster's whole health bar.");
            Assert.AreEqual(0, SpellScaling.CasterContribution(caster, StatType.None, null),
                "None means flat power.");
            Assert.AreEqual(6, SpellScaling.CasterContribution(caster, StatType.Strength, null));
        }

        /// <summary>
        /// <c>BuffType</c> is a second per-stat list, so "one enum member plus one catalog row" has an
        /// asterisk: a stat that should be buffable needs a <c>BuffType</c> member of the same name.
        /// <c>BuffHandlerRegistry</c> silently skips a stat without one, so no buff, debuff or
        /// Haste-style effect could ever target it and nothing would throw.
        /// </summary>
        [Test]
        public void EveryNonPoolStatCanBeBuffed()
        {
            var missing = BuffHandlerRegistry.StatsWithNoBuffType();

            Assert.IsEmpty(missing,
                "These stats have no BuffType member, so nothing can buff or debuff them: "
                + string.Join(", ", missing.ConvertAll(s => s.ToString()))
                + ". Add a BuffType member with the same name - the handler is generated from it.");
        }

        [Test]
        public void NoneIsSafeToLabel()
        {
            Assert.AreEqual("-", StatCatalog.ShortName(StatType.None));
            Assert.AreEqual("None", StatCatalog.DisplayName(StatType.None));
            Assert.IsFalse(StatCatalog.CanScalePower(StatType.None));
        }

        [Test]
        public void DefaultsProducesAPlayableUnit()
        {
            var block = StatBlock.Defaults();

            Assert.Greater(block[StatType.MaxHealth], 0,
                "A freshly authored hero or enemy must not have 0 MaxHealth — it would spawn dead.");
            Assert.Greater(block[StatType.Agility], 0,
                "0 Agility means TurnManager clamps it, so the unit acts at the floor rate.");
        }

        /// <summary>
        /// <c>BalanceRulesSO.PowerWeights</c> is serialized, so a saved rules asset is a snapshot of
        /// whatever stats existed the day it was saved. A stat added afterwards has no row, and
        /// before the catalog fallback it scored 0 - an enemy built around it measured as harmless
        /// with nothing anywhere reporting a problem.
        /// </summary>
        [Test]
        public void PowerWeightsMissingFromTheRulesAssetFallBackToTheCatalog()
        {
            var rules = ScriptableObject.CreateInstance<BalanceRulesSO>();
            try
            {
                // a rules asset saved before any of these stats existed
                rules.PowerWeights = new List<StatWeight>();

                foreach (var definition in StatCatalog.All)
                {
                    Assert.AreEqual(definition.PowerWeight, rules.WeightFor(definition.Type), 0.0001f,
                        definition.Type + " has no row in the asset and must fall back to its catalog "
                        + "weight; 0 would score an enemy built around it as harmless.");
                }

                rules.PowerWeights = null;
                Assert.AreEqual(StatCatalog.Of(StatType.Strength).PowerWeight,
                    rules.WeightFor(StatType.Strength), 0.0001f, "A null list must fall back too.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void AnAuthoredPowerWeightOverridesTheCatalog()
        {
            var rules = ScriptableObject.CreateInstance<BalanceRulesSO>();
            try
            {
                rules.PowerWeights = new List<StatWeight> { new StatWeight(StatType.Strength, 99f) };

                Assert.AreEqual(99f, rules.WeightFor(StatType.Strength), 0.0001f,
                    "The catalog seeds the weights; it must not outrank a designer's tuning.");
                Assert.AreEqual(0f, rules.WeightFor(StatType.None), 0.0001f,
                    "None must return 0 rather than throwing on the catalog lookup.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }
    }
}
