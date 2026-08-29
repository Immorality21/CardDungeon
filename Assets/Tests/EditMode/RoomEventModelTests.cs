using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The balance model's view of room events, on synthetic assets so the expectation maths is
    /// pinned independently of whatever the project's real events happen to be.
    ///
    /// <para>Heroes here are authored at <b>Endurance 0</b> on purpose: the defense curve then
    /// removes nothing, so an outcome's authored Power is its damage and every expected value below
    /// can be checked by hand.</para>
    /// </summary>
    public class RoomEventModelTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _created.Clear();
        }

        // ------------------------------------------------------------------
        //  Fixtures
        // ------------------------------------------------------------------

        private BalanceRulesSO Rules()
        {
            var rules = Make<BalanceRulesSO>();
            rules.ReferenceHeroXp = 0;

            // These cases are about how a level's rooms are spread, replaced and budgeted, not about
            // how much of the floor the player walks. Pin the traversal discount off so an arithmetic
            // assertion here stays readable; TraversalModelTests covers the discount itself, and
            // RunCurve_TraversalMode_DiscountsTheRoomsThePlayerNeverOpens covers it reaching the curve.
            rules.Traversal = TraversalMode.FullClear;
            return rules;
        }

        private HeroSO Hero(string label, int strength = 12, int luck = 0, int health = 100)
        {
            var hero = Make<HeroSO>();
            hero.Label = label;
            hero.BaseStats[StatType.Strength] = strength;
            hero.BaseStats[StatType.Endurance] = 0;
            hero.BaseStats[StatType.MaxHealth] = health;
            hero.BaseStats[StatType.Agility] = 5;
            if (luck != 0)
            {
                hero.BaseStats[StatType.Luck] = luck;
            }
            return hero;
        }

        private PartyBaseline Party(params HeroSO[] heroes)
        {
            return PartyBaseline.Build(new List<HeroSO>(heroes), 0);
        }

        private PartyBaseline PartyWithPotions(ItemSO potion, int count, params HeroSO[] heroes)
        {
            return PartyBaseline.Build(new List<HeroSO>(heroes), 0, null, potion, count);
        }

        private ItemSO Potion(int amount)
        {
            var item = Make<ItemSO>();
            item.Key = "Potion";
            item.DisplayName = "Potion";
            item.Category = ItemCategory.Consumable;
            item.ConsumableEffect = ConsumableEffectType.RestoreHealth;
            item.ConsumableAmount = amount;
            return item;
        }

        private EnemySO Goblin(int xp = 10, int gold = 5)
        {
            var enemy = Make<EnemySO>();
            enemy.DisplayName = "Goblin";
            enemy.BaseStats[StatType.Strength] = 4;
            enemy.BaseStats[StatType.Endurance] = 0;
            enemy.BaseStats[StatType.MaxHealth] = 20;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.XpReward = xp;
            enemy.GoldReward = gold;
            enemy.Archetype = EnemyArchetype.Aggressor;
            enemy.DrawableMagics = new List<DrawableMagicEntry>();
            return enemy;
        }

        private static SpellEffect Damage(int power)
        {
            return new SpellEffect { EffectType = SpellEffectType.Damage, Power = power };
        }

        private static RoomEventOutcome Outcome(int weight = 1)
        {
            return new RoomEventOutcome { Weight = weight, Text = "..." };
        }

        /// <summary>An event with a single Guaranteed option, i.e. no roll between the player and the outcome.</summary>
        private RoomEventSO GuaranteedEvent(string key, float spawnPercent, params RoomEventOutcome[] outcomes)
        {
            var definition = Make<RoomEventSO>();
            definition.Key = key;
            definition.Title = key;
            definition.SpawnChancePercent = spawnPercent;
            definition.SpawnModifierStat = StatType.None;
            definition.Options = new List<RoomEventOption>
            {
                new RoomEventOption
                {
                    Label = "Do it",
                    Kind = RoomEventOptionKind.Guaranteed,
                    Success = new List<RoomEventOutcome>(outcomes)
                }
            };
            return definition;
        }

        private RoomSO RoomOffering(params RoomEventSO[] events)
        {
            var room = Make<RoomSO>();
            room.Name = "Test Room";
            room.Width = 3;
            room.Height = 3;
            room.EnemySpawnTable = new List<EnemySpawnEntry>();
            room.PossibleEvents = new List<RoomEventSO>(events);
            return room;
        }

        private List<RoomEncounter> Encounters(PartyBaseline party, float occurrences, params RoomSO[] rooms)
        {
            var list = new List<RoomEncounter>();
            foreach (var room in rooms)
            {
                var encounter = RoomEncounter.Build(room, null, false, party, Rules());
                encounter.Occurrences = occurrences;
                list.Add(encounter);
            }
            return list;
        }

        // ------------------------------------------------------------------
        //  Costing one outcome
        // ------------------------------------------------------------------

        [Test]
        public void Build_DamageOutcome_CostsThroughTheDefenseCurve()
        {
            var tough = Hero("Tough");
            tough.BaseStats[StatType.Endurance] = 20; // exactly the constant: 50% reduction.
            var party = Party(tough);

            var outcome = Outcome();
            outcome.Effects.Add(Damage(10));
            var definition = GuaranteedEvent("Trap", 100f, outcome);

            var encounter = RoomEventModel.Build(definition, party, party.BestStats, 0);

            Assert.AreEqual(5f, encounter.Engaged.ExpectedSustainCost, 0.001f,
                "Event damage must run through DamageCalculator, not land raw.");
        }

        [Test]
        public void Build_ActingHeroTarget_CostsOneHero_WholePartyCostsEveryone()
        {
            var party = Party(Hero("A"), Hero("B"));

            var single = Outcome();
            single.Targets = RoomEventTargets.ActingHero;
            single.Effects.Add(Damage(10));

            var everyone = Outcome();
            everyone.Targets = RoomEventTargets.WholeParty;
            everyone.Effects.Add(Damage(10));

            var soloEvent = GuaranteedEvent("Solo", 100f, single);
            var partyEvent = GuaranteedEvent("Party", 100f, everyone);

            Assert.AreEqual(10f,
                RoomEventModel.Build(soloEvent, party, party.BestStats, 0).Engaged.ExpectedSustainCost, 0.001f);
            Assert.AreEqual(20f,
                RoomEventModel.Build(partyEvent, party, party.BestStats, 0).Engaged.ExpectedSustainCost, 0.001f);
        }

        [Test]
        public void Build_Healing_IsANegativeCost()
        {
            var party = Party(Hero("A"));

            var outcome = Outcome();
            outcome.Effects.Add(new SpellEffect { EffectType = SpellEffectType.Heal, Power = 6 });

            var encounter = RoomEventModel.Build(GuaranteedEvent("Shrine", 100f, outcome), party, party.BestStats, 0);

            Assert.AreEqual(-6f, encounter.Engaged.ExpectedSustainCost, 0.001f);
        }

        [Test]
        public void Build_LosingAConsumable_CostsThePotionsWorthOfSustain()
        {
            // A potion is worth exactly its restore amount out of the pool attrition divides by,
            // which is what makes losing one a cost rather than an inventory line.
            var party = PartyWithPotions(Potion(5), 2, Hero("A"));

            var outcome = Outcome();
            outcome.LoseAConsumable = true;

            var encounter = RoomEventModel.Build(GuaranteedEvent("Pickpocket", 100f, outcome), party, party.BestStats, 0);

            Assert.AreEqual(5f, encounter.Engaged.ExpectedSustainCost, 0.001f);
        }

        [Test]
        public void Build_AwakenedEnemies_AddTheirFightsCostAndRewards()
        {
            var party = Party(Hero("A"));

            var outcome = Outcome();
            outcome.AwakenedEnemies.Add(Goblin(xp: 10, gold: 5));

            var encounter = RoomEventModel.Build(GuaranteedEvent("Noise", 100f, outcome), party, party.BestStats, 0);

            Assert.Greater(encounter.Engaged.ExpectedSustainCost, 0f,
                "Waking something turns a safe room into a fight, and that fight costs HP.");
            Assert.AreEqual(10f, encounter.Engaged.ExpectedXp, 0.001f);
            Assert.AreEqual(5f, encounter.Engaged.ExpectedGold, 0.001f);
        }

        [Test]
        public void Build_Loot_IsCountedAtItsDropChance()
        {
            var party = Party(Hero("A"));

            var common = Make<ItemSO>();
            common.Key = "Sword";
            common.Rarity = ItemRarity.Common;
            common.ItemLevel = 1;

            var outcome = Outcome();
            outcome.LootTable.Add(common);
            outcome.LootTable.Add(common);

            var encounter = RoomEventModel.Build(GuaranteedEvent("Chest", 100f, outcome), party, party.BestStats, 0);

            // LootRoller gives a common item at depth 1 a 60% chance; two entries expect 1.2 drops.
            Assert.AreEqual(1.2f, encounter.Engaged.ExpectedLootDrops, 0.001f);
        }

        [Test]
        public void Build_BuffAndDebuffEffects_AreCountedAsAfflictionsNotHealth()
        {
            var party = Party(Hero("A"));

            var outcome = Outcome();
            outcome.Effects.Add(new SpellEffect
            {
                EffectType = SpellEffectType.Debuff,
                BuffType = BuffType.Endurance,
                Power = 2
            });

            var encounter = RoomEventModel.Build(GuaranteedEvent("Curse", 100f, outcome), party, party.BestStats, 0);

            Assert.AreEqual(0f, encounter.Engaged.ExpectedSustainCost, 0.001f,
                "An affliction is a stat delta, not damage - the closed form cannot price it.");
            Assert.AreEqual(1f, encounter.Engaged.ExpectedAfflictions, 0.001f);
        }

        // ------------------------------------------------------------------
        //  Weighing the pools
        // ------------------------------------------------------------------

        [Test]
        public void Build_StatCheck_WeighsSuccessAndFailureByTheCheckOdds()
        {
            var hero = Hero("A", strength: 12);
            var party = Party(hero);

            var clean = Outcome();
            clean.Gold = 100;

            var bite = Outcome();
            bite.Effects.Add(Damage(10));

            var definition = Make<RoomEventSO>();
            definition.Key = "Tomb";
            definition.GoverningStat = StatType.Strength;
            definition.Difficulty = 12;  // matches the hero, so an even bet.
            definition.SpawnChancePercent = 100f;
            definition.SpawnModifierStat = StatType.None;
            definition.Options = new List<RoomEventOption>
            {
                new RoomEventOption
                {
                    Label = "Force it",
                    Kind = RoomEventOptionKind.StatCheck,
                    Success = new List<RoomEventOutcome> { clean },
                    Failure = new List<RoomEventOutcome> { bite }
                }
            };

            var encounter = RoomEventModel.Build(definition, party, party.BestStats, 0);

            Assert.AreEqual(0.5f, encounter.CheckSuccessChance, 0.001f);
            Assert.AreEqual(5f, encounter.Engaged.ExpectedSustainCost, 0.001f);
            Assert.AreEqual(50f, encounter.Engaged.ExpectedGold, 0.001f);
        }

        [Test]
        public void Build_OutcomeWeightModifiers_BendTheExpectedCost()
        {
            // The acting hero's Luck tilts the pool through RoomEventResolver.EffectiveWeight; the
            // model has to use the same weighting or it measures a pool the game never rolls.
            var party = Party(Hero("Lucky", luck: 1));

            var cheap = Outcome(weight: 1);

            var expensive = Outcome(weight: 1);
            expensive.WeightModifierStat = StatType.Luck;
            expensive.WeightModifierRate = 100f; // 1 Luck doubles this outcome's weight.
            expensive.Effects.Add(Damage(30));

            var encounter = RoomEventModel.Build(
                GuaranteedEvent("Chest", 100f, cheap, expensive), party, party.BestStats, 0);

            // Weights 1 and 2, so the expensive outcome lands two thirds of the time: 20 HP.
            Assert.AreEqual(20f, encounter.Engaged.ExpectedSustainCost, 0.001f);
        }

        [Test]
        public void Build_EngagedIsTheDearestOption_SafestIsTheCheapest()
        {
            var party = Party(Hero("A"));

            var safe = Outcome();
            safe.Gold = 15;

            var risky = Outcome();
            risky.Effects.Add(Damage(12));

            var definition = Make<RoomEventSO>();
            definition.Key = "Treasury";
            definition.GoverningStat = StatType.Luck;
            definition.Difficulty = 0;  // never rolls below the floor; irrelevant to a Guaranteed option.
            definition.SpawnChancePercent = 100f;
            definition.SpawnModifierStat = StatType.None;
            definition.Options = new List<RoomEventOption>
            {
                new RoomEventOption
                {
                    Label = "Take the sure thing",
                    Kind = RoomEventOptionKind.Guaranteed,
                    Success = new List<RoomEventOutcome> { safe }
                },
                new RoomEventOption
                {
                    Label = "Open the chest",
                    Kind = RoomEventOptionKind.Guaranteed,
                    Success = new List<RoomEventOutcome> { risky }
                },
                new RoomEventOption
                {
                    Label = "Walk away",
                    Kind = RoomEventOptionKind.Decline
                }
            };

            var encounter = RoomEventModel.Build(definition, party, party.BestStats, 0);

            Assert.AreEqual("Open the chest", encounter.Engaged.Label);
            Assert.AreEqual("Take the sure thing", encounter.Safest.Label);
        }

        [Test]
        public void Build_DeclineOption_IsNeverEngaged()
        {
            var party = Party(Hero("A"));

            var definition = Make<RoomEventSO>();
            definition.Key = "Nothing";
            definition.SpawnChancePercent = 100f;
            definition.SpawnModifierStat = StatType.None;
            definition.Options = new List<RoomEventOption>
            {
                new RoomEventOption { Label = "Leave it", Kind = RoomEventOptionKind.Decline }
            };

            var encounter = RoomEventModel.Build(definition, party, party.BestStats, 0);

            Assert.IsNull(encounter.Engaged, "Walking away is free, and it is not engaging with the event.");
        }

        // ------------------------------------------------------------------
        //  Placement
        // ------------------------------------------------------------------

        [Test]
        public void BuildForLevel_Occurrences_AreEligibleRoomsTimesTheSpawnChance()
        {
            var party = Party(Hero("A"));
            var outcome = Outcome();
            outcome.Effects.Add(Damage(10));

            var definition = GuaranteedEvent("Trap", 50f, outcome);
            var rooms = Encounters(party, 4f, RoomOffering(definition));

            var events = RoomEventModel.BuildForLevel(rooms, party, Rules(), 0);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(0.5f, events[0].AppearChancePerRoom, 0.001f);
            Assert.AreEqual(2f, events[0].Occurrences, 0.001f);
            Assert.AreEqual(20f, events[0].ExpectedSustainCost, 0.001f);
        }

        [Test]
        public void BuildForLevel_SecondCandidate_OnlyRollsWhenTheFirstMissed()
        {
            // Placement is one pass per room and the first to pass takes it, so listing two events
            // raises the odds of the room offering *something* rather than splitting them.
            var party = Party(Hero("A"));
            var first = GuaranteedEvent("First", 50f, Outcome());
            var second = GuaranteedEvent("Second", 50f, Outcome());

            var rooms = Encounters(party, 1f, RoomOffering(first, second));
            var events = RoomEventModel.BuildForLevel(rooms, party, Rules(), 0);

            Assert.AreEqual(0.5f, events[0].AppearChancePerRoom, 0.001f);
            Assert.AreEqual(0.25f, events[1].AppearChancePerRoom, 0.001f);
        }

        [Test]
        public void BuildForLevel_UnmetRequirements_BlockTheEventWithoutBlockingTheNextCandidate()
        {
            var party = Party(Hero("A", strength: 12));

            var gated = GuaranteedEvent("Gated", 100f, Outcome());
            gated.SpawnRequirements = new List<UnitStat> { new UnitStat(StatType.Intelligence, 99) };

            var open = GuaranteedEvent("Open", 100f, Outcome());

            var rooms = Encounters(party, 1f, RoomOffering(gated, open));
            var events = RoomEventModel.BuildForLevel(rooms, party, Rules(), 0);

            Assert.IsFalse(events[0].RequirementsMet);
            Assert.AreEqual(0f, events[0].AppearChancePerRoom, 0.001f);
            Assert.AreEqual(1f, events[1].AppearChancePerRoom, 0.001f,
                "A gated-out candidate is skipped before its roll, so it cannot consume the room.");
        }

        [Test]
        public void BuildForLevel_RequirementsAreMetPerStat_NotPerHero()
        {
            // 10 Strength AND 15 Intelligence passes for a party covering one each - the rule the
            // party-best-per-stat reading exists for.
            var warrior = Hero("Warrior", strength: 11);
            var acolyte = Hero("Acolyte", strength: 4);
            acolyte.BaseStats[StatType.Intelligence] = 20;

            var party = Party(warrior, acolyte);

            var gated = GuaranteedEvent("Gated", 100f, Outcome());
            gated.SpawnRequirements = new List<UnitStat>
            {
                new UnitStat(StatType.Strength, 10),
                new UnitStat(StatType.Intelligence, 15)
            };

            var events = RoomEventModel.BuildForLevel(
                Encounters(party, 1f, RoomOffering(gated)), party, Rules(), 0);

            Assert.IsTrue(events[0].RequirementsMet);
        }

        [Test]
        public void BuildForLevel_SpawnModifierStat_RaisesTheChanceRelativeToTheBase()
        {
            var party = Party(Hero("Lucky", luck: 10));

            var definition = GuaranteedEvent("Find", 10f, Outcome());
            definition.SpawnModifierStat = StatType.Luck;
            definition.SpawnModifierRate = 5f;

            var events = RoomEventModel.BuildForLevel(
                Encounters(party, 1f, RoomOffering(definition)), party, Rules(), 0);

            // 10 + 10 * (10 * 5 / 100) = 15%.
            Assert.AreEqual(0.15f, events[0].AppearChancePerRoom, 0.0001f);
        }

        [Test]
        public void BuildForLevel_ConnectorRooms_NeverHoldEvents()
        {
            var party = Party(Hero("A"));
            var room = RoomOffering(GuaranteedEvent("Trap", 100f, Outcome()));
            room.Kind = RoomKind.Connector;

            var events = RoomEventModel.BuildForLevel(Encounters(party, 4f, room), party, Rules(), 0);

            Assert.AreEqual(0, events.Count);
        }

        // ------------------------------------------------------------------
        //  Through the run curve
        // ------------------------------------------------------------------

        private RunDefinitionSO RunWith(RoomSO room, int roomsToGenerate)
        {
            var template = Make<LevelDefinitionSO>();
            template.Key = "T";
            template.RoomsToGenerate = roomsToGenerate;
            template.RoomPool = new List<RoomSO> { room };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "L1" }
            };
            return run;
        }

        [Test]
        public void RunCurve_EventCost_LandsInTheLevelsAttrition()
        {
            var party = Party(Hero("A"));

            var outcome = Outcome();
            outcome.Effects.Add(Damage(10));
            outcome.Gold = 20;

            var room = RoomOffering(GuaranteedEvent("Trap", 100f, outcome));
            room.EnemySpawnTable.Add(new EnemySpawnEntry
            {
                Enemy = Goblin(),
                SpawnChance = 1f,
                EvaluationCount = 1
            });

            var curve = RunCurve.Build(RunWith(room, 5), party, Rules());
            var level = curve.Levels[0];

            // 5 generated rooms less the party's start room = 4 eligible, every one taking the event.
            Assert.AreEqual(4f, level.ExpectedEventRooms, 0.001f);
            Assert.AreEqual(40f, level.ExpectedEventHealthCost, 0.001f);
            Assert.AreEqual(80f, level.ExpectedEventGold, 0.001f);
            Assert.AreEqual(level.ExpectedCombatHealthCost + level.ExpectedEventHealthCost,
                level.ExpectedHealthCost, 0.001f);
            Assert.Greater(level.EventAttritionShare, 0f);
        }

        [Test]
        public void RunCurve_EventsRaiseAttritionAboveTheCombatOnlyReading()
        {
            var party = Party(Hero("A"));

            var costly = Outcome();
            costly.Effects.Add(Damage(10));

            var plain = RoomOffering();
            plain.EnemySpawnTable.Add(new EnemySpawnEntry
            {
                Enemy = Goblin(),
                SpawnChance = 1f,
                EvaluationCount = 1
            });

            var trapped = RoomOffering(GuaranteedEvent("Trap", 100f, costly));
            trapped.EnemySpawnTable.Add(new EnemySpawnEntry
            {
                Enemy = Goblin(),
                SpawnChance = 1f,
                EvaluationCount = 1
            });

            var without = RunCurve.Build(RunWith(plain, 5), party, Rules()).Levels[0];
            var with = RunCurve.Build(RunWith(trapped, 5), party, Rules()).Levels[0];

            Assert.AreEqual(without.ExpectedCombatHealthCost, with.ExpectedCombatHealthCost, 0.001f,
                "The fights are identical; only the events differ.");
            Assert.Greater(with.AttritionLoad, without.AttritionLoad,
                "This is the whole point: the curve was optimistic by whatever the events cost.");
        }

        [Test]
        public void RunCurve_EventEngagementRateOfZero_ModelsAPlayerWhoWalksPastThemAll()
        {
            var party = Party(Hero("A"));

            var outcome = Outcome();
            outcome.Effects.Add(Damage(10));

            var room = RoomOffering(GuaranteedEvent("Trap", 100f, outcome));
            room.EnemySpawnTable.Add(new EnemySpawnEntry
            {
                Enemy = Goblin(),
                SpawnChance = 1f,
                EvaluationCount = 1
            });

            var rules = Rules();
            rules.EventEngagementRate = 0f;

            var level = RunCurve.Build(RunWith(room, 5), party, rules).Levels[0];

            Assert.AreEqual(0f, level.ExpectedEventHealthCost, 0.001f);
            Assert.AreEqual(level.ExpectedCombatHealthCost, level.ExpectedHealthCost, 0.001f);
        }

        [Test]
        public void RunCurve_ARescuedHero_TakesOneRoomOutOfTheEventBudget()
        {
            // The captive's room cannot also hold an event (DungeonManager.IsEventEligible), so a
            // level with a rescue has one fewer eligible room.
            var party = Party(Hero("A"));
            var room = RoomOffering(GuaranteedEvent("Trap", 100f, Outcome()));

            var withoutRescue = RunWith(room, 5);
            var withRescue = RunWith(room, 5);
            withRescue.Levels[0].RescueHero = Hero("Captive");

            float plain = RunCurve.Build(withoutRescue, party, Rules()).Levels[0].ExpectedEventRooms;
            float rescued = RunCurve.Build(withRescue, party, Rules()).Levels[0].ExpectedEventRooms;

            Assert.AreEqual(4f, plain, 0.001f);
            Assert.AreEqual(3f, rescued, 0.001f);
        }
    }
}
