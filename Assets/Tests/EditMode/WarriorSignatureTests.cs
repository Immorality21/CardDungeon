using System.Collections.Generic;
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
    /// The Warrior's kit from his vision (2026-10-10): Momentum (stacks of Strength for every turn in a
    /// row he lands a hit), Take Cover (the party takes less from every hit for a turn - a resistance to
    /// every element) and Flurry (an Ultra that strikes several random enemies).
    /// </summary>
    public class WarriorSignatureTests
    {
        private class MomentumUnit : MockCombatUnit, IHasMomentum
        {
            public MomentumUnit(string name, int strength, MomentumRule rule)
                : base(name, strength, 0, 100)
            {
                Momentum = rule;
            }

            public MomentumRule Momentum { get; }
        }

        private static readonly MomentumRule Rule = new MomentumRule { PercentPerStack = 10, MaxStacks = 3 };

        // ---------------------------------------------------------------- Momentum, the arithmetic

        [Test]
        public void Next_AHitAddsAStack_UpToTheCap()
        {
            Assert.AreEqual(1, MomentumOps.Next(0, true, Rule));
            Assert.AreEqual(3, MomentumOps.Next(2, true, Rule));
            Assert.AreEqual(3, MomentumOps.Next(3, true, Rule));
        }

        [Test]
        public void Next_ATurnWithNoHit_DropsEveryStack()
        {
            Assert.AreEqual(0, MomentumOps.Next(3, false, Rule));
        }

        [Test]
        public void StrengthBonus_IsAPercentPerStack_ButAlwaysAtLeastOneAStack()
        {
            Assert.AreEqual(6, MomentumOps.StrengthBonus(20, 3, Rule));   // 10% of 20, three times
            Assert.AreEqual(2, MomentumOps.StrengthBonus(4, 2, Rule));    // 0.8 rounds to 1, floor of a point a stack
            Assert.AreEqual(0, MomentumOps.StrengthBonus(20, 0, Rule));
        }

        [Test]
        public void Inactive_RuleBuildsNothing()
        {
            var none = new MomentumRule();
            Assert.AreEqual(0, MomentumOps.Next(0, true, none));
            Assert.AreEqual(0, MomentumOps.StrengthBonus(20, 3, none));
        }

        // ---------------------------------------------------------------- Momentum, off the event stream

        private static void Turn(CombatEvents events, ICombatUnit unit, ICombatUnit target, HealthCause? hit)
        {
            events.Publish(new TurnStarted { Unit = unit });
            if (hit.HasValue)
            {
                HealthOps.Damage(target, 5, new HealthSource(unit, hit.Value), events);
            }
            events.Publish(new TurnEnded { Unit = unit });
        }

        [Test]
        public void Tracker_TurnsInARowWithAHit_BuildStrength_AndAnEmptyTurnDropsIt()
        {
            var events = new CombatEvents();
            var buffs = new CombatBuffTracker { Events = events };
            var tracker = new MomentumTracker(events, buffs);
            var warrior = new MomentumUnit("Warrior", 20, Rule);
            var enemy = new MockCombatUnit("Slime", 1, 0, 100, isHero: false);

            Turn(events, warrior, enemy, HealthCause.Attack);
            Turn(events, warrior, enemy, HealthCause.Ability);
            Assert.AreEqual(2, tracker.StacksOf(warrior));
            Assert.AreEqual(4, buffs.GetBuffAmount(warrior, StatType.Strength));

            Turn(events, warrior, enemy, null);
            Assert.AreEqual(0, tracker.StacksOf(warrior));
            Assert.AreEqual(0, buffs.GetBuffAmount(warrior, StatType.Strength));
        }

        [Test]
        public void Tracker_TicksAndPricesDoNotCount()
        {
            var events = new CombatEvents();
            var buffs = new CombatBuffTracker { Events = events };
            var tracker = new MomentumTracker(events, buffs);
            var warrior = new MomentumUnit("Warrior", 20, Rule);
            var enemy = new MockCombatUnit("Slime", 1, 0, 100, isHero: false);

            Turn(events, warrior, enemy, HealthCause.OverTime);
            Assert.AreEqual(0, tracker.StacksOf(warrior));
        }

        [Test]
        public void Tracker_TheStrengthDoesNotCountDownWithTheTurns()
        {
            var events = new CombatEvents();
            var buffs = new CombatBuffTracker { Events = events };
            var tracker = new MomentumTracker(events, buffs);
            var warrior = new MomentumUnit("Warrior", 20, Rule);
            var enemy = new MockCombatUnit("Slime", 1, 0, 100, isHero: false);

            Turn(events, warrior, enemy, HealthCause.Attack);
            for (int i = 0; i < 5; i++)
            {
                buffs.TickBuffs(warrior);
            }
            Assert.AreEqual(2, buffs.GetBuffAmount(warrior, StatType.Strength));
            Assert.AreEqual(1, tracker.StacksOf(warrior));
        }

        [Test]
        public void Tracker_LeavesAuthoredStrengthBuffsAlone()
        {
            var events = new CombatEvents();
            var buffs = new CombatBuffTracker { Events = events };
            new MomentumTracker(events, buffs);
            var warrior = new MomentumUnit("Warrior", 20, Rule);
            var enemy = new MockCombatUnit("Slime", 1, 0, 100, isHero: false);
            buffs.ApplyPercentBuff(warrior, StatType.Strength, 10, 3);   // the Bloodfang Boar's +50%, say

            Turn(events, warrior, enemy, HealthCause.Attack);
            Turn(events, warrior, enemy, null);

            Assert.AreEqual(10, buffs.GetBuffAmount(warrior, StatType.Strength));
        }

        // ---------------------------------------------------------------- Take Cover (Guarded)

        [Test]
        public void Guarded_IsResistanceToEveryElement_NormalIncluded()
        {
            var tracker = new CombatBuffTracker();
            var hero = new MockCombatUnit("Hero", 10, 0, 100);
            BuffHandlerRegistry.Get(BuffType.Guarded).Apply(hero, 30, 1, tracker);

            Assert.AreEqual(30f, tracker.GetResistanceBonus(hero, DamageType.Normal));
            Assert.AreEqual(30f, tracker.GetResistanceBonus(hero, DamageType.Fire));
            Assert.AreEqual(30f, tracker.GetResistanceBonus(hero, DamageType.Shadow));
            Assert.IsTrue(tracker.HasResistanceToAll(hero));
        }

        [Test]
        public void Guarded_CutsAPhysicalHitByItsPercentage()
        {
            var tracker = new CombatBuffTracker();
            var hero = new MockCombatUnit("Hero", 10, 0, 100);
            BuffHandlerRegistry.Get(BuffType.Guarded).Apply(hero, 30, 1, tracker);

            int guarded = DamageCalculator.Calculate(20, 0, DamageType.Normal, hero.Resistances,
                tracker.GetResistanceBonus(hero, DamageType.Normal));
            Assert.AreEqual(14, guarded);
        }

        [Test]
        public void Guarded_WearsOff()
        {
            var tracker = new CombatBuffTracker();
            var hero = new MockCombatUnit("Hero", 10, 0, 100);
            BuffHandlerRegistry.Get(BuffType.Guarded).Apply(hero, 30, 1, tracker);
            tracker.TickBuffs(hero);

            Assert.AreEqual(0f, tracker.GetResistanceBonus(hero, DamageType.Normal));
            Assert.IsFalse(tracker.HasResistanceToAll(hero));
        }

        // ---------------------------------------------------------------- Flurry (Strike hits)

        [Test]
        public void StrikeTargets_RandomTargets_PicksTheEnemyTheRollNames()
        {
            var ultra = ScriptableObject.CreateInstance<UltraSO>();
            ultra.Kind = UltraKind.Strike;
            ultra.TargetType = MagicTargetType.SingleEnemy;
            ultra.RandomTargets = true;
            var hero = new MockCombatUnit("Hero", 10, 0, 100);
            var weak = new MockCombatUnit("Weak", 1, 0, 5, isHero: false);
            var strong = new MockCombatUnit("Strong", 1, 0, 50, isHero: false);
            var enemies = new List<ICombatUnit> { weak, strong };

            var picked = UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, enemies, new List<ICombatUnit> { hero }, count => 1);
            Assert.AreSame(strong, picked[0]);

            ultra.RandomTargets = false;
            picked = UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, enemies, new List<ICombatUnit> { hero }, count => 1);
            Assert.AreSame(weak, picked[0], "without RandomTargets a single-enemy Strike takes the weakest");
            Object.DestroyImmediate(ultra);
        }

        [Test]
        public void StrikeTargets_NobodyLeft_IsEmpty()
        {
            var ultra = ScriptableObject.CreateInstance<UltraSO>();
            ultra.TargetType = MagicTargetType.SingleEnemy;
            ultra.RandomTargets = true;
            var hero = new MockCombatUnit("Hero", 10, 0, 100);

            var picked = UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, new List<ICombatUnit>(), new List<ICombatUnit> { hero }, count => 0);
            Assert.IsEmpty(picked);
            Object.DestroyImmediate(ultra);
        }

        // ---------------------------------------------------------------- the authored Warrior

        [Test]
        public void Warrior_HasMomentum_AndTeachesFlurryAndTakeCover()
        {
            var warrior = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Warrior.asset");
            Assert.IsNotNull(warrior);
            Assert.IsTrue(warrior.Momentum.IsActive, "the Warrior's signature is Momentum");

            var ultras = SphereGridOps.UltrasForNodes(warrior.SphereGrid, new List<string> { "warrior-a-cover", "warrior-b-flurry" });
            CollectionAssert.Contains(ultras, "TakeCover");
            CollectionAssert.Contains(ultras, "Flurry");
            Assert.IsNotNull(UltraCatalogSO.Resolve("TakeCover"));
            Assert.IsNotNull(UltraCatalogSO.Resolve("Flurry"));
        }
    }
}
