using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Tinkerer's mechanics (docs/plans/SPECIALIZATION.md, "The Tinkerer"): Disassemble's odds and
    /// what a success does, the guard table a mech shields its rider with, the turn clock's follower
    /// link that makes a mech act right after its rider, and the balance model's mirror of mounting.
    /// </summary>
    public class TinkererMechanicsTests
    {
        private const float Tolerance = 1e-4f;

        /// <summary>A mock unit with traits, for the checks that read them.</summary>
        private class TraitUnit : MockCombatUnit, IHasTraits
        {
            public TraitUnit(string name, UnitTraits traits, int health = 30, bool boss = false)
                : base(name, strength: 5, endurance: 2, health: health, agility: 10)
            {
                Traits = traits;
                IsBoss = boss;
            }

            public UnitTraits Traits { get; }
            public bool IsBoss { get; }
        }

        private static SimUnit Sim(string name, int strength, int health, int agility)
        {
            return new SimUnit
            {
                DisplayName = name,
                Stats = TestStats.Make(strength, 0, health, agility),
                Effective = TestStats.Block(strength, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = strength,
                Resistances = new List<Resistance>()
            };
        }

        private static MagicSO DisassembleMagic()
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test-disassemble";
            magic.TargetType = MagicTargetType.SingleEnemy;
            magic.Effects = new List<SpellEffect> { new SpellEffect { EffectType = SpellEffectType.Disassemble } };
            return magic;
        }

        private static MagicSO DamageMagic(MagicTargetType target, int power)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test-damage";
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = power, PowerMode = PowerMode.Flat }
            };
            return magic;
        }

        // ------------------------------------------------------------------ Disassemble's odds

        [Test]
        public void Chance_StartsAtAQuarter_AndGrowsFivePointsAKill()
        {
            Assert.AreEqual(0.25f, DisassembleOps.Chance(0, isBoss: false), Tolerance);
            Assert.AreEqual(0.50f, DisassembleOps.Chance(5, isBoss: false), Tolerance);
        }

        [Test]
        public void Chance_IsNeverCertain_AndHalvedOnABoss()
        {
            Assert.AreEqual(0.9f, DisassembleOps.Chance(13, isBoss: false), Tolerance);
            Assert.AreEqual(0.9f, DisassembleOps.Chance(500, isBoss: false), Tolerance);
            Assert.AreEqual(0.45f, DisassembleOps.Chance(13, isBoss: true), Tolerance);
        }

        [Test]
        public void OnlyMachines_CanBeTargeted()
        {
            Assert.IsTrue(DisassembleOps.CanTarget(new TraitUnit("Sentry", UnitTraits.Mechanical)));
            Assert.IsTrue(DisassembleOps.CanTarget(new TraitUnit("Flying sentry", UnitTraits.Mechanical | UnitTraits.Flying)));
            Assert.IsFalse(DisassembleOps.CanTarget(new TraitUnit("Golem", UnitTraits.Construct)));
            Assert.IsFalse(DisassembleOps.CanTarget(new MockCombatUnit("No traits", strength: 1, endurance: 1, health: 10, agility: 10)));
        }

        // ------------------------------------------------------------------ Disassemble's effect

        [Test]
        public void Disassemble_ASuccess_TakesTheMachineDown_CreditedToTheCaster()
        {
            var tinkerer = new MockCombatUnit("Tinkerer", strength: 1, endurance: 1, health: 20, agility: 10);
            var sentry = new TraitUnit("Sentry", UnitTraits.Mechanical);
            var events = new CombatEvents();
            var deaths = new List<UnitDefeated>();
            events.Subscribe<UnitDefeated>(deaths.Add);
            var resolver = new EffectResolver { Events = events, Roll = () => 0.2f };

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = DisassembleMagic(), Caster = tinkerer, Targets = new List<ICombatUnit> { sentry }
            }, new CombatBuffTracker());

            Assert.IsFalse(sentry.IsAlive);
            Assert.AreEqual(1, deaths.Count);
            Assert.AreSame(tinkerer, deaths[0].Killer);
            Assert.AreEqual(HealthCause.Disassemble, deaths[0].Cause, "the cause is what pays the salvage");
            Assert.AreEqual("Disassembled!", result.Entries.Single().Text);
        }

        [Test]
        public void Disassemble_AFailedRoll_LeavesTheMachineUntouched()
        {
            var sentry = new TraitUnit("Sentry", UnitTraits.Mechanical);
            var resolver = new EffectResolver { Roll = () => 0.25f };   // the chance is 0.25: not below it

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = DisassembleMagic(),
                Caster = new MockCombatUnit("Tinkerer", strength: 1, endurance: 1, health: 20, agility: 10),
                Targets = new List<ICombatUnit> { sentry }
            }, new CombatBuffTracker());

            Assert.AreEqual(30, sentry.Stats.Health);
            Assert.AreEqual("Failed (25%)", result.Entries.Single().Text);
        }

        [Test]
        public void Disassemble_KnowingAMachine_RaisesTheOdds()
        {
            var sentry = new TraitUnit("Sentry", UnitTraits.Mechanical);
            var resolver = new EffectResolver { Roll = () => 0.6f, KillsOf = unit => 10 };   // 0.25 + 10 * 0.05 = 0.75

            resolver.Execute(new SpellcastAction
            {
                Magic = DisassembleMagic(),
                Caster = new MockCombatUnit("Tinkerer", strength: 1, endurance: 1, health: 20, agility: 10),
                Targets = new List<ICombatUnit> { sentry }
            }, new CombatBuffTracker());

            Assert.IsFalse(sentry.IsAlive);
        }

        [Test]
        public void Disassemble_SomethingNotAMachine_IsNeverTouched()
        {
            var golem = new TraitUnit("Golem", UnitTraits.Construct);
            var resolver = new EffectResolver { Roll = () => 0f };

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = DisassembleMagic(),
                Caster = new MockCombatUnit("Tinkerer", strength: 1, endurance: 1, health: 20, agility: 10),
                Targets = new List<ICombatUnit> { golem }
            }, new CombatBuffTracker());

            Assert.AreEqual(30, golem.Stats.Health);
            Assert.AreEqual("Not a machine", result.Entries.Single().Text);
        }

        // ------------------------------------------------------------------ the mech's shield

        [Test]
        public void Guards_RedirectWhileTheGuardStands_AndStopWhenItFalls()
        {
            var rider = new MockCombatUnit("Rider", strength: 1, endurance: 1, health: 20, agility: 10);
            var mech = new MockCombatUnit("Mech", strength: 1, endurance: 1, health: 20, agility: 10);
            var guards = new GuardTable();
            guards.Set(rider, mech);

            Assert.AreSame(mech, guards.Redirect(rider));
            Assert.AreSame(mech, guards.Redirect(mech), "the guard itself is struck as itself");

            mech.Stats.Health = 0;
            Assert.AreSame(rider, guards.Redirect(rider), "a broken mech shields nobody");

            mech.Stats.Health = 20;
            guards.ClearGuard(mech);
            Assert.AreSame(rider, guards.Redirect(rider));
        }

        [Test]
        public void AbilityDamage_AimedAtTheRider_LandsOnTheMech()
        {
            var rider = new MockCombatUnit("Rider", strength: 1, endurance: 0, health: 20, agility: 10);
            var mech = new MockCombatUnit("Mech", strength: 1, endurance: 0, health: 40, agility: 10);
            var events = new CombatEvents();
            events.Guards.Set(rider, mech);
            var resolver = new EffectResolver { Events = events };

            resolver.Execute(new SpellcastAction
            {
                Magic = DamageMagic(MagicTargetType.SingleEnemy, 10),
                Caster = new MockCombatUnit("Enemy", strength: 1, endurance: 1, health: 20, agility: 10),
                Targets = new List<ICombatUnit> { rider }
            }, new CombatBuffTracker());

            Assert.AreEqual(20, rider.Stats.Health);
            Assert.AreEqual(30, mech.Stats.Health);
        }

        [Test]
        public void AreaDamage_ReachingRiderAndMech_HitsTheMechOnce()
        {
            var rider = new MockCombatUnit("Rider", strength: 1, endurance: 0, health: 20, agility: 10);
            var mech = new MockCombatUnit("Mech", strength: 1, endurance: 0, health: 40, agility: 10);
            var events = new CombatEvents();
            events.Guards.Set(rider, mech);
            var resolver = new EffectResolver { Events = events };

            resolver.Execute(new SpellcastAction
            {
                Magic = DamageMagic(MagicTargetType.AllEnemies, 10),
                Caster = new MockCombatUnit("Enemy", strength: 1, endurance: 1, health: 20, agility: 10),
                Targets = new List<ICombatUnit> { rider, mech }
            }, new CombatBuffTracker());

            Assert.AreEqual(20, rider.Stats.Health);
            Assert.AreEqual(30, mech.Stats.Health);
        }

        [Test]
        public void SimulatedAttack_AimedAtTheRider_LandsOnTheMech()
        {
            var rider = Sim("Rider", 1, 20, 10);
            var mech = Sim("Mech", 1, 40, 10);
            var enemy = Sim("Enemy", 10, 20, 10);
            var events = new CombatEvents();
            events.Guards.Set(rider, mech);

            EncounterSimulator.ResolveAttack(enemy, rider, new CombatBuffTracker { Events = events });

            Assert.AreEqual(20, rider.Stats.Health);
            Assert.Less(mech.Stats.Health, 40);
        }

        // ------------------------------------------------------------------ the shared pace

        [Test]
        public void Follower_ActsStraightAfterEachOfItsLeadersTurns()
        {
            var rider = new MockCombatUnit("Rider", strength: 1, endurance: 1, health: 10, agility: 10);
            var foe = new MockCombatUnit("Foe", strength: 1, endurance: 1, health: 10, agility: 13);
            var mech = new MockCombatUnit("Mech", strength: 1, endurance: 1, health: 10, agility: 50);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { rider, foe });

            clock.Follow(mech, rider, actsNow: true);

            Assert.AreSame(mech, clock.GetNextUnit(), "it acts at once when it arrives");
            var order = Enumerable.Range(0, 6).Select(_ => clock.GetNextUnit()).ToList();
            for (int i = 0; i < order.Count - 1; i++)
            {
                if (ReferenceEquals(order[i], rider))
                {
                    Assert.AreSame(mech, order[i + 1], "after every rider turn, whatever the mech's own Agility");
                }
            }
            Assert.AreEqual(order.Count(u => ReferenceEquals(u, rider)), order.Count(u => ReferenceEquals(u, mech)), 1,
                "one mech turn per rider turn");
        }

        [Test]
        public void Follower_ShowsInThePreview_AndLeavesWithRemoveUnit()
        {
            var rider = new MockCombatUnit("Rider", strength: 1, endurance: 1, health: 10, agility: 10);
            var foe = new MockCombatUnit("Foe", strength: 1, endurance: 1, health: 10, agility: 7);
            var mech = new MockCombatUnit("Mech", strength: 1, endurance: 1, health: 10, agility: 10);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { rider, foe });
            clock.Follow(mech, rider, actsNow: false);

            var preview = clock.GetTurnOrder(4);
            Assert.AreSame(rider, preview[0]);
            Assert.AreSame(mech, preview[1]);

            clock.RemoveUnit(mech);
            Assert.IsFalse(clock.GetTurnOrder(6).Contains(mech));
            Assert.IsNull(clock.FollowerOf(rider));
        }

        // ------------------------------------------------------------------ the model's mirror

        [Test]
        public void SimMount_GuardsTheRider_AndFollowsTheirTurns_UntilItBreaks()
        {
            var mechDef = ScriptableObject.CreateInstance<SummonSO>();
            mechDef.Key = "test-mech";
            mechDef.Kind = SummonKind.JoinParty;
            mechDef.StatPercents = new StatBlock(new UnitStat(StatType.MaxHealth, 150), new UnitStat(StatType.Agility, 100));
            var rider = Sim("Rider", 1, 20, 10);
            var foe = Sim("Foe", 1, 20, 9);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { rider, foe });
            var events = new CombatEvents();
            var allies = new SimAllies(clock, new List<SimUnit> { foe }) { Events = events };

            var mech = allies.ArriveMount(rider, mechDef);

            Assert.AreSame(mech, allies.MountOf(rider));
            Assert.AreSame(mech.Unit, events.Guards.Redirect(rider));
            Assert.AreSame(mech.Unit, clock.FollowerOf(rider));
            Assert.AreSame(mech.Unit, clock.GetNextUnit(), "acts at once");

            HealthOps.Set(mech.Unit, 0, new HealthSource(foe, HealthCause.Attack), events);
            allies.AfterTurn(foe);

            Assert.IsNull(allies.MountOf(rider));
            Assert.AreSame(rider, events.Guards.Redirect(rider));
            Assert.IsNull(clock.FollowerOf(rider));
            Object.DestroyImmediate(mechDef);
        }

        [Test]
        public void SimMount_StaysThroughAPartyReplacingSummon()
        {
            // A party-replacing summon sends the guests home, but a mech steps out with its rider and
            // comes back with her (owner, 2026-10-07).
            var mechDef = ScriptableObject.CreateInstance<SummonSO>();
            mechDef.Key = "test-mech";
            mechDef.Kind = SummonKind.JoinParty;
            mechDef.StatPercents = new StatBlock(new UnitStat(StatType.MaxHealth, 150), new UnitStat(StatType.Agility, 100));
            var guestDef = ScriptableObject.CreateInstance<SummonSO>();
            guestDef.Key = "test-guest";
            guestDef.Kind = SummonKind.JoinParty;
            guestDef.StatPercents = new StatBlock(new UnitStat(StatType.MaxHealth, 50));
            guestDef.TurnsActive = 3;
            var rider = Sim("Rider", 1, 20, 10);
            var other = Sim("Other", 1, 20, 10);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { rider, other });
            var allies = new SimAllies(clock, new List<SimUnit>()) { Events = new CombatEvents() };
            var mech = allies.ArriveMount(rider, mechDef);
            allies.Arrive(other, guestDef, null);

            allies.DismissAll();

            Assert.AreSame(mech, allies.MountOf(rider));
            Assert.AreEqual(1, allies.All.Count, "the guest went home; the mech stayed");
            Object.DestroyImmediate(mechDef);
            Object.DestroyImmediate(guestDef);
        }
    }
}
