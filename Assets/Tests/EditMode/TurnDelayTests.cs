using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The turn delay (<see cref="SpellEffectType.TurnDelay"/>, docs/plans/SPECIALIZATION.md §4b, the
    /// Ranger's Exatrix): a hit that pushes its target back on the CTB clock at once, capped at one
    /// extra full turn, and a party-replacing summon's own Attack (<see cref="SummonSO.AttackAbility"/>)
    /// that carries it - in the live resolver and in the balance model alike.
    /// </summary>
    public class TurnDelayTests
    {
        private const float Tolerance = 1e-3f;

        // --- TurnManager.Delay ------------------------------------------------------------------

        [Test]
        public void Delay_HalfATurn_LetsTheOtherUnitActFirst()
        {
            // Equal Agility: without a delay A acts first (first in the list wins a tie).
            var a = new MockCombatUnit("A", strength: 1, endurance: 1, health: 10, agility: 10);
            var b = new MockCombatUnit("B", strength: 1, endurance: 1, health: 10, agility: 10);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { a, b });

            float moved = clock.Delay(a, 0.5f);

            Assert.AreEqual(0.5f, moved, Tolerance);
            Assert.AreSame(b, clock.GetNextUnit(), "Half a turn behind, A now waits for B.");
            Assert.AreSame(a, clock.GetNextUnit());
        }

        [Test]
        public void Delay_IsMeasuredInTheTargetsOwnTurn()
        {
            // A slow unit's turn is longer, so half of it is more ticks. The preview, which reads
            // the same counters, is where that shows.
            var fast = new MockCombatUnit("Fast", strength: 1, endurance: 1, health: 10, agility: 20);
            var slow = new MockCombatUnit("Slow", strength: 1, endurance: 1, health: 10, agility: 5);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { fast, slow });

            Assert.AreEqual(0.4f, clock.Delay(slow, 0.4f), Tolerance);
            // Slow was at 20 ticks and is now at 28; Fast acts at 5, 10, 15, 20, 25 before it. Measured
            // in Fast's turn instead, the delay would have been 2 ticks and Fast would have gone four times.
            var order = clock.GetTurnOrder(7);
            Assert.AreEqual(5, order.TakeWhile(u => ReferenceEquals(u, fast)).Count());
        }

        [Test]
        public void Delay_StacksUpToOneExtraTurn_AndNoFurther()
        {
            var a = new MockCombatUnit("A", strength: 1, endurance: 1, health: 10, agility: 10);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { a });

            // A unit that has not acted sits at one full turn; the cap is two.
            Assert.AreEqual(0.5f, clock.Delay(a, 0.5f), Tolerance);
            Assert.AreEqual(0.5f, clock.Delay(a, 0.5f), Tolerance);
            Assert.AreEqual(0f, clock.Delay(a, 0.5f), Tolerance, "Already one whole turn behind.");
        }

        [Test]
        public void Delay_PartlyBlockedByTheCap_ReportsOnlyWhatMoved()
        {
            var a = new MockCombatUnit("A", strength: 1, endurance: 1, health: 10, agility: 10);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { a });

            Assert.AreEqual(0.75f, clock.Delay(a, 0.75f), Tolerance);
            Assert.AreEqual(0.25f, clock.Delay(a, 0.5f), Tolerance);
        }

        [Test]
        public void Delay_ASuspendedUnit_IsNotMoved()
        {
            // The party is off the clock while a summon replaces it; nothing can reach it there.
            var hero = new MockCombatUnit("Hero", strength: 1, endurance: 1, health: 10, agility: 10);
            var enemy = new MockCombatUnit("Enemy", strength: 1, endurance: 1, health: 10, agility: 10, isHero: false);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { hero, enemy });
            clock.Suspend(new List<ICombatUnit> { hero });

            Assert.AreEqual(0f, clock.Delay(hero, 0.5f), Tolerance);
            clock.Resume();
            Assert.AreSame(hero, clock.GetNextUnit(), "It came back exactly where it was frozen.");
        }

        // --- the effect -------------------------------------------------------------------------

        private static MagicSO Rend(int damage, int delayPercent)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "TestRend";
            magic.DisplayName = "Rend";
            magic.TargetType = MagicTargetType.SingleEnemy;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = damage, PowerMode = PowerMode.Flat },
                new SpellEffect { EffectType = SpellEffectType.TurnDelay, Power = delayPercent }
            };
            return magic;
        }

        [Test]
        public void TurnDelayEffect_WithAClock_PushesTheTargetBack()
        {
            var caster = new MockCombatUnit("Shade", strength: 1, endurance: 1, health: 10, agility: 10);
            var target = new MockCombatUnit("Rat", strength: 1, endurance: 0, health: 50, agility: 10, isHero: false);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { target, caster });
            var resolver = new EffectResolver { Clock = clock };

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = Rend(3, 50), Caster = caster, Targets = new List<ICombatUnit> { target }
            }, new CombatBuffTracker());

            Assert.AreEqual(47, target.Stats.Health, "The claw still lands.");
            Assert.IsTrue(result.Entries.Any(e => e.Text == "Delayed"));
            Assert.AreSame(caster, clock.GetNextUnit(), "Half a turn behind, the rat yields its tie.");
        }

        [Test]
        public void TurnDelayEffect_AtTheCap_SaysSo()
        {
            var caster = new MockCombatUnit("Shade", strength: 1, endurance: 1, health: 10, agility: 10);
            var target = new MockCombatUnit("Rat", strength: 1, endurance: 0, health: 50, agility: 10, isHero: false);
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { target, caster });
            clock.Delay(target, 1f);
            var resolver = new EffectResolver { Clock = clock };

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = Rend(1, 50), Caster = caster, Targets = new List<ICombatUnit> { target }
            }, new CombatBuffTracker());

            Assert.IsTrue(result.Entries.Any(e => e.Text == "Can't delay further"));
        }

        [Test]
        public void TurnDelayEffect_WithoutAClock_IsInertAndSilent()
        {
            var caster = new MockCombatUnit("Shade", strength: 1, endurance: 1, health: 10, agility: 10);
            var target = new MockCombatUnit("Rat", strength: 1, endurance: 0, health: 50, agility: 10, isHero: false);

            var result = new EffectResolver().Execute(new SpellcastAction
            {
                Magic = Rend(3, 50), Caster = caster, Targets = new List<ICombatUnit> { target }
            }, new CombatBuffTracker());

            Assert.AreEqual(47, target.Stats.Health);
            Assert.IsFalse(result.Entries.Any(e => e.Text == "Delayed" || e.Text == "Can't delay further"),
                "No clock, no delay - and no claim of one.");
        }

        // --- the summon's own Attack, in the balance model ---------------------------------------

        private static SimUnit Unit(string name, bool hero, int strength, int health, int agility)
        {
            return new SimUnit
            {
                DisplayName = name,
                HeroKey = hero ? name : null,
                IsHero = hero,
                Archetype = EnemyArchetype.Aggressor,
                Stats = TestStats.Make(strength, 0, health, agility),
                Effective = TestStats.Block(strength, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = strength,
                Resistances = new List<Resistance>()
            };
        }

        private static EncounterSimulator.FloorOutcome RunShade(MagicSO attackAbility)
        {
            // A shade with no Strength of its own: its plain Attack does nothing, so the fight only
            // ends if the simulator swings its AttackAbility instead.
            var shade = ScriptableObject.CreateInstance<SummonSO>();
            shade.Key = "TestShade";
            shade.Kind = SummonKind.ReplaceParty;
            shade.BaseCharges = 1;
            shade.TurnsActive = 10;
            shade.StatPercents = new StatBlock(
                new UnitStat(StatType.MaxHealth, 500),
                new UnitStat(StatType.Agility, 100));
            shade.AttackAbility = attackAbility;

            var hero = Unit("ranger", true, strength: 1, health: 100, agility: 20);
            hero.Summons.Add(new SimSummonSlot
            {
                Summon = shade, Grant = new SummonGrant { Key = shade.Key }, Charges = 1, MaxCharges = 1
            });
            var party = new PartyBaseline { SourceLabel = "test" };
            party.Heroes.Add(new HeroBaseline { Effective = hero.Effective.Clone(), Unit = hero });

            var rat = Unit("rat", false, strength: 1, health: 30, agility: 5);
            return EncounterSimulator.RunFloor(party,
                new List<IList<SimUnit>> { new List<SimUnit> { rat } },
                new EncounterSimulator.FloorSimSettings
                {
                    Trials = 5, Seed = 4242, MaxTurns = 8, Policy = SimPolicy.Adaptive,
                    Combos = new List<MagicComboSO>(), UseSummons = true
                });
        }

        [Test]
        public void Simulator_AReplacementWithItsOwnAttack_SwingsIt()
        {
            var with = RunShade(Rend(15, 50));
            var without = RunShade(null);

            Assert.AreEqual(1f, with.AverageSummonsUsed, Tolerance);
            Assert.AreEqual(1f, with.ClearRate, Tolerance, "Two Rends kill the rat inside the turn cap.");
            Assert.Less(without.ClearRate, 1f, "The control: a Strength-0 plain Attack never kills it in time.");
        }

        // --- the authored summon ----------------------------------------------------------------

        [Test]
        public void Exatrix_HerAttackDelays_AndIsNeverAHerosMagic()
        {
            var catalog = UnityEngine.Resources.Load<SummonCatalogSO>("SummonCatalog");
            Assert.IsNotNull(catalog);
            var exatrix = catalog.Summons.FirstOrDefault(s => s != null && s.Key == "Exatrix");
            Assert.IsNotNull(exatrix, "Exatrix is in the summon catalog.");
            Assert.AreEqual(SummonKind.ReplaceParty, exatrix.Kind);
            Assert.IsNotNull(exatrix.AttackAbility, "Her Attack is her own claw.");
            Assert.AreEqual(MagicTargetType.SingleEnemy, exatrix.AttackAbility.TargetType);
            Assert.IsTrue(exatrix.AttackAbility.HasEffectType(SpellEffectType.TurnDelay), "Rend delays.");
            Assert.IsTrue(SummonOps.AbilityKeys(catalog.Summons).Contains(exatrix.AttackAbility.Key),
                "An attack override is a summon's ability, so it stays out of every hero list.");
        }
    }
}
