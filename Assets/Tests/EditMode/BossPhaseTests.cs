using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The two pieces a telegraphed boss is built from: a spell wound up for a turn and named before it
    /// lands, and a phase change that fires once as the boss's health crosses a line.
    /// </summary>
    public class BossPhaseTests
    {
        private class Bearer : MockCombatUnit, ITriggerSource
        {
            public readonly List<CarriedTrigger> Carried = new List<CarriedTrigger>();

            public Bearer(string name, int strength, int health, bool isHero)
                : base(name, strength, 0, health, isHero: isHero)
            {
            }

            public IEnumerable<CarriedTrigger> GetTriggers()
            {
                return Carried;
            }
        }

        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        private MagicSO Spell(string name, MagicTargetType target)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            _created.Add(magic);
            magic.Key = name;
            magic.DisplayName = name;
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 10, ScalingStat = StatType.None }
            };
            return magic;
        }

        private static EnemyCombatContext Context(ICombatUnit hero, int charging = EnemyActionPlanner.NoCharge)
        {
            return new EnemyCombatContext
            {
                Heroes = new List<ICombatUnit> { hero },
                Allies = new List<ICombatUnit>(),
                BuffTracker = new CombatBuffTracker(),
                ChargingEntryIndex = charging
            };
        }

        private static readonly EnemyPlanRolls Rolls = new EnemyPlanRolls { Tier = 0f, Target = 0f, Magic = 0f, Fallback = 0f };

        [Test]
        public void TelegraphedCast_WindsUpNamingTheSpell_ThenCastsIt()
        {
            var boss = new MockCombatUnit("Boss", 10, 4, 100, isHero: false);
            var hero = new MockCombatUnit("Knight", 10, 4, 100);
            var requiem = Spell("Requiem", MagicTargetType.AllEnemies);
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            _created.Add(behavior);
            behavior.Actions = new List<EnemyActionEntry>
            {
                new EnemyActionEntry { Kind = EnemyActionKind.CastMagic, Magic = requiem, Telegraphed = true }
            };

            var windUp = EnemyActionPlanner.Plan(boss, Context(hero), behavior, Rolls);
            var landing = EnemyActionPlanner.Plan(boss, Context(hero, charging: windUp.EntryIndex), behavior, Rolls);

            Assert.AreEqual(EnemyActionType.ChargeCast, windUp.Type);
            Assert.AreEqual("Requiem", EnemyActionPlanner.AnnouncedName(windUp, behavior.Actions), "the wind-up names it");
            Assert.AreEqual(EnemyActionType.CastMagic, landing.Type);
            Assert.AreSame(requiem, landing.Magic);
            Assert.AreEqual(EnemyActionType.CastMagic,
                EnemyActionPlanner.PredictCertain(boss, Context(hero, charging: 0), behavior), "the intent icon is certain");
        }

        [Test]
        public void TelegraphedCast_WithNoNamedSpell_DoesNotWindUp()
        {
            var entry = new EnemyActionEntry { Kind = EnemyActionKind.CastMagic, Telegraphed = true };

            Assert.IsFalse(entry.IsTelegraphed, "a wind-up must name what is coming");
        }

        [Test]
        public void Summon_CallsEveryGroup_InOrder()
        {
            var skeleton = ScriptableObject.CreateInstance<EnemySO>();
            var golem = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(skeleton);
            _created.Add(golem);
            var decision = new EnemyDecision
            {
                Type = EnemyActionType.Summon, SummonDefinition = skeleton, SummonCount = 3,
                AlsoSummons = new List<SummonGroup> { new SummonGroup { Enemy = golem, Count = 1 } }
            };

            CollectionAssert.AreEqual(new[] { skeleton, skeleton, skeleton, golem }, decision.SummonedBodies().ToList());
        }

        [Test]
        public void ShieldedBoss_CannotBeTargeted_WhileWhatItCalledStands()
        {
            var bossDefinition = ScriptableObject.CreateInstance<EnemySO>();
            bossDefinition.ShieldedBySummons = true;
            var guardDefinition = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(bossDefinition);
            _created.Add(guardDefinition);
            var boss = new Assets.Scripts.Balance.SimUnit { Definition = bossDefinition, Stats = new Stats { Health = 100 } };
            var guard = new Assets.Scripts.Balance.SimUnit { Definition = guardDefinition, IsSummoned = true, Stats = new Stats { Health = 10 } };
            var side = new List<ICombatUnit> { boss, guard };

            CollectionAssert.AreEqual(new ICombatUnit[] { guard }, SummonShield.Targetable(side), "behind his guard");

            guard.Stats.Health = 0;
            CollectionAssert.Contains(SummonShield.Targetable(side), boss, "exposed once it falls");
        }

        [Test]
        public void OnHealthBelow_FiresOnceAsTheLineIsCrossed()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var boss = new Bearer("Boss", 10, 100, isHero: false);
            var hero = new Bearer("Knight", 10, 100, isHero: true);
            var phase = new TriggeredEffect
            {
                When = TriggerKind.OnHealthBelow,
                HealthBelow = 0.5f,
                Limit = TriggerLimit.OncePerCombat,
                Target = TriggerTarget.Bearer,
                Effects = new List<SpellEffect>
                {
                    new SpellEffect { EffectType = SpellEffectType.Buff, BuffType = BuffType.Strength, Power = 3,
                        PowerMode = PowerMode.Flat, ScalingStat = StatType.None, Duration = 99 }
                }
            };
            boss.Carried.Add(new CarriedTrigger(phase, "test"));
            var fired = new List<ReactionResolved>();
            new TriggerRegistry(events, resolver, tracker, () => new List<ICombatUnit> { boss, hero }, () => 0f);
            events.Subscribe<ReactionResolved>(fired.Add, TriggerRegistry.Order + 1);

            HealthOps.Damage(boss, 30, new HealthSource(hero, HealthCause.Attack), events);
            Assert.AreEqual(0, fired.Count, "70% is above the line");

            HealthOps.Damage(boss, 25, new HealthSource(hero, HealthCause.Attack), events);
            Assert.AreEqual(1, fired.Count, "70% to 45% crosses it");
            Assert.AreEqual(3, tracker.GetBuffAmount(boss, StatType.Strength));

            HealthOps.Damage(boss, 10, new HealthSource(hero, HealthCause.Attack), events);
            Assert.AreEqual(1, fired.Count, "already below: it does not fire again");
        }
    }
}
