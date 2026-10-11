using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Tinkerer's workbook pass (2026-10-11): the Auto Turret (a summon that acts alone, whose
    /// grid node raises the odds of its dormant Blind), and the many-shot ability behind Rivet Storm.
    /// </summary>
    public class TinkererKitTests
    {
        private System.Func<float> _savedRoll;

        [SetUp]
        public void SetUp()
        {
            _savedRoll = AfflictionRules.Roll;
        }

        [TearDown]
        public void TearDown()
        {
            AfflictionRules.Roll = _savedRoll;
        }

        /// <summary>A caster whose grid (or summoner's grid) added <paramref name="grant"/> to its abilities.</summary>
        private class UpgradedCaster : MockCombatUnit, IAbilityUpgrades
        {
            private readonly AbilityGrant _grant;

            public UpgradedCaster(AbilityGrant grant) : base("Turret", 5, 2, 20)
            {
                _grant = grant;
            }

            public AbilityGrant UpgradesFor(string abilityKey)
            {
                return _grant;
            }
        }

        private static MagicSO Shot()
        {
            var shot = ScriptableObject.CreateInstance<MagicSO>();
            shot.Key = "Shot";
            shot.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 1, BuffType = BuffType.Blinded, Duration = 2, ApplyChance = 0 }
            };
            return shot;
        }

        private static SummonSO Turret(MagicSO attack)
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "Turret";
            summon.Kind = SummonKind.JoinParty;
            summon.AttackAbility = attack;
            summon.ActsAlone = true;
            return summon;
        }

        private static bool Blinds(ICombatUnit caster, MagicSO shot)
        {
            var target = new MockCombatUnit("Sentry", 5, 2, 30, isHero: false);
            var tracker = new CombatBuffTracker();
            new EffectResolver().Execute(
                new SpellcastAction { Magic = shot, Caster = caster, Targets = new List<ICombatUnit> { target } },
                tracker);
            return tracker.HasStatusEffect(target, BuffType.Blinded);
        }

        [Test]
        public void SummonsForNodes_ApplyChanceNode_AddsToTheSummonsGrant()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "turret";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "turret", Kind = SphereNodeKind.Summon, GrantedSummonKey = "Turret", Neighbors = new List<string>() },
                new SphereGridNode { Key = "blind", Kind = SphereNodeKind.SummonApplyChance, GrantedSummonKey = "Turret", SummonAmount = 35, Neighbors = new List<string> { "turret" } }
            };

            var grants = SphereGridOps.SummonsForNodes(grid, new[] { "turret", "blind" });

            Assert.AreEqual(35, grants[0].ApplyChanceBonus);
        }

        [Test]
        public void UpgradesFor_OnlyReachesTheSummonsOwnAbilities()
        {
            var shot = Shot();
            var turret = Turret(shot);
            var grant = new SummonGrant { Key = "Turret", ApplyChanceBonus = 35 };

            Assert.AreEqual(35, SummonOps.UpgradesFor(turret, grant, "Shot").ApplyChanceBonus);
            Assert.IsNull(SummonOps.UpgradesFor(turret, grant, "Fireball"));
            Assert.IsNull(SummonOps.UpgradesFor(turret, new SummonGrant { Key = "Turret" }, "Shot"));
        }

        [Test]
        public void DormantDebuff_NeverLands_EvenOnAPerfectRoll()
        {
            AfflictionRules.Roll = () => 0f;

            Assert.IsFalse(Blinds(new MockCombatUnit("Turret", 5, 2, 20), Shot()));
        }

        [Test]
        public void DormantDebuff_WakesWhenTheGridRaisesItsOdds()
        {
            AfflictionRules.Roll = () => 0f;

            Assert.IsTrue(Blinds(new UpgradedCaster(new AbilityGrant { ApplyChanceBonus = 35 }), Shot()));
        }

        [Test]
        public void DormantDebuff_IsLeftOutOfTheAbilityText()
        {
            var plain = AbilityDescriber.EffectLines(Shot(), new MockCombatUnit("Turret", 5, 2, 20), null);
            var upgraded = AbilityDescriber.EffectLines(Shot(), new UpgradedCaster(new AbilityGrant { ApplyChanceBonus = 35 }), null);

            Assert.IsEmpty(plain);
            Assert.AreEqual(1, upgraded.Count);
            StringAssert.Contains("35% chance", upgraded[0]);
        }

        [Test]
        public void RandomHits_DescribesItsShots()
        {
            var storm = ScriptableObject.CreateInstance<MagicSO>();
            storm.TargetType = MagicTargetType.AllEnemies;
            storm.RandomHits = 3;

            Assert.AreEqual("3 shots, random enemies", AbilityDescriber.TargetLabel(storm));
        }

        // --- the authored assets ----------------------------------------------

        [Test]
        public void AuthoredKit_MatchesTheWorkbookPass()
        {
            var turret = AssetDatabase.LoadAssetAtPath<SummonSO>("Assets/ScriptableObjects/Summons/AutoTurret.asset");
            var storm = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/RivetStorm.asset");
            var bomb = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/IncendiaryBomb.asset");
            var cluster = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/ClusterBomb.asset");
            var grid = AssetDatabase.LoadAssetAtPath<SphereGridSO>("Assets/ScriptableObjects/Heroes/Grids/TinkererGrid.asset");

            Assert.IsTrue(turret.ActsAlone && turret.Kind == SummonKind.JoinParty && turret.AttackAbility != null);
            Assert.AreEqual(3, storm.RandomHits);
            Assert.AreEqual(MagicTargetType.SingleEnemy, bomb.TargetType, "Incendiary Bomb is single-target; Cluster Bomb is its area awakening.");
            Assert.AreEqual(MagicTargetType.AllEnemies, cluster.TargetType);

            var all = new List<string>();
            grid.Nodes.ForEach(n => all.Add(n.Key));
            var known = SphereGridOps.KnownMagicForNodes(grid, all);
            Assert.IsTrue(known.Exists(k => k.Key == "RivetStorm") && known.Exists(k => k.Key == "ClusterBomb"));
            Assert.IsFalse(known.Exists(k => k.Key == "RivetGun" || k.Key == "IncendiaryBomb"), "an awakening replaces the ability");
            var turretGrant = SphereGridOps.SummonsForNodes(grid, all).Find(g => g.Key == "AutoTurret");
            Assert.Greater(turretGrant.ApplyChanceBonus, 0);
        }
    }
}
