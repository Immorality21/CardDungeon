using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The systems the Cleric's vision needed (2026-10-10): Revive, the barrier, Cure, Execute and an
    /// effect's bonus against a kind of unit - and the authored Cleric that uses them.
    /// </summary>
    public class ClericSystemsTests
    {
        private class TraitUnit : MockCombatUnit, IHasTraits
        {
            public TraitUnit(string name, int health, UnitTraits traits, bool boss = false)
                : base(name, 1, 0, health, isHero: false)
            {
                Traits = traits;
                IsBoss = boss;
            }

            public UnitTraits Traits { get; }
            public bool IsBoss { get; }
        }

        private static MagicSO Magic(MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test";
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>(effects);
            return magic;
        }

        private static EffectResult Cast(EffectResolver resolver, CombatBuffTracker tracker, MagicSO magic, ICombatUnit caster,
            params ICombatUnit[] targets)
        {
            return resolver.Execute(new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit>(targets) }, tracker);
        }

        // ---------------------------------------------------------------- the barrier

        [Test]
        public void Barrier_SoaksHitsBeforeTheBar_ThenBreaks()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Hero", 1, 0, 50);
            events.Barriers.Add(hero, 8, 3);

            var first = HealthOps.Damage(hero, 5, new HealthSource(null, HealthCause.Attack), events);
            Assert.AreEqual(5, first.Absorbed);
            Assert.AreEqual(50, hero.Stats.Health);

            var second = HealthOps.Damage(hero, 5, new HealthSource(null, HealthCause.Ability), events);
            Assert.AreEqual(3, second.Absorbed);
            Assert.AreEqual(48, hero.Stats.Health);
            Assert.AreEqual(0, events.Barriers.PointsOf(hero));
        }

        [Test]
        public void Barrier_NeverSoaksAPrice()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Hero", 1, 0, 50);
            events.Barriers.Add(hero, 20, 3);

            HealthOps.Damage(hero, 5, new HealthSource(hero, HealthCause.Cost), events);
            Assert.AreEqual(45, hero.Stats.Health);
            Assert.AreEqual(20, events.Barriers.PointsOf(hero));
        }

        [Test]
        public void Barrier_WearsOffWithTheBearersTurns_AndATopUpDoesNotStack()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Hero", 1, 0, 50);
            events.Barriers.Add(hero, 10, 1);
            events.Barriers.Add(hero, 6, 2);
            Assert.AreEqual(10, events.Barriers.PointsOf(hero), "the larger pool wins, it is not added");

            events.Publish(new TurnStarted { Unit = hero });
            events.Publish(new TurnEnded { Unit = hero });
            Assert.AreEqual(10, events.Barriers.PointsOf(hero));
            events.Publish(new TurnStarted { Unit = hero });
            events.Publish(new TurnEnded { Unit = hero });
            Assert.AreEqual(0, events.Barriers.PointsOf(hero));
        }

        [Test]
        public void BarrierBuff_AsAShareOfTheBar_IsThatShareOfEachTarget()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var caster = new MockCombatUnit("Cleric", 1, 0, 20);
            var big = new MockCombatUnit("Big", 1, 0, 80);
            var shield = Magic(MagicTargetType.SingleAlly, new SpellEffect
            {
                EffectType = SpellEffectType.Buff, BuffType = BuffType.Barrier, Power = 25, Duration = 3,
                PowerMode = PowerMode.PercentOfMaxHealth
            });

            Cast(resolver, tracker, shield, caster, big);
            Assert.AreEqual(20, events.Barriers.PointsOf(big));
            Object.DestroyImmediate(shield);
        }

        // ---------------------------------------------------------------- Revive

        [Test]
        public void Revive_StandsAFallenHeroUp_AtItsShare_AndSaysSo()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var fallen = new MockCombatUnit("Warrior", 1, 0, 50);
            fallen.Stats.Health = 0;
            ICombatUnit revived = null;
            events.Subscribe<UnitRevived>(e => revived = e.Unit);
            var revive = Magic(MagicTargetType.FallenAlly, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 30 });

            Cast(resolver, tracker, revive, cleric, fallen);
            Assert.AreEqual(15, fallen.Stats.Health);
            Assert.AreSame(fallen, revived);
            Object.DestroyImmediate(revive);
        }

        [Test]
        public void Revive_LeavesTheStandingAlone()
        {
            var resolver = new EffectResolver();
            var tracker = new CombatBuffTracker();
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var hurt = new MockCombatUnit("Warrior", 1, 0, 50);
            hurt.Stats.Health = 4;
            var revive = Magic(MagicTargetType.FallenAlly, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 30 });

            Cast(resolver, tracker, revive, cleric, hurt);
            Assert.AreEqual(4, hurt.Stats.Health);
            Object.DestroyImmediate(revive);
        }

        [Test]
        public void Revive_PartyWide_ReachesEveryFallenAllyTheFightKnows()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var a = new MockCombatUnit("A", 1, 0, 40);
            var b = new MockCombatUnit("B", 1, 0, 40);
            a.Stats.Health = 0;
            b.Stats.Health = 0;
            var resolver = new EffectResolver { Events = events, FallenAllies = c => new List<ICombatUnit> { a, b } };
            var phoenix = Magic(MagicTargetType.AllAllies, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 20 });

            Cast(resolver, tracker, phoenix, cleric, cleric);
            Assert.AreEqual(8, a.Stats.Health);
            Assert.AreEqual(8, b.Stats.Health);
            Object.DestroyImmediate(phoenix);
        }

        [Test]
        public void Revive_NeverReachesAHeroGivenToASacrifice()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var victim = new MockCombatUnit("Warrior", 1, 0, 50);
            HealthOps.Set(victim, 0, new HealthSource(cleric, HealthCause.Sacrifice), events);
            var resolver = new EffectResolver { Events = events, FallenAllies = c => new List<ICombatUnit> { victim } };
            var phoenix = Magic(MagicTargetType.AllAllies, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 50 });
            var single = Magic(MagicTargetType.FallenAlly, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 50 });

            Cast(resolver, tracker, phoenix, cleric, cleric);
            Cast(resolver, tracker, single, cleric, victim);
            Assert.AreEqual(0, victim.Stats.Health, "a sacrificed hero is gone, not fallen");
            Assert.IsTrue(events.Sacrificed.Contains(victim));
            Object.DestroyImmediate(phoenix);
            Object.DestroyImmediate(single);
        }

        [Test]
        public void Revive_StandsTheHeroUpClean()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var fallen = new MockCombatUnit("Warrior", 1, 0, 50);
            tracker.ApplyStatusEffect(fallen, BuffType.Silenced, 3);
            tracker.ApplyBuff(fallen, StatType.Strength, -2, 3);
            tracker.SetKeyedBuff(fallen, StatType.Strength, 5, MomentumTracker.BuffKey);
            events.Barriers.Add(fallen, 10, 3);
            HealthOps.Damage(fallen, 70, new HealthSource(null, HealthCause.Attack), events);
            var revive = Magic(MagicTargetType.FallenAlly, new SpellEffect { EffectType = SpellEffectType.Revive, Power = 30 });

            var change = (HealthChange)null;
            events.Subscribe<HealthChange>(c => change = c);
            Cast(resolver, tracker, revive, cleric, fallen);
            Assert.AreEqual(15, fallen.Stats.Health);
            Assert.IsFalse(tracker.HasStatusEffect(fallen, BuffType.Silenced));
            Assert.AreEqual(0, tracker.GetBuffAmount(fallen, StatType.Strength));
            Assert.AreEqual(0, events.Barriers.PointsOf(fallen));
            Assert.AreEqual(15, change.Healed, "from a bar below zero only what lands above zero is healing");
            Object.DestroyImmediate(revive);
        }

        [Test]
        public void Barrier_ATickItSoaksReportsOnlyWhatReachedTheBar()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var hero = new MockCombatUnit("Hero", 1, 0, 50);
            tracker.ApplyOverTime(hero, BuffType.Poisoned, 6, 3);
            events.Barriers.Add(hero, 4, 3);

            var ticks = tracker.ResolveOverTime(hero);
            Assert.AreEqual(48, hero.Stats.Health);
            Assert.AreEqual(1, ticks.Count);
            Assert.AreEqual(2, ticks[0].Amount);
        }

        // ---------------------------------------------------------------- picking one ally (revive targets)

        [Test]
        public void Ultra_AimedAtOneAllyOrOneFallenAlly_AsksForATarget()
        {
            var ultra = ScriptableObject.CreateInstance<UltraSO>();
            ultra.Kind = UltraKind.Strike;
            ultra.TargetType = MagicTargetType.AllEnemies;
            Assert.IsFalse(UltraOps.NeedsTarget(ultra));
            ultra.TargetType = MagicTargetType.FallenAlly;
            Assert.IsTrue(UltraOps.NeedsTarget(ultra));
            ultra.TargetType = MagicTargetType.SingleAlly;
            Assert.IsTrue(UltraOps.NeedsTarget(ultra));
            ultra.Kind = UltraKind.Sacrifice;
            ultra.TargetType = MagicTargetType.AllEnemies;
            Assert.IsTrue(UltraOps.NeedsTarget(ultra));
            Object.DestroyImmediate(ultra);
        }

        [Test]
        public void StrikeTargets_OneAlly_LandsOnTheOnePicked()
        {
            var ultra = ScriptableObject.CreateInstance<UltraSO>();
            ultra.Kind = UltraKind.Strike;
            ultra.TargetType = MagicTargetType.FallenAlly;
            var hero = new MockCombatUnit("Cleric", 1, 0, 20);
            var fallen = new MockCombatUnit("Warrior", 1, 0, 50);
            fallen.Stats.Health = 0;
            var none = new List<ICombatUnit>();

            Assert.AreSame(fallen, UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, none, none, c => 0, fallen)[0]);
            Assert.IsEmpty(UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, none, none, c => 0), "a revive with nobody picked revives nobody");
            ultra.TargetType = MagicTargetType.SingleAlly;
            Assert.AreSame(hero, UltraOps.StrikeTargets<ICombatUnit>(ultra, hero, none, none, c => 0)[0], "an unpicked one-ally Strike lands on the user");
            Object.DestroyImmediate(ultra);
        }

        [Test]
        public void Summon_AimedAtOneFallenAlly_AsksForATarget_OnlyAsASpecialAttack()
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Kind = SummonKind.SpecialAttack;
            summon.TargetType = MagicTargetType.AllAllies;
            Assert.IsFalse(SummonOps.NeedsTarget(summon));
            summon.TargetType = MagicTargetType.FallenAlly;
            Assert.IsTrue(SummonOps.NeedsTarget(summon));
            summon.Kind = SummonKind.ReplaceParty;
            Assert.IsFalse(SummonOps.NeedsTarget(summon));
            Object.DestroyImmediate(summon);
        }

        // ---------------------------------------------------------------- Cure

        [Test]
        public void Cure_ClearsStatusesAndStatCuts_ButNotBuffs()
        {
            var tracker = new CombatBuffTracker();
            var hero = new MockCombatUnit("Hero", 10, 0, 50);
            tracker.ApplyStatusEffect(hero, BuffType.Silenced, 3);
            tracker.ApplyBuff(hero, StatType.Strength, -3, 3);
            tracker.ApplyBuff(hero, StatType.Endurance, 4, 3);

            int cleared = tracker.CureDebuffs(hero, 0);
            Assert.AreEqual(2, cleared);
            Assert.IsFalse(tracker.HasStatusEffect(hero, BuffType.Silenced));
            Assert.AreEqual(0, tracker.GetBuffAmount(hero, StatType.Strength));
            Assert.AreEqual(4, tracker.GetBuffAmount(hero, StatType.Endurance));
        }

        [Test]
        public void Cure_WithACount_ClearsOnlyThatMany()
        {
            var tracker = new CombatBuffTracker();
            var hero = new MockCombatUnit("Hero", 10, 0, 50);
            tracker.ApplyStatusEffect(hero, BuffType.Silenced, 3);
            tracker.ApplyBuff(hero, StatType.Strength, -3, 3);

            Assert.AreEqual(1, tracker.CureDebuffs(hero, 1));
        }

        // ---------------------------------------------------------------- Execute and the bonus vs a kind

        [Test]
        public void Execute_DestroysTheRightKindUnderTheLine_NeverABoss()
        {
            var effect = new SpellEffect { EffectType = SpellEffectType.Execute, Power = 30, BonusVsTrait = UnitTraits.Undead };
            var low = new TraitUnit("Ghoul", 100, UnitTraits.Undead); low.Stats.Health = 30;
            var high = new TraitUnit("Ghoul", 100, UnitTraits.Undead); high.Stats.Health = 31;
            var living = new TraitUnit("Imp", 100, UnitTraits.Demon); living.Stats.Health = 10;
            var boss = new TraitUnit("Lich", 100, UnitTraits.Undead, boss: true); boss.Stats.Health = 10;

            Assert.IsTrue(Assets.Scripts.Cards.Effects.ExecuteEffectExecutor.Destroys(effect, low));
            Assert.IsFalse(Assets.Scripts.Cards.Effects.ExecuteEffectExecutor.Destroys(effect, high));
            Assert.IsFalse(Assets.Scripts.Cards.Effects.ExecuteEffectExecutor.Destroys(effect, living));
            Assert.IsFalse(Assets.Scripts.Cards.Effects.ExecuteEffectExecutor.Destroys(effect, boss));
        }

        [Test]
        public void BonusVsTrait_DoublesTheRawHit_OnlyOnThatKind()
        {
            var resolver = new EffectResolver();
            var tracker = new CombatBuffTracker();
            var cleric = new MockCombatUnit("Cleric", 1, 0, 20);
            var undead = new TraitUnit("Ghoul", 100, UnitTraits.Undead);
            var living = new TraitUnit("Imp", 100, UnitTraits.Demon);
            var purge = Magic(MagicTargetType.AllEnemies, new SpellEffect
            {
                EffectType = SpellEffectType.Damage, Power = 10, PowerMode = PowerMode.Flat, DamageType = DamageType.Holy,
                BonusVsTrait = UnitTraits.Undead, BonusPercent = 100
            });

            Cast(resolver, tracker, purge, cleric, undead, living);
            Assert.AreEqual(80, undead.Stats.Health);
            Assert.AreEqual(90, living.Stats.Health);
            Object.DestroyImmediate(purge);
        }

        // ---------------------------------------------------------------- the authored Cleric

        [Test]
        public void Cleric_StartsOnMend_AndHerGridTeachesHerWholeVision()
        {
            var cleric = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Cleric.asset");
            var grid = cleric.SphereGrid;
            Assert.AreEqual("cleric-sig", grid.StartNodeKey);

            var all = new List<string>();
            foreach (var node in grid.Nodes)
            {
                all.Add(node.Key);
            }
            var known = SphereGridOps.KnownMagicForNodes(grid, all);
            var keys = new List<string>();
            foreach (var k in known)
            {
                keys.Add(k.Key);
            }
            foreach (var ability in new[] { "Mend", "SacredShield", "Renew", "Absolution", "Revive", "Sanctuary", "Inspire", "Quicken",
                         "DivineBolt", "Purge", "SearingLight" })
            {
                CollectionAssert.Contains(keys, ability, ability + " should be on her grid");
            }
            foreach (var ultra in new[] { "MiracleOfLight", "MiracleOfHope", "MiracleOfRighteousness" })
            {
                CollectionAssert.Contains(SphereGridOps.UltrasForNodes(grid, all), ultra);
                Assert.IsNotNull(UltraCatalogSO.Resolve(ultra), ultra + " should be in the Ultra catalog");
            }
            foreach (var summon in new[] { "Phoenix", "GuardianAngel", "Archon" })
            {
                Assert.IsNotNull(SummonCatalogSO.Resolve(summon), summon + " should be in the summon catalog");
            }
        }
    }
}
