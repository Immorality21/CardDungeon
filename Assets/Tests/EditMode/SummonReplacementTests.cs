using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The party-replacing summon (docs/plans/SPECIALIZATION.md §4b, the Cairn Golem): the turn
    /// manager freezing the party and inserting the summon's immediate turn, and the balance model's
    /// replacement path - the party untouchable while it is out, the finishing blow swallowed, a
    /// wind-up re-aimed at it, and the party back where it left when its turns run out.
    /// </summary>
    public class SummonReplacementTests
    {
        // --- TurnManager: suspend, resume, act next ------------------------------------------

        [Test]
        public void Suspend_TakesUnitsOffTheClock_AndOutOfThePreview()
        {
            var hero = new MockCombatUnit("Hero", 5, 0, 30, agility: 10);
            var enemy = new MockCombatUnit("Enemy", 5, 0, 30, agility: 10);
            var tm = new TurnManager();
            tm.Initialize(new List<ICombatUnit> { hero, enemy });

            tm.Suspend(new List<ICombatUnit> { hero });

            Assert.IsTrue(tm.IsSuspended(hero));
            Assert.That(tm.GetTurnOrder(6).All(u => ReferenceEquals(u, enemy)), "Only the enemy is on the clock.");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreSame(enemy, tm.GetNextUnit(), "A suspended unit never gets a turn.");
            }
        }

        [Test]
        public void Resume_PutsUnitsBackAtTheCounterTheyWereFrozenWith()
        {
            // Hero is due in 5 ticks (agility 20), enemy in 10. Freeze the hero, let the enemy take
            // three turns, resume: the hero is still 5 ticks out, so it acts before the enemy's 10.
            var hero = new MockCombatUnit("Hero", 5, 0, 30, agility: 20);
            var enemy = new MockCombatUnit("Enemy", 5, 0, 30, agility: 10);
            var tm = new TurnManager();
            tm.Initialize(new List<ICombatUnit> { hero, enemy });

            tm.Suspend(new List<ICombatUnit> { hero });
            tm.GetNextUnit();
            tm.GetNextUnit();
            tm.GetNextUnit();
            tm.Resume();

            Assert.IsFalse(tm.IsSuspended(hero));
            Assert.AreSame(hero, tm.GetNextUnit(), "Time did not pass for the frozen hero.");
        }

        [Test]
        public void AddUnit_ActsNext_GoesFirstWithoutMovingTheClock_AndLeadsThePreview()
        {
            var enemy = new MockCombatUnit("Enemy", 5, 0, 30, agility: 50);
            var summon = new MockCombatUnit("Summon", 5, 0, 30, agility: 1);
            var tm = new TurnManager();
            tm.Initialize(new List<ICombatUnit> { enemy });

            tm.AddUnit(summon, actsNext: true);

            Assert.AreSame(summon, tm.GetTurnOrder(3)[0], "The preview shows the immediate turn first.");
            Assert.AreSame(summon, tm.GetNextUnit(), "A slow summon still takes its arrival turn at once.");
            Assert.AreSame(enemy, tm.GetNextUnit(), "Then the clock carries on, the enemy still due first.");
        }

        [Test]
        public void RemoveUnit_ForgetsAnInsertedTurn()
        {
            var enemy = new MockCombatUnit("Enemy", 5, 0, 30, agility: 10);
            var summon = new MockCombatUnit("Summon", 5, 0, 30, agility: 10);
            var tm = new TurnManager();
            tm.Initialize(new List<ICombatUnit> { enemy });
            tm.AddUnit(summon, actsNext: true);

            tm.RemoveUnit(summon);

            Assert.AreSame(enemy, tm.GetNextUnit());
        }

        // --- Slow, which Quake is the first thing to apply ---------------------------------

        [Test]
        public void SlowCastAsADebuff_LowersAgility()
        {
            // DebuffEffectExecutor hands the handler a negative power; Slow used to negate it again,
            // so a Slow cast as a Debuff *raised* the target's Agility.
            var caster = new MockCombatUnit("Golem", 10, 0, 100, agility: 5);
            var target = new MockCombatUnit("Enemy", 10, 0, 100, agility: 10);
            var tracker = new CombatBuffTracker();
            var effect = new SpellEffect { EffectType = SpellEffectType.Debuff, BuffType = BuffType.Slow, Power = 3, Duration = 3 };

            new DebuffEffectExecutor().Execute(effect, caster, new List<ICombatUnit> { target }, tracker, new EffectResult(), flatPower: true);

            Assert.AreEqual(-3, tracker.GetBuffAmount(target, StatType.Agility));
            Assert.IsTrue(tracker.HasStatusEffect(target, BuffType.Slow));
        }

        // --- the balance model ------------------------------------------------------------------

        private static SimUnit Hero(int strength, int health, int agility)
        {
            return new SimUnit
            {
                DisplayName = "warrior",
                HeroKey = "warrior",
                IsHero = true,
                Stats = TestStats.Make(strength, 0, health, agility),
                Effective = TestStats.Block(strength, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = strength,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit Enemy(int attack, int health, int agility, EnemyArchetype archetype = EnemyArchetype.Aggressor)
        {
            return new SimUnit
            {
                DisplayName = "enemy",
                IsHero = false,
                Archetype = archetype,
                Stats = TestStats.Make(attack, 0, health, agility),
                Effective = TestStats.Block(attack, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static SummonSO Golem(int healthPercent, int strengthPercent, int agilityPercent, int turns)
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "TestGolem";
            summon.Kind = SummonKind.ReplaceParty;
            summon.BaseCharges = 1;
            summon.TurnsActive = turns;
            summon.StatPercents = new StatBlock(
                new UnitStat(StatType.MaxHealth, healthPercent),
                new UnitStat(StatType.Strength, strengthPercent),
                new UnitStat(StatType.Agility, agilityPercent));
            return summon;
        }

        private static SimUnit WithGolem(SimUnit hero, SummonSO golem)
        {
            hero.Summons.Add(new SimSummonSlot
            {
                Summon = golem,
                Grant = new SummonGrant { Key = golem.Key },
                Charges = 1,
                MaxCharges = 1
            });
            return hero;
        }

        private static PartyBaseline Party(SimUnit hero)
        {
            var party = new PartyBaseline { SourceLabel = "test" };
            party.Heroes.Add(new HeroBaseline { Effective = hero.Effective.Clone(), Unit = hero });
            return party;
        }

        private static List<IList<SimUnit>> OneRoom(params SimUnit[] enemies)
        {
            return new List<IList<SimUnit>> { new List<SimUnit>(enemies) };
        }

        private static EncounterSimulator.FloorSimSettings Settings(int maxTurns = 300, bool summons = true)
        {
            return new EncounterSimulator.FloorSimSettings
            {
                Trials = 10,
                Seed = 4242,
                MaxTurns = maxTurns,
                Policy = SimPolicy.Adaptive,
                Combos = new List<MagicComboSO>(),
                UseSummons = summons
            };
        }

        [Test]
        public void ReplacementWanted_OnAWindUp_OrAtOnceWhenNothingTelegraphs()
        {
            var plain = Enemy(5, 30, 10);
            var bruiser = Enemy(5, 30, 10, EnemyArchetype.Bruiser);

            Assert.IsTrue(EncounterSimulator.ReplacementWanted(new List<SimUnit> { plain }),
                "Nothing here telegraphs, so there is nothing to wait for.");
            Assert.IsFalse(EncounterSimulator.ReplacementWanted(new List<SimUnit> { plain, bruiser }),
                "The bruiser will wind up; the charge waits for it.");

            bruiser.ChargingEntryIndex = 0;
            Assert.IsTrue(EncounterSimulator.ReplacementWanted(new List<SimUnit> { plain, bruiser }));
        }

        [Test]
        public void WhileTheGolemIsOut_ThePartyTakesNothing()
        {
            // The golem (fast, as strong as the hero, ten turns) kills the enemy before it leaves.
            // Without it the hero trades blows and ends the fight hurt.
            var enemy = Enemy(attack: 10, health: 60, agility: 15);

            var with = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(20, 100, 20), Golem(250, 100, 100, 10))), OneRoom(enemy), Settings());
            var without = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(20, 100, 20), Golem(250, 100, 100, 10))), OneRoom(enemy), Settings(summons: false));

            Assert.AreEqual(1f, with.AverageSummonsUsed, 1e-4f);
            Assert.AreEqual(1f, with.AverageEndHealthFraction, 1e-4f, "Every blow landed on the golem.");
            Assert.Less(without.AverageEndHealthFraction, 1f, "The control: the enemy does hurt the hero.");
        }

        [Test]
        public void TheBlowThatBreaksTheGolem_GoesNoFurther()
        {
            // An enemy that hits for far more than the golem's 250 HP. The fight is cut off just after
            // that blow: the golem is gone, and the hero it covered has not been touched.
            var ogre = Enemy(attack: 1000, health: 5000, agility: 20);
            var with = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(5, 100, 30), Golem(250, 10, 80, 10))), OneRoom(ogre), Settings(maxTurns: 3));
            var without = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(5, 100, 30), Golem(250, 10, 80, 10))), OneRoom(ogre), Settings(maxTurns: 3, summons: false));

            Assert.AreEqual(1f, with.AverageSummonsUsed, 1e-4f);
            Assert.AreEqual(0, with.Wipes, "The 750 left over from breaking the golem never reached the party.");
            Assert.AreEqual(0f, with.AverageHeroDeaths, 1e-4f);
            Assert.AreEqual(without.Trials, without.Wipes, "The control: the same blow kills the hero.");
        }

        [Test]
        public void AWindUpAimedAtTheHero_LandsOnTheGolemInstead()
        {
            // The bruiser locks its heavy onto the hero, the golem answers it, and the heavy lands on
            // the golem. A heavy that one-shots the hero would otherwise wipe the party.
            var bruiser = Enemy(attack: 60, health: 5000, agility: 10, EnemyArchetype.Bruiser);
            var outcome = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(5, 100, 11), Golem(300, 10, 50, 20))), OneRoom(bruiser), Settings(maxTurns: 6));

            Assert.AreEqual(1f, outcome.AverageSummonsUsed, 1e-4f, "It waited for the wind-up.");
            Assert.AreEqual(0f, outcome.AverageHeroDeaths, 1e-4f, "The 150-damage heavy hit the golem.");
        }

        [Test]
        public void WhenItsTurnsRunOut_ThePartyComesBackAndFightsOn()
        {
            // A one-turn golem too weak to matter: it leaves, and the hero wins the fight themselves.
            var rat = Enemy(attack: 2, health: 40, agility: 5);
            var outcome = EncounterSimulator.RunFloor(
                Party(WithGolem(Hero(10, 100, 20), Golem(250, 1, 50, 1))), OneRoom(rat), Settings());

            Assert.AreEqual(1f, outcome.AverageSummonsUsed, 1e-4f);
            Assert.AreEqual(1f, outcome.ClearRate, 1e-4f, "The hero was back on the clock and finished it.");
        }
    }
}
