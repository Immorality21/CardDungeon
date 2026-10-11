using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Hero Workbook pass of 2026-10-11 for the last four heroes: the Rogue rebuilt on three branches
    /// (Steal, Eviscerate, Vanish, Shadowstep, Mug, Maim, Fence; Dark Twin, Murder of Crows, Golden Ferret;
    /// Death Mark, Smoke and Mirrors, Grand Heist), the Ranger's third branch (Hunter's Mark, Flushing Shot,
    /// Grey Fang) with his Ultras and awakenings, the Warrior's third branch, and the Cultist's two Ultras and
    /// awakenings. Mechanics first, then the authored grids.
    /// </summary>
    public class HeroPassKitTests
    {
        private static MagicSO Magic(MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test";
            magic.DisplayName = "Test";
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>(effects);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        private static SpellEffect Hit(int power, StatType stat = StatType.Strength)
        {
            return new SpellEffect { EffectType = SpellEffectType.Damage, Power = power, ScalingStat = stat, Duration = 0 };
        }

        private static int DamageOf(EffectResult result, ICombatUnit target)
        {
            return result.Entries.Where(e => e.Target == target).Sum(e => e.Impact);
        }

        private static EffectResult Cast(EffectResolver resolver, CombatBuffTracker tracker, MagicSO magic, ICombatUnit caster, ICombatUnit target)
        {
            return resolver.Execute(new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { target } }, tracker);
        }

        // --- Steal and Fence ----------------------------------------------------------------------

        [Test]
        public void StealChance_GrowsWithLuck_IsCapped_HalvedOnBosses_AndCertainWithABonus()
        {
            Assert.AreEqual(0.40f, StealOps.Chance(0, false), 0.001f);
            Assert.AreEqual(0.60f, StealOps.Chance(10, false), 0.001f);
            Assert.AreEqual(0.90f, StealOps.Chance(50, false), 0.001f);
            Assert.AreEqual(0.20f, StealOps.Chance(0, true), 0.001f);
            Assert.AreEqual(1f, StealOps.Chance(0, true, 100), 0.001f, "Grand Heist is certain, on a boss too");
        }

        [Test]
        public void Steal_OnASuccess_AsksTheFightForTheGoods()
        {
            var robbed = new List<ICombatUnit>();
            var resolver = new EffectResolver
            {
                Roll = () => 0f,
                StealFrom = (thief, mark) => { robbed.Add(mark); return "Stole Iron Sword"; }
            };
            var rogue = new MockCombatUnit("Rogue", 5, 0, 30);
            var enemy = new MockCombatUnit("Goblin", 3, 0, 30, isHero: false);

            var result = Cast(resolver, new CombatBuffTracker(), Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Steal }), rogue, enemy);

            CollectionAssert.AreEqual(new[] { enemy }, robbed);
            Assert.IsTrue(result.Entries.Any(e => e.Text == "Stole Iron Sword"));
            Assert.AreEqual(30, enemy.Stats.Health, "a steal does no damage");
        }

        [Test]
        public void Steal_OnAFailure_TakesNothing()
        {
            int calls = 0;
            var resolver = new EffectResolver { Roll = () => 0.99f, StealFrom = (thief, mark) => { calls++; return "x"; } };
            var result = Cast(resolver, new CombatBuffTracker(), Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Steal }),
                new MockCombatUnit("Rogue", 5, 0, 30), new MockCombatUnit("Goblin", 3, 0, 30, isHero: false));

            Assert.AreEqual(0, calls);
            Assert.IsTrue(result.Entries.Any(e => e.Text.StartsWith("Steal failed")));
        }

        [Test]
        public void Fence_RaisesTheFightsGoldBonus()
        {
            int bonus = 0;
            var resolver = new EffectResolver { AddGoldBonus = p => bonus = p };
            var rogue = new MockCombatUnit("Rogue", 5, 0, 30);
            Cast(resolver, new CombatBuffTracker(), Magic(MagicTargetType.Self,
                new SpellEffect { EffectType = SpellEffectType.FightGoldBonus, Power = 50 }), rogue, rogue);

            Assert.AreEqual(50, bonus);
            Assert.AreEqual(15, StealOps.WithBonus(10, 50));
            Assert.AreEqual(10, StealOps.WithBonus(10, 0));
        }

        // --- Evasion, Vanish and Death Mark --------------------------------------------------------

        [Test]
        public void Evasive_SpendsOneEvasionPerPhysicalBlow()
        {
            var tracker = new CombatBuffTracker();
            var resolver = new EffectResolver();
            var rogue = new MockCombatUnit("Rogue", 5, 0, 30);
            var brute = new MockCombatUnit("Brute", 10, 0, 30, isHero: false);
            BuffHandlerRegistry.Get(BuffType.Evasive).Apply(rogue, 1, 3, tracker);

            var first = Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(3)), brute, rogue);
            Assert.IsTrue(first.Entries.Any(e => e.Text == "Evade"));
            Assert.AreEqual(30, rogue.Stats.Health);

            Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(3)), brute, rogue);
            Assert.Less(rogue.Stats.Health, 30, "one evasion, one blow");
            Assert.IsFalse(tracker.HasStatusEffect(rogue, BuffType.Evasive));
        }

        [Test]
        public void Evasive_DoesNotStopMagic()
        {
            var tracker = new CombatBuffTracker();
            var rogue = new MockCombatUnit("Rogue", 5, 0, 30);
            BuffHandlerRegistry.Get(BuffType.Evasive).Apply(rogue, 1, 3, tracker);
            Cast(new EffectResolver(), tracker, Magic(MagicTargetType.SingleEnemy, Hit(3, StatType.Intelligence)),
                new MockCombatUnit("Witch", 1, 0, 30, isHero: false), rogue);

            Assert.Less(rogue.Stats.Health, 30);
            Assert.IsTrue(tracker.HasStatusEffect(rogue, BuffType.Evasive), "kept for a blow it can dodge");
        }

        [Test]
        public void Vanish_HidesTheBearer()
        {
            var tracker = new CombatBuffTracker();
            var rogue = new MockCombatUnit("Rogue", 5, 0, 30);
            BuffHandlerRegistry.Get(BuffType.Hidden).Apply(rogue, 1, 1, tracker);
            Assert.IsTrue(HiddenBuffHandler.IsHidden(tracker, rogue));
            Assert.IsFalse(BuffHandlerRegistry.IsCurable(BuffType.Hidden), "a cure must not strip her own trick");
        }

        [Test]
        public void DeathMark_MakesEveryDamagingHitAHarderCrit()
        {
            var tracker = new CombatBuffTracker();
            var resolver = new EffectResolver();
            var rogue = new MockCombatUnit("Rogue", 10, 0, 30);
            var plain = new MockCombatUnit("Plain", 1, 0, 200, isHero: false);
            var marked = new MockCombatUnit("Marked", 1, 0, 200, isHero: false);
            BuffHandlerRegistry.Get(BuffType.DeathMarked).Apply(marked, 1, 3, tracker);

            int normal = DamageOf(Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(4)), rogue, plain), plain);
            int onMark = DamageOf(Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(4)), rogue, marked), marked);

            Assert.AreEqual(CritRules.Apply(normal, true), onMark);
            Assert.Greater(onMark, CritRules.Apply(normal));
        }

        // --- Eviscerate, Hunter's Mark -------------------------------------------------------------

        [Test]
        public void Eviscerate_EarnsItsBonusPerDebuff_UpToThree()
        {
            var tracker = new CombatBuffTracker();
            var target = new MockCombatUnit("Target", 1, 0, 100, isHero: false);
            var effect = new SpellEffect { EffectType = SpellEffectType.Damage, BonusPercent = 50, BonusPerDebuff = true };

            Assert.AreEqual(1f, effect.BonusFactor(target, tracker), 0.001f);
            tracker.ApplyStatusEffect(target, BuffType.Silenced, 3);
            tracker.ApplyBuff(target, StatType.Strength, -2, 3);
            Assert.AreEqual(2f, effect.BonusFactor(target, tracker), 0.001f);
            tracker.ApplyStatusEffect(target, BuffType.Blinded, 3);
            tracker.ApplyStatusEffect(target, BuffType.Slow, 3);
            Assert.AreEqual(2.5f, effect.BonusFactor(target, tracker), 0.001f, "capped at three debuffs");
        }

        [Test]
        public void HuntersMark_MakesEveryHitLandHarder_AndFlushingShotHarderStill()
        {
            var tracker = new CombatBuffTracker();
            var resolver = new EffectResolver();
            var ranger = new MockCombatUnit("Ranger", 10, 0, 30);
            var plain = new MockCombatUnit("Plain", 1, 0, 200, isHero: false);
            var prey = new MockCombatUnit("Prey", 1, 0, 200, isHero: false);
            Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Debuff, BuffType = BuffType.Marked, Power = 25, Duration = 3 }), ranger, prey);

            Assert.IsTrue(tracker.HasStatusEffect(prey, BuffType.Marked));
            Assert.AreEqual(-25f, tracker.GetResistanceBonus(prey, DamageType.Fire), 0.001f);

            int onPlain = DamageOf(Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(10)), ranger, plain), plain);
            int onPrey = DamageOf(Cast(resolver, tracker, Magic(MagicTargetType.SingleEnemy, Hit(10)), ranger, prey), prey);
            Assert.Greater(onPrey, onPlain);

            var flush = Hit(10);
            flush.BonusVsStatus = BuffType.Marked;
            flush.BonusPercent = 100;
            Assert.AreEqual(2f, flush.BonusFactor(prey, tracker), 0.001f);
            Assert.AreEqual(1f, flush.BonusFactor(plain, tracker), 0.001f);
        }

        // --- Mass Hysteria -------------------------------------------------------------------------

        [Test]
        public void Confusion_IsACurableStatus()
        {
            var tracker = new CombatBuffTracker();
            var enemy = new MockCombatUnit("Goblin", 3, 0, 30, isHero: false);
            BuffHandlerRegistry.Get(BuffType.Confused).Apply(enemy, 1, 2, tracker);
            Assert.IsTrue(ConfusedBuffHandler.IsConfused(tracker, enemy));
            Assert.IsTrue(BuffHandlerRegistry.IsCurable(BuffType.Confused));
            Assert.IsTrue(StatusImmunity.IsStatus(BuffType.Confused));
        }

        // --- the authored grids --------------------------------------------------------------------

        private static SphereGridSO Grid(string hero)
        {
            var so = AssetDatabase.LoadAssetAtPath<HeroSO>($"Assets/ScriptableObjects/Heroes/{hero}.asset");
            Assert.IsNotNull(so, hero);
            return so.SphereGrid;
        }

        private static List<string> Taught(SphereGridSO grid, SphereNodeKind kind)
        {
            return grid.Nodes.Where(n => n.Kind == kind)
                .Select(n => kind == SphereNodeKind.MagicKnown ? n.GrantedMagicKey
                    : kind == SphereNodeKind.Summon ? n.GrantedSummonKey
                    : n.GrantedUltraKey)
                .ToList();
        }

        [Test]
        public void Rogue_HasStealOnTheTrunk_AndThreeBranchesOfHerOwn()
        {
            var grid = Grid("Rogue");
            var steal = grid.Nodes.Single(n => n.GrantedMagicKey == "Steal" && n.Kind == SphereNodeKind.MagicKnown);
            Assert.That(steal.Neighbors, Does.Contain("rogue-trunk-1"));

            var known = Taught(grid, SphereNodeKind.MagicKnown);
            CollectionAssert.IsSubsetOf(new[] { "Backstab", "PoisonDart", "Eviscerate", "SmokeBomb", "Vanish", "Shadowstep", "Mug", "Maim", "Fence" }, known);
            CollectionAssert.DoesNotContain(known, "AimedShot", "Aimed Shot stays the Ranger's");
            CollectionAssert.DoesNotContain(known, "Hush", "Hush stays the Ranger's");
            CollectionAssert.AreEquivalent(new[] { "DarkTwin", "MurderOfCrows", "GoldenFerret" }, Taught(grid, SphereNodeKind.Summon));
            CollectionAssert.AreEquivalent(new[] { "DeathMark", "SmokeAndMirrors", "GrandHeist" }, Taught(grid, SphereNodeKind.Ultra));
        }

        [Test]
        public void Ranger_ThirdBranch_IsTheHunt()
        {
            var grid = Grid("Ranger");
            CollectionAssert.IsSubsetOf(new[] { "HuntersMark", "FlushingShot" }, Taught(grid, SphereNodeKind.MagicKnown));
            CollectionAssert.Contains(Taught(grid, SphereNodeKind.Summon), "GreyFang");
            CollectionAssert.AreEquivalent(new[] { "RainOfArrows", "PinDown", "PackHunt" }, Taught(grid, SphereNodeKind.Ultra));
            var awakened = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "ranger-a-awaken", "ranger-b-awaken" });
            Assert.AreEqual("ArrowStorm", awakened["Volley"]);
            Assert.AreEqual("DeadSilence", awakened["Hush"]);
        }

        [Test]
        public void Warrior_ThirdBranch_IsTheDuelist()
        {
            var grid = Grid("Warrior");
            CollectionAssert.IsSubsetOf(new[] { "Hamstring", "PressTheAttack", "FinishingBlow" }, Taught(grid, SphereNodeKind.MagicKnown));
            CollectionAssert.Contains(Taught(grid, SphereNodeKind.Summon), "IronCharger");
            CollectionAssert.Contains(Taught(grid, SphereNodeKind.Ultra), "Earthsplitter");
        }

        [Test]
        public void Cultist_GainsTwoUltras_AndTwoAwakenings()
        {
            var grid = Grid("Cultist");
            CollectionAssert.AreEquivalent(new[] { "Sacrifice", "MassHysteria", "BloodMoon" }, Taught(grid, SphereNodeKind.Ultra));
            var awakened = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "cultist-b-awaken", "cultist-c-awaken" });
            Assert.AreEqual("Terror", awakened["Dread"]);
            Assert.AreEqual("BloodCircle", awakened["RiteOfWarding"]);
        }
    }
}
