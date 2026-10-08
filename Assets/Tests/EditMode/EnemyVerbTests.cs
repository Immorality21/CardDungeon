using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The three enemy verbs of COMBAT_DEPTH §12 - BuffAlly, Guard and Summon - through the planner, the
    /// guard table, the simulator's call-in and the closed-form model.
    /// </summary>
    public class EnemyVerbTests
    {
        private MockCombatUnit _self;
        private MockCombatUnit _hero;
        private CombatBuffTracker _buffTracker;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _self = new MockCombatUnit("Shaman", strength: 3, endurance: 2, health: 20, isHero: false);
            _hero = new MockCombatUnit("Knight", strength: 10, endurance: 5, health: 100);
            _buffTracker = new CombatBuffTracker();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        private static EnemyPlanRolls Rolls()
        {
            return new EnemyPlanRolls { Tier = 0f, Target = 0f, Magic = 0f, Fallback = 0f };
        }

        private EnemyBehaviorSO Behavior(params EnemyActionEntry[] actions)
        {
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            _created.Add(behavior);
            behavior.Actions = new List<EnemyActionEntry>(actions);
            return behavior;
        }

        private EnemySO Enemy(string name, int strength = 4, int health = 12, EnemyBehaviorSO behavior = null)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(enemy);
            enemy.DisplayName = name;
            enemy.BaseStats[StatType.Strength] = strength;
            enemy.BaseStats[StatType.Endurance] = 2;
            enemy.BaseStats[StatType.MaxHealth] = health;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.XpReward = 5;
            enemy.GoldReward = 5;
            enemy.Behavior = behavior;
            return enemy;
        }

        private EnemyCombatContext Context(List<ICombatUnit> allies = null, GuardTable guards = null,
            int openSlots = 3, Dictionary<int, int> uses = null)
        {
            return new EnemyCombatContext
            {
                Heroes = new List<ICombatUnit> { _hero },
                Allies = allies ?? new List<ICombatUnit>(),
                BuffTracker = _buffTracker,
                Guards = guards,
                OpenSlots = openSlots,
                ActionUses = uses
            };
        }

        // ------------------------------------------------------------------ BuffAlly

        private static EnemyActionEntry BuffStrength()
        {
            return new EnemyActionEntry
            {
                Kind = EnemyActionKind.BuffAlly, Priority = 1, Power = 3, Duration = 3,
                TargetStat = StatType.Strength
            };
        }

        [Test]
        public void BuffAlly_LandsOnTheHardestHitter()
        {
            var brute = new MockCombatUnit("Brute", strength: 9, endurance: 2, health: 40, isHero: false);
            var imp = new MockCombatUnit("Imp", strength: 4, endurance: 2, health: 10, isHero: false);

            var decision = EnemyActionPlanner.Plan(_self, Context(new List<ICombatUnit> { imp, brute }),
                Behavior(BuffStrength()), Rolls());

            Assert.AreEqual(EnemyActionType.BuffAlly, decision.Type);
            Assert.AreSame(brute, decision.Target);
            Assert.AreEqual(3, decision.Amount);
            Assert.AreEqual(StatType.Strength, decision.DebuffStat);
        }

        [Test]
        public void BuffAlly_SkipsAnAllyAlreadyBuffed()
        {
            var brute = new MockCombatUnit("Brute", strength: 9, endurance: 2, health: 40, isHero: false);
            var imp = new MockCombatUnit("Imp", strength: 4, endurance: 2, health: 10, isHero: false);
            _buffTracker.ApplyBuff(brute, StatType.Strength, 2, 3);

            var decision = EnemyActionPlanner.Plan(_self, Context(new List<ICombatUnit> { imp, brute }),
                Behavior(BuffStrength()), Rolls());

            Assert.AreSame(imp, decision.Target);
        }

        [Test]
        public void BuffAlly_Alone_BuffsItself()
        {
            var decision = EnemyActionPlanner.Plan(_self, Context(), Behavior(BuffStrength()), Rolls());

            Assert.AreEqual(EnemyActionType.BuffAlly, decision.Type);
            Assert.AreSame(_self, decision.Target);
        }

        [Test]
        public void BuffAlly_EveryoneBuffed_FallsThroughToASwing()
        {
            _buffTracker.ApplyBuff(_self, StatType.Strength, 2, 3);

            var decision = EnemyActionPlanner.Plan(_self, Context(), Behavior(BuffStrength()), Rolls());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        // ------------------------------------------------------------------ Guard

        private static EnemyActionEntry Guard(int brace = 0)
        {
            return new EnemyActionEntry { Kind = EnemyActionKind.Guard, Priority = 1, Power = brace, Duration = 2 };
        }

        [Test]
        public void Guard_CoversTheMostWoundedAlly()
        {
            var healthy = new MockCombatUnit("Healthy", strength: 4, endurance: 2, health: 20, isHero: false);
            var hurt = new MockCombatUnit("Hurt", strength: 4, endurance: 2, health: 20, isHero: false);
            hurt.Stats.Health = 5;

            var decision = EnemyActionPlanner.Plan(_self, Context(new List<ICombatUnit> { healthy, hurt }),
                Behavior(Guard()), Rolls());

            Assert.AreEqual(EnemyActionType.Guard, decision.Type);
            Assert.AreSame(hurt, decision.Target);
        }

        [Test]
        public void Guard_AllWhole_CoversTheFrailest()
        {
            var big = new MockCombatUnit("Big", strength: 4, endurance: 2, health: 40, isHero: false);
            var small = new MockCombatUnit("Small", strength: 4, endurance: 2, health: 8, isHero: false);

            var decision = EnemyActionPlanner.Plan(_self, Context(new List<ICombatUnit> { big, small }),
                Behavior(Guard()), Rolls());

            Assert.AreSame(small, decision.Target);
        }

        [Test]
        public void Guard_Alone_FallsThroughToASwing()
        {
            var decision = EnemyActionPlanner.Plan(_self, Context(), Behavior(Guard()), Rolls());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Guard_SkipsAnAllySomeoneAlreadyCovers()
        {
            var otherGuard = new MockCombatUnit("Other", strength: 4, endurance: 2, health: 20, isHero: false);
            var ward = new MockCombatUnit("Ward", strength: 4, endurance: 2, health: 8, isHero: false);
            var guards = new GuardTable();
            guards.Cover(ward, otherGuard);

            var decision = EnemyActionPlanner.Plan(_self,
                Context(new List<ICombatUnit> { otherGuard, ward }, guards), Behavior(Guard()), Rolls());

            Assert.AreSame(otherGuard, decision.Target);
        }

        // ------------------------------------------------------------------ the guard table

        [Test]
        public void Cover_RedirectsSingleTargetBlows_ButNotAreaHits()
        {
            var guard = new MockCombatUnit("Guard", strength: 4, endurance: 2, health: 20, isHero: false);
            var ward = new MockCombatUnit("Ward", strength: 4, endurance: 2, health: 8, isHero: false);
            var table = new GuardTable();
            table.Cover(ward, guard);

            Assert.AreSame(guard, table.Redirect(ward));
            Assert.AreSame(ward, table.RedirectAreaHit(ward));
            Assert.IsTrue(table.IsCovered(ward));
            Assert.IsTrue(table.IsCovering(guard));
        }

        [Test]
        public void FullGuard_StillTakesAreaHits()
        {
            var mech = new MockCombatUnit("Mech", strength: 4, endurance: 2, health: 30);
            var rider = new MockCombatUnit("Rider", strength: 4, endurance: 2, health: 20);
            var table = new GuardTable();
            table.Set(rider, mech);

            Assert.AreSame(mech, table.RedirectAreaHit(rider));
            Assert.IsFalse(table.IsCovered(rider));
        }

        [Test]
        public void Cover_EndsWhenTheGuardsTurnStarts_AFullGuardDoesNot()
        {
            var events = new CombatEvents();
            var guard = new MockCombatUnit("Guard", strength: 4, endurance: 2, health: 20, isHero: false);
            var ward = new MockCombatUnit("Ward", strength: 4, endurance: 2, health: 8, isHero: false);
            var mech = new MockCombatUnit("Mech", strength: 4, endurance: 2, health: 30);
            var rider = new MockCombatUnit("Rider", strength: 4, endurance: 2, health: 20);
            events.Guards.Cover(ward, guard);
            events.Guards.Set(rider, mech);

            events.Publish(new TurnStarted { Unit = ward });
            Assert.AreSame(guard, events.Guards.GuardOf(ward), "only the guard's own turn ends its cover");

            events.Publish(new TurnStarted { Unit = guard });
            events.Publish(new TurnStarted { Unit = mech });

            Assert.IsNull(events.Guards.GuardOf(ward));
            Assert.AreSame(mech, events.Guards.GuardOf(rider));
        }

        [Test]
        public void Cover_FallenGuard_CoversNobody()
        {
            var guard = new MockCombatUnit("Guard", strength: 4, endurance: 2, health: 20, isHero: false);
            var ward = new MockCombatUnit("Ward", strength: 4, endurance: 2, health: 8, isHero: false);
            var table = new GuardTable();
            table.Cover(ward, guard);
            guard.Stats.Health = 0;

            Assert.AreSame(ward, table.Redirect(ward));
            Assert.IsFalse(table.IsCovered(ward));
        }

        // ------------------------------------------------------------------ Summon

        private EnemyActionEntry Summon(EnemySO what, int count = 1, int maxUses = 1)
        {
            return new EnemyActionEntry
            {
                Kind = EnemyActionKind.Summon, Priority = 1, Summons = what, SummonCount = count, MaxUses = maxUses
            };
        }

        [Test]
        public void Summon_WithRoom_CallsItsBodies()
        {
            var imp = Enemy("Imp");

            var decision = EnemyActionPlanner.Plan(_self, Context(openSlots: 3), Behavior(Summon(imp, count: 2)), Rolls());

            Assert.AreEqual(EnemyActionType.Summon, decision.Type);
            Assert.AreSame(imp, decision.SummonDefinition);
            Assert.AreEqual(2, decision.SummonCount);
            Assert.AreEqual(0, decision.EntryIndex);
        }

        [Test]
        public void Summon_CountIsCappedByTheRoomLeft()
        {
            var decision = EnemyActionPlanner.Plan(_self, Context(openSlots: 1),
                Behavior(Summon(Enemy("Imp"), count: 3)), Rolls());

            Assert.AreEqual(1, decision.SummonCount);
        }

        [Test]
        public void Summon_StageFull_FallsThroughToASwing()
        {
            var decision = EnemyActionPlanner.Plan(_self, Context(openSlots: 0),
                Behavior(Summon(Enemy("Imp"))), Rolls());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Summon_UsesSpent_FallsThroughToASwing()
        {
            var uses = new Dictionary<int, int> { { 0, 1 } };

            var decision = EnemyActionPlanner.Plan(_self, Context(uses: uses),
                Behavior(Summon(Enemy("Imp"), maxUses: 1)), Rolls());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Summon_NoLimit_KeepsCallingWhileThereIsRoom()
        {
            var uses = new Dictionary<int, int> { { 0, 9 } };

            var decision = EnemyActionPlanner.Plan(_self, Context(uses: uses),
                Behavior(Summon(Enemy("Imp"), maxUses: 0)), Rolls());

            Assert.AreEqual(EnemyActionType.Summon, decision.Type);
        }

        [Test]
        public void PredictCertain_NamesTheNewVerbs()
        {
            var brute = new MockCombatUnit("Brute", strength: 9, endurance: 2, health: 40, isHero: false);
            var allies = new List<ICombatUnit> { brute };

            Assert.AreEqual(EnemyActionType.BuffAlly,
                EnemyActionPlanner.PredictCertain(_self, Context(allies), Behavior(BuffStrength())));
            Assert.AreEqual(EnemyActionType.Guard,
                EnemyActionPlanner.PredictCertain(_self, Context(allies), Behavior(Guard())));
            Assert.AreEqual(EnemyActionType.Summon,
                EnemyActionPlanner.PredictCertain(_self, Context(allies), Behavior(Summon(Enemy("Imp")))));
        }

        // ------------------------------------------------------------------ the simulator

        [Test]
        public void SimCallIn_AddsSummonedBodies_UpToTheStage()
        {
            var imp = Enemy("Imp");
            var summoner = SimUnit.FromEnemy(Enemy("Caller"));
            var enemies = new List<SimUnit> { summoner, SimUnit.FromEnemy(imp), SimUnit.FromEnemy(imp) };
            var decision = new EnemyDecision
            {
                Type = EnemyActionType.Summon, SummonDefinition = imp, SummonCount = 5, EntryIndex = 2
            };

            int called = EncounterSimulator.CallIn(summoner, decision, enemies, new TurnManager());

            Assert.AreEqual(EnemyFormation.DesignMax - 3, called);
            Assert.AreEqual(EnemyFormation.DesignMax, enemies.Count);
            Assert.IsTrue(enemies[4].IsSummoned);
            Assert.IsFalse(enemies[1].IsSummoned);
            Assert.AreEqual(1, summoner.ActionUses[2]);
        }

        // ------------------------------------------------------------------ the closed form

        [Test]
        public void ExpectedSummons_IsCappedByMaxUses()
        {
            var imp = Enemy("Imp");
            var behavior = Behavior(Summon(imp, count: 2, maxUses: 1));

            var calls = EnemyBehaviorModel.ExpectedSummons(behavior);

            Assert.AreEqual(1, calls.Count);
            Assert.AreSame(imp, calls[0].Key);
            Assert.AreEqual(2f, calls[0].Value, 0.001f, "one use of two bodies");
        }

        [Test]
        public void WeightedGroup_PricesCalledBodies_ButPaysAndPlacesNothingForThem()
        {
            var imp = Enemy("Imp");
            var caller = Enemy("Caller", behavior: Behavior(Summon(imp, count: 1, maxUses: 1)));
            var group = new WeightedEnemyGroup();

            group.Add(caller, 1f);

            Assert.AreEqual(2f, group.TotalCount, 0.001f, "the caller and the body it calls");
            Assert.AreEqual(1f, group.PlacedCount, 0.001f);
            Assert.AreEqual(5f, group.ExpectedXp, 0.001f, "only the caller pays");
            var units = group.ToDiscreteUnits();
            Assert.AreEqual(1, units.Count, "the simulator calls the body in itself");
            Assert.AreEqual("Caller", units[0].DisplayName);
        }

        [Test]
        public void BuffAllyAndBrace_ArePricedAsShiftsOnTheEnemySide()
        {
            var behavior = Behavior(BuffStrength(), Guard(brace: 4));

            var profile = EnemyBehaviorModel.Profile(behavior, 2, 0f, 5f);

            Assert.IsTrue(profile.StatShifts.Exists(s => s.Stat == StatType.Strength && !s.OnHeroSide));
            Assert.IsTrue(profile.StatShifts.Exists(s => s.Stat == StatType.Endurance && !s.OnHeroSide));
            Assert.Greater(profile.IdleShare, 0.99f, "neither verb lands damage on its turn");
        }
    }
}
