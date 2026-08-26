using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The floor simulation, which exists because the per-encounter one structurally cannot report the
    /// way runs actually end. Every test here pins something a room-at-a-time simulation is blind to:
    /// attrition compounding, the absence of any revive, and a potion belt that is spent rather than
    /// refilled per fight. If these ever pass with the rooms run independently, the floor sim has
    /// stopped carrying state and the whole losability measurement is back to reporting 100%.
    /// </summary>
    public class FloorSimulatorTests
    {
        private static SimUnit Hero(string name, int attack, int defense, int health, int agility = 5)
        {
            return new SimUnit
            {
                DisplayName = name,
                HeroKey = name,
                IsHero = true,
                Stats = TestStats.Make(attack, defense, health, agility),
                Effective = TestStats.Block(attack, defense, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit Enemy(string name, int attack, int defense, int health, int agility = 5)
        {
            return new SimUnit
            {
                DisplayName = name,
                IsHero = false,
                Archetype = EnemyArchetype.Aggressor,
                Stats = TestStats.Make(attack, defense, health, agility),
                Effective = TestStats.Block(attack, defense, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static PartyBaseline Party(params SimUnit[] heroes)
        {
            var party = new PartyBaseline { SourceLabel = "test" };
            foreach (var hero in heroes)
            {
                party.Heroes.Add(new HeroBaseline
                {
                    Effective = TestStats.Block(
                        hero.EffectiveAttackPower,
                        hero.Effective[StatType.Endurance],
                        hero.Effective[StatType.MaxHealth],
                        hero.Effective[StatType.Agility]),
                    Unit = hero
                });
            }
            return party;
        }

        private static EncounterSimulator.FloorSimSettings Settings(
            int trials = 40,
            SimPolicy policy = SimPolicy.AttackOnly,
            int potions = 0,
            int restRooms = 0)
        {
            return new EncounterSimulator.FloorSimSettings
            {
                Trials = trials,
                Seed = 4321,
                MaxTurns = 300,
                Policy = policy,
                Combos = new List<MagicComboSO>(),
                PotionCount = potions,
                PotionHealAmount = 5,
                RestRooms = restRooms,
                RestHealFraction = 0.35f
            };
        }

        private static List<IList<SimUnit>> Rooms(params SimUnit[][] rooms)
        {
            var list = new List<IList<SimUnit>>();
            foreach (var room in rooms)
            {
                list.Add(new List<SimUnit>(room));
            }
            return list;
        }

        /// <summary>
        /// The core claim. One weak enemy is survivable; the same enemy four times over, off one
        /// health pool, is not. A per-encounter simulation reports the second case as four copies of
        /// the first and so reports 100% either way.
        /// </summary>
        [Test]
        public void RunFloor_AttritionCompoundsAcrossRooms()
        {
            var oneRoom = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)),
                Rooms(new[] { Enemy("biter", 8, 0, 10) }),
                Settings());

            var fourRooms = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)),
                Rooms(new[] { Enemy("biter", 8, 0, 10) },
                      new[] { Enemy("biter", 8, 0, 10) },
                      new[] { Enemy("biter", 8, 0, 10) },
                      new[] { Enemy("biter", 8, 0, 10) }),
                Settings());

            Assert.AreEqual(0f, oneRoom.WipeRate,
                "A single weak enemy should not be able to wipe a full-health hero.");
            Assert.Greater(fourRooms.WipeRate, 0f,
                "Four of the same enemy off one health pool must be able to kill; if this is 0 the "
                + "floor is resetting health between rooms and measures nothing the per-room sim does not.");
            Assert.Less(fourRooms.AverageEndHealthFraction, oneRoom.AverageEndHealthFraction,
                "Surviving four rooms must cost more health than surviving one.");
        }

        /// <summary>
        /// There is no revive item, spell or between-room recovery in the game - Party.HealAll fires
        /// only on entering a fresh dungeon - so a hero lost in room 1 must still be dead in room 3.
        /// This is the death spiral that makes late rooms disproportionately lethal.
        /// </summary>
        [Test]
        public void RunFloor_DeadHeroesStayDeadForTheWholeFloor()
        {
            var outcome = EncounterSimulator.RunFloor(DeathSpiralParty(), DeathSpiralRooms(), Settings());

            Assert.AreEqual(0f, outcome.WipeRate, "The anchor should carry the floor.");
            Assert.GreaterOrEqual(outcome.AverageHeroDeaths, 0.5f,
                "The glass hero should be dead at floor end. A per-room sim reports 0 deaths here "
                + "because it re-clones a full party for rooms 2 and 3.");
        }

        /// <summary>
        /// A hero who cannot survive the opening room, beside one nothing on the floor can hurt. The
        /// killer is fast (so it lands hits before it dies) and durable (so it gets turns at all - an
        /// enemy that dies to the anchor's first swing never acts, which is what made the first draft
        /// of these two tests report zero deaths).
        /// </summary>
        private static PartyBaseline DeathSpiralParty()
        {
            return Party(Hero("glass", 3, 0, 2), Hero("anchor", 14, 100, 3000));
        }

        private static List<IList<SimUnit>> DeathSpiralRooms()
        {
            return Rooms(new[] { Enemy("killer", 30, 0, 60, agility: 20) },
                         new[] { Enemy("pushover", 1, 0, 1) },
                         new[] { Enemy("pushover", 1, 0, 1) });
        }

        /// <summary>
        /// The potion belt is a floor resource, not a per-fight one. Per encounter it is effectively
        /// multiplied by the room count, which is most of why per-room sustain reads as unlimited.
        /// </summary>
        [Test]
        public void RunFloor_PotionBeltIsSpentAcrossTheWholeFloor()
        {
            var rooms = Rooms(new[] { Enemy("biter", 7, 0, 12) },
                              new[] { Enemy("biter", 7, 0, 12) },
                              new[] { Enemy("biter", 7, 0, 12) });

            var withBelt = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 34)), rooms,
                Settings(policy: SimPolicy.Adaptive, potions: 2));

            Assert.LessOrEqual(withBelt.AveragePotionsUsed, 2f,
                "A floor can never spend more than the belt it started with; anything above 2 means "
                + "potions are being refilled per room.");
        }

        /// <summary>A refuge hands health back mid-floor, so it must move the outcome.</summary>
        [Test]
        public void RunFloor_RefugeHealsBetweenRooms()
        {
            var rooms = Rooms(new[] { Enemy("biter", 8, 0, 10) },
                              new[] { Enemy("biter", 8, 0, 10) },
                              new[] { Enemy("biter", 8, 0, 10) },
                              new[] { Enemy("biter", 8, 0, 10) });

            var noRest = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)), rooms, Settings());
            var withRest = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)), rooms, Settings(restRooms: 2));

            Assert.Less(withRest.WipeRate, noRest.WipeRate,
                "Two refuges on a lethal floor must reduce the wipe rate.");
        }

        /// <summary>A refuge is not a revive: the dead do not come back from resting.</summary>
        [Test]
        public void RunFloor_RefugeDoesNotReviveTheDead()
        {
            var outcome = EncounterSimulator.RunFloor(
                DeathSpiralParty(), DeathSpiralRooms(), Settings(restRooms: 2));

            Assert.GreaterOrEqual(outcome.AverageHeroDeaths, 0.5f,
                "Resting restores health to the living only - a downed hero must stay down.");
        }

        /// <summary>
        /// Floor 0 is the only floor that starts on full charges (charges are a run resource), so the
        /// flag has to actually empty them - otherwise every floor is measured as a first floor.
        /// </summary>
        [Test]
        public void RunFloor_WithoutFullCharges_CastsNothing()
        {
            var hero = Hero("caster", 6, 0, 40);
            hero.MagicSlots.Add(new SimMagicSlot { Magic = null, Charges = 4, MaxCharges = 4 });

            var settings = Settings(policy: SimPolicy.MagicFirst);
            settings.StartsWithFullCharges = false;

            var outcome = EncounterSimulator.RunFloor(
                Party(hero),
                Rooms(new[] { Enemy("biter", 4, 0, 12) }),
                settings);

            Assert.AreEqual(0f, outcome.AverageCastsUsed,
                "A floor after the first starts on empty charges, so nothing can be cast.");
        }

        /// <summary>Progress reporting: a floor stopped early must not claim rooms it never cleared.</summary>
        [Test]
        public void RunFloor_ReportsRoomsClearedAndProgress()
        {
            var outcome = EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 12)),
                Rooms(new[] { Enemy("wall", 20, 0, 10) },
                      new[] { Enemy("wall", 20, 0, 10) },
                      new[] { Enemy("wall", 20, 0, 10) }),
                Settings());

            Assert.AreEqual(3, outcome.Rooms);
            Assert.Greater(outcome.WipeRate, 0f, "This floor should kill the hero.");
            Assert.Less(outcome.AverageRoomsCleared, 3f,
                "A wiped floor cannot have cleared every room.");
            Assert.AreEqual(outcome.AverageRoomsCleared / 3f, outcome.FloorProgress, 0.0001f);
        }

        /// <summary>An empty floor or a null party is a no-op, not an exception.</summary>
        [Test]
        public void RunFloor_EmptyInputs_ReturnEmptyOutcome()
        {
            Assert.AreEqual(0, EncounterSimulator.RunFloor(null, Rooms(), Settings()).Trials);
            Assert.AreEqual(0, EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)), new List<IList<SimUnit>>(), Settings()).Trials);
            Assert.AreEqual(0, EncounterSimulator.RunFloor(
                Party(Hero("solo", 6, 0, 30)), Rooms(new[] { Enemy("x", 1, 0, 1) }), null).Trials);
        }
    }
}
