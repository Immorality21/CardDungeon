using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The four non-boss archetype presets, driven through <see cref="EnemyActionPlanner"/>.
    ///
    /// <para>These assertions are unchanged from when each archetype was a hard-coded
    /// <c>IEnemyBehavior</c> class — that is the point. Behaviour is authored data now
    /// (<see cref="EnemyBehaviorSO"/>), and the presets have to reproduce the original decisions
    /// exactly or the migration silently changed the game.</para>
    /// </summary>
    public class EnemyBehaviorTests
    {
        private MockCombatUnit _self;
        private MockCombatUnit _hero;
        private CombatBuffTracker _buffTracker;

        [SetUp]
        public void SetUp()
        {
            _self = new MockCombatUnit("Enemy", strength: 5, endurance: 2, health: 30, isHero: false);
            _hero = new MockCombatUnit("Knight", strength: 10, endurance: 5, health: 100);
            _buffTracker = new CombatBuffTracker();
        }

        private EnemyCombatContext Context(
            List<ICombatUnit> heroes = null,
            List<ICombatUnit> allies = null,
            int chargingEntry = EnemyActionPlanner.NoCharge)
        {
            return new EnemyCombatContext
            {
                Heroes = heroes ?? new List<ICombatUnit> { _hero },
                Allies = allies ?? new List<ICombatUnit>(),
                BuffTracker = _buffTracker,
                ChargingEntryIndex = chargingEntry
            };
        }

        /// <summary>Deterministic rolls: every gate passes and the first weighted entry wins.</summary>
        private static EnemyPlanRolls Rolls()
        {
            return new EnemyPlanRolls { Tier = 0f, Target = 0f, Magic = 0f, Fallback = 0f };
        }

        private EnemyDecision Plan(EnemyArchetype archetype, EnemyCombatContext context)
        {
            return EnemyActionPlanner.Plan(
                _self, context, EnemyBehaviorSO.BuiltInPreset(archetype), Rolls());
        }

        // ------------------------------------------------------------------ Aggressor

        [Test]
        public void Aggressor_Attacks_ALivingHero()
        {
            var decision = Plan(EnemyArchetype.Aggressor, Context());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreSame(_hero, decision.Target);
        }

        // ------------------------------------------------------------------ Bruiser

        [Test]
        public void Bruiser_NotCharging_Charges()
        {
            var decision = Plan(EnemyArchetype.Bruiser, Context());

            Assert.AreEqual(EnemyActionType.ChargeHeavy, decision.Type);
            Assert.AreSame(_hero, decision.Target);
            Assert.GreaterOrEqual(decision.EntryIndex, 0,
                "The charge has to record which action it is winding up, or the payload turn cannot know.");
        }

        [Test]
        public void Bruiser_Charging_HeavyAttackWithMultiplier()
        {
            // Wind up first so the charge index is the one the preset actually used.
            var opening = Plan(EnemyArchetype.Bruiser, Context());
            var decision = Plan(EnemyArchetype.Bruiser, Context(chargingEntry: opening.EntryIndex));

            Assert.AreEqual(EnemyActionType.HeavyAttack, decision.Type);
            Assert.Greater(decision.Multiplier, 1f);
        }

        [Test]
        public void Bruiser_HeavyIs2Point5x_TheOriginalConstant()
        {
            var opening = Plan(EnemyArchetype.Bruiser, Context());
            var decision = Plan(EnemyArchetype.Bruiser, Context(chargingEntry: opening.EntryIndex));

            Assert.AreEqual(2.5f, decision.Multiplier, 0.0001f);
        }

        // ------------------------------------------------------------------ Healer

        [Test]
        public void Healer_HealsMostWoundedAlly()
        {
            var hurt = new MockCombatUnit("Goblin", strength: 4, endurance: 1, health: 20, isHero: false);
            hurt.Stats.Health = 5; // wounded (of a 20 bar) - Health, not the MaxHealth stat
            var healthy = new MockCombatUnit("Orc", strength: 6, endurance: 2, health: 25, isHero: false);

            var decision = Plan(
                EnemyArchetype.Healer, Context(allies: new List<ICombatUnit> { hurt, healthy }));

            Assert.AreEqual(EnemyActionType.Heal, decision.Type);
            Assert.AreSame(hurt, decision.Target);
            Assert.Greater(decision.Amount, 0);
        }

        [Test]
        public void Healer_NoWounded_Attacks()
        {
            var fullAlly = new MockCombatUnit("Orc", strength: 6, endurance: 2, health: 25, isHero: false);

            var decision = Plan(
                EnemyArchetype.Healer, Context(allies: new List<ICombatUnit> { fullAlly }));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Healer_SelfWounded_NoAllies_HealsSelf()
        {
            _self.Stats.Health = 10; // wounded, MaxHealth 30

            var decision = Plan(EnemyArchetype.Healer, Context());

            Assert.AreEqual(EnemyActionType.Heal, decision.Type);
            Assert.AreSame(_self, decision.Target);
        }

        [Test]
        public void Healer_HealsFor8_TheOriginalConstant()
        {
            _self.Stats.Health = 10;

            var decision = Plan(EnemyArchetype.Healer, Context());

            Assert.AreEqual(8, decision.Amount);
        }

        // ------------------------------------------------------------------ Debuffer

        [Test]
        public void Debuffer_WeakensUndebuffedHero()
        {
            var decision = Plan(EnemyArchetype.Debuffer, Context());

            Assert.AreEqual(EnemyActionType.Debuff, decision.Type);
            Assert.AreSame(_hero, decision.Target);
            Assert.AreEqual(StatType.Strength, decision.DebuffStat);
            Assert.Greater(decision.Amount, 0);
        }

        [Test]
        public void Debuffer_AllHeroesAlreadyWeakened_Attacks()
        {
            _buffTracker.ApplyBuff(_hero, StatType.Strength, -2, 3); // already weakened

            var decision = Plan(EnemyArchetype.Debuffer, Context());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Debuffer_Applies3For3Turns_TheOriginalConstants()
        {
            var decision = Plan(EnemyArchetype.Debuffer, Context());

            Assert.AreEqual(3, decision.Amount);
            Assert.AreEqual(3, decision.Duration);
        }

        // ------------------------------------------------------------------ the new machinery

        [Test]
        public void EveryPresetActsWhenThePartyIsAlive()
        {
            // A half-authored or fully gated behaviour must still take a turn; the planner falls back
            // to a swing rather than wasting it.
            foreach (EnemyArchetype archetype in System.Enum.GetValues(typeof(EnemyArchetype)))
            {
                var decision = Plan(archetype, Context());
                Assert.IsNotNull(decision, archetype + " produced no decision");
            }
        }

        [Test]
        public void EmptyBehavior_StillSwings()
        {
            var empty = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            try
            {
                var decision = EnemyActionPlanner.Plan(_self, Context(), empty, Rolls());

                Assert.AreEqual(EnemyActionType.Attack, decision.Type);
                Assert.AreSame(_hero, decision.Target);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void ChanceGate_GatesTheEntryWithoutBlockingTheTurn()
        {
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            try
            {
                behavior.Actions = new List<EnemyActionEntry>
                {
                    new EnemyActionEntry
                    {
                        Kind = EnemyActionKind.Debuff,
                        Priority = 20,
                        ChanceGate = 0.25f,
                        Power = 3
                    },
                    new EnemyActionEntry { Kind = EnemyActionKind.Attack }
                };

                var passes = new EnemyPlanRolls { Gates = new[] { 0.10f, 0f }, Tier = 0f };
                var fails = new EnemyPlanRolls { Gates = new[] { 0.90f, 0f }, Tier = 0f };

                Assert.AreEqual(EnemyActionType.Debuff,
                    EnemyActionPlanner.Plan(_self, Context(), behavior, passes).Type,
                    "A roll under the gate should let the gated entry pre-empt.");
                Assert.AreEqual(EnemyActionType.Attack,
                    EnemyActionPlanner.Plan(_self, Context(), behavior, fails).Type,
                    "A roll over the gate should fall through to the lower tier, not waste the turn.");
            }
            finally
            {
                Object.DestroyImmediate(behavior);
            }
        }

        [Test]
        public void HigherPriorityWinsOutright_RegardlessOfWeight()
        {
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            try
            {
                behavior.Actions = new List<EnemyActionEntry>
                {
                    new EnemyActionEntry { Kind = EnemyActionKind.Attack, Priority = 0, Weight = 99f },
                    new EnemyActionEntry { Kind = EnemyActionKind.Debuff, Priority = 5, Weight = 0.01f, Power = 3 }
                };

                var decision = EnemyActionPlanner.Plan(_self, Context(), behavior, Rolls());

                Assert.AreEqual(EnemyActionType.Debuff, decision.Type,
                    "Priority is a tier, not a tie-break: a heavier weight below it must not win.");
            }
            finally
            {
                Object.DestroyImmediate(behavior);
            }
        }

        [Test]
        public void AnActionWithNowhereToLandIsNotChosen()
        {
            // A Heal with nobody wounded must not win a turn and then do nothing.
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            try
            {
                behavior.Actions = new List<EnemyActionEntry>
                {
                    new EnemyActionEntry { Kind = EnemyActionKind.Heal, Priority = 10, Power = 8 },
                    new EnemyActionEntry { Kind = EnemyActionKind.Attack }
                };

                var decision = EnemyActionPlanner.Plan(_self, Context(), behavior, Rolls());

                Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            }
            finally
            {
                Object.DestroyImmediate(behavior);
            }
        }
    }
}
