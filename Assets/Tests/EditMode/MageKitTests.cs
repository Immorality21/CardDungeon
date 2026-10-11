using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Mage's kit from his vision (2026-10-10): Focus and MultiCast, thorns on the elemental cloaks,
    /// Glacius (a claw that lays Ice + a barrier on the party), Ifrit's parting Hellfire, and the grid.
    /// </summary>
    public class MageKitTests
    {
        private class FocusedUnit : MockCombatUnit, IHasFocus
        {
            public FocusedUnit(string name) : base(name, 3, 0, 40)
            {
            }

            public FocusRule Focus { get; } = new FocusRule { PerCast = 1, Max = 3 };
        }

        private static MagicSO Magic(string key, MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>(effects);
            return magic;
        }

        private static HeroSO Mage()
        {
            return AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Mage.asset");
        }

        // ---------------------------------------------------------------- Focus

        [Test]
        public void FocusOps_BuildsOnCasts_UpToTheMax()
        {
            var rule = new FocusRule { PerCast = 1, Max = 3 };
            Assert.AreEqual(1, FocusOps.Next(0, true, rule));
            Assert.AreEqual(1, FocusOps.Next(1, false, rule), "a turn without a cast keeps what was built");
            Assert.AreEqual(3, FocusOps.Next(3, true, rule));
            Assert.AreEqual(0, FocusOps.Next(2, true, new FocusRule()), "no rule, no Focus");
        }

        [Test]
        public void FocusOps_Casts_AreOnePlusFocus()
        {
            Assert.AreEqual(1, FocusOps.Casts(0));
            Assert.AreEqual(4, FocusOps.Casts(3));
            Assert.AreEqual("Triple-cast", FocusOps.CastName(2));
        }

        [Test]
        public void FocusTracker_CountsCasts_NotSummonsOrUltras_AndSpendingEarnsNothing()
        {
            var events = new CombatEvents();
            var focus = new FocusTracker(events);
            var mage = new FocusedUnit("Mage");
            var bolt = Magic("Bolt", MagicTargetType.SingleEnemy);
            var summon = Magic(SummonOps.CastKeyPrefix + "Qyraxis", MagicTargetType.AllEnemies);

            void Turn(MagicSO cast)
            {
                events.Publish(new TurnStarted { Unit = mage });
                if (cast != null)
                {
                    events.Publish(new AbilityUsed { Caster = mage, Ability = cast });
                }
                events.Publish(new TurnEnded { Unit = mage });
            }

            Turn(bolt);
            Turn(summon);
            Turn(null);
            Assert.AreEqual(1, focus.FocusOf(mage), "only the ability counted");
            Turn(bolt);
            Assert.AreEqual(2, focus.FocusOf(mage));

            events.Publish(new TurnStarted { Unit = mage });
            Assert.AreEqual(2, focus.Spend(mage));
            events.Publish(new AbilityUsed { Caster = mage, Ability = bolt });
            events.Publish(new TurnEnded { Unit = mage });
            Assert.AreEqual(0, focus.FocusOf(mage), "the MultiCast turn builds nothing back");
            Object.DestroyImmediate(bolt);
            Object.DestroyImmediate(summon);
        }

        [Test]
        public void MultiCast_IsReadyOnFocus_NotTheGauge()
        {
            var multi = ScriptableObject.CreateInstance<UltraSO>();
            multi.Kind = UltraKind.MultiCast;
            Assert.IsFalse(UltraOps.IsReady(multi, UltraOps.Max, 0), "a full gauge does nothing for it");
            Assert.IsTrue(UltraOps.IsReady(multi, 0, 1), "one Focus is a double-cast");
            var strike = ScriptableObject.CreateInstance<UltraSO>();
            strike.Kind = UltraKind.Strike;
            Assert.IsFalse(UltraOps.IsReady(strike, 0, 3));
            Assert.IsTrue(UltraOps.IsReady(strike, UltraOps.Max, 0));
            Object.DestroyImmediate(multi);
            Object.DestroyImmediate(strike);
        }

        // ---------------------------------------------------------------- thorns

        [Test]
        public void Thorns_ReturnAShareOfEachBlow_InTheirElement_UntilTheyRunOut()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            new RetaliationTracker(events, resolver, tracker);
            var mage = new MockCombatUnit("Mage", 3, 0, 60);
            var hound = new MockCombatUnit("Hound", 5, 0, 40, isHero: false);
            var cloak = Magic("Cloak", MagicTargetType.Self, new SpellEffect
            {
                EffectType = SpellEffectType.Thorns, Power = 25, DamageType = DamageType.Fire, Duration = 1
            });

            events.Publish(new TurnStarted { Unit = mage });
            resolver.Execute(new SpellcastAction { Magic = cloak, Caster = mage, Targets = new List<ICombatUnit> { mage } }, tracker);
            events.Publish(new TurnEnded { Unit = mage });

            HealthOps.Damage(mage, 8, new HealthSource(hound, HealthCause.Attack), events);
            Assert.AreEqual(38, hound.Stats.Health, "25% of 8 comes back");
            HealthOps.Damage(mage, 8, new HealthSource(hound, HealthCause.Attack), events);
            Assert.AreEqual(36, hound.Stats.Health, "every blow, not just the first");

            events.Publish(new TurnStarted { Unit = mage });
            events.Publish(new TurnEnded { Unit = mage });
            HealthOps.Damage(mage, 8, new HealthSource(hound, HealthCause.Attack), events);
            Assert.AreEqual(36, hound.Stats.Health, "a 1-turn cloak's thorns are gone after its next turn");
            Object.DestroyImmediate(cloak);
        }

        [Test]
        public void ElementalCloaks_CarryThornsInTheirOwnElement()
        {
            var expected = new Dictionary<string, DamageType>
            {
                { "FireCloak", DamageType.Fire }, { "FrostCloak", DamageType.Ice }, { "StormCloak", DamageType.Lightning }
            };
            foreach (var pair in expected)
            {
                var cloak = AssetDatabase.LoadAssetAtPath<MagicSO>($"Assets/ScriptableObjects/Cards/{pair.Key}.asset");
                var thorns = cloak.Effects.FirstOrDefault(e => e.EffectType == SpellEffectType.Thorns);
                Assert.IsNotNull(thorns, pair.Key);
                Assert.AreEqual(pair.Value, thorns.DamageType, pair.Key);
            }
        }

        // ---------------------------------------------------------------- summons

        [Test]
        public void Glacius_ClawAsksForAnEnemy_LaysIce_AndItsPowerNodeGrowsTheBarrier()
        {
            var glacius = AssetDatabase.LoadAssetAtPath<SummonSO>("Assets/ScriptableObjects/Summons/Glacius.asset");
            Assert.IsTrue(SummonOps.NeedsTarget(glacius), "a single-enemy special attack asks which");
            var castable = SummonOps.BuildCastable(glacius, null);
            CollectionAssert.Contains(castable.Tags, MagicTag.Ice);
            Object.DestroyImmediate(castable);

            var grant = new SummonGrant { PowerBonus = 5 };
            Assert.AreEqual(glacius.Effects[0].Power, SummonOps.EffectsFor(glacius, grant)[0].Power, "the claw does not grow");
            Assert.AreEqual(glacius.PartyEffects[0].Power + 5, SummonOps.PartyEffectsFor(glacius, grant)[0].Power);
        }

        [Test]
        public void Ifrit_LeavesWithHellfire_AndItIsNeverAHerosMagic()
        {
            var ifrit = AssetDatabase.LoadAssetAtPath<SummonSO>("Assets/ScriptableObjects/Summons/Ifrit.asset");
            Assert.AreEqual(SummonKind.ReplaceParty, ifrit.Kind);
            Assert.IsNotNull(ifrit.PartingAbility);
            CollectionAssert.Contains(SummonOps.AbilityKeys(new[] { ifrit }), ifrit.PartingAbility.Key);
        }

        // ---------------------------------------------------------------- the grid

        [Test]
        public void Mage_TeachesMultiCast_ThreeSummons_AndThreeAwakenings()
        {
            var mage = Mage();
            Assert.IsTrue(mage.Focus.IsActive, "the Mage builds Focus");
            var all = mage.SphereGrid.Nodes.Select(n => n.Key).ToList();
            CollectionAssert.Contains(SphereGridOps.UltrasForNodes(mage.SphereGrid, all), "MultiCast");
            Assert.IsNotNull(UltraCatalogSO.Resolve("MultiCast"));
            foreach (var key in new[] { "Qyraxis", "Glacius", "Ifrit" })
            {
                Assert.IsTrue(mage.SphereGrid.Nodes.Any(n => n.Kind == SphereNodeKind.Summon && n.GrantedSummonKey == key), key);
            }
            var awakenings = SphereGridOps.AwakeningsForNodes(mage.SphereGrid, all);
            Assert.AreEqual("Tempest", awakenings["ChainLightning"]);
            Assert.AreEqual("Deluge", awakenings["WaterSplash"]);
            Assert.AreEqual("Inferno", awakenings["Fireball"]);
        }
    }
}
