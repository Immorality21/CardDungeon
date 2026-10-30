using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
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
    /// The Ultra gauge and the transform (<see cref="UltraOps"/>, COMBAT_DEPTH §13), the squad summon
    /// (<see cref="SummonOps.SquadFor"/>, the Warlock's Demon Army) and the content guards for both.
    /// </summary>
    public class UltraTests
    {
        // --- the gauge -------------------------------------------------------------------------

        [Test]
        public void Gauge_FillsWhenTheFillShareOfTheBarIsLost()
        {
            int max = 50;
            int share = Mathf.CeilToInt(max * UltraOps.FillShare);

            Assert.IsTrue(UltraOps.IsFull(UltraOps.Add(0, UltraOps.GainFor(share, max))));
            Assert.IsFalse(UltraOps.IsFull(UltraOps.Add(0, UltraOps.GainFor(share / 2, max))));
            Assert.AreEqual(UltraOps.Max, UltraOps.Add(90, 500), "Clamped at full.");
        }

        [Test]
        public void Gauge_AnyRealCreditMovesIt_AndNothingElseDoes()
        {
            Assert.Greater(UltraOps.GainFor(1, 1000), 0);
            Assert.AreEqual(0, UltraOps.GainFor(0, 50));
            Assert.AreEqual(0, UltraOps.GainFor(-10, 50), "A negative credit is no credit.");
        }

        private static Dictionary<ICombatUnit, float> Credited(HealthChange change)
        {
            var credited = new Dictionary<ICombatUnit, float>();
            UltraOps.Credit(change, (unit, amount) =>
            {
                credited.TryGetValue(unit, out float sum);
                credited[unit] = sum + amount;
            });
            return credited;
        }

        [Test]
        public void Credit_DamageFillsTheHeroHitAndTheHeroWhoHit_TakingFastest()
        {
            var hero = new MockCombatUnit("Hero", 10, 5, 50);
            var enemy = new MockCombatUnit("Enemy", 10, 5, 50, isHero: false);

            var taken = Credited(new HealthChange
            {
                Target = hero, Source = new HealthSource(enemy, HealthCause.Attack), Before = 50, After = 40, Landed = 10
            });
            var dealt = Credited(new HealthChange
            {
                Target = enemy, Source = new HealthSource(hero, HealthCause.Ability), Before = 50, After = 40, Landed = 10
            });

            Assert.AreEqual(10f * UltraOps.TakenWeight, taken[hero]);
            // The enemy is credited for its blow too; a caller keeps only the units that hold a gauge.
            Assert.AreEqual(10f * UltraOps.DealtWeight, taken[enemy]);
            Assert.AreEqual(10f * UltraOps.DealtWeight, dealt[hero]);
            Assert.Greater(UltraOps.TakenWeight, UltraOps.DealtWeight, "Taking damage fills fastest (owner, 2026-10-30).");
        }

        [Test]
        public void Credit_HealingFillsTheHealer_AndABloodPriceCountsAsTaken()
        {
            var healer = new MockCombatUnit("Cleric", 5, 5, 40);
            var ally = new MockCombatUnit("Warrior", 10, 5, 60);

            var healed = Credited(new HealthChange
            {
                Target = ally, Source = new HealthSource(healer, HealthCause.Ability), Before = 20, After = 35, Healed = 15
            });
            var price = Credited(new HealthChange
            {
                Target = healer, Source = new HealthSource(healer, HealthCause.Cost), Before = 40, After = 36, Landed = 4
            });

            Assert.AreEqual(15f * UltraOps.HealedWeight, healed[healer]);
            Assert.IsFalse(healed.ContainsKey(ally), "Being healed fills nothing.");
            Assert.AreEqual(4f * UltraOps.TakenWeight, price[healer], "The Warlock's blood price still feeds his gauge.");
        }

        [Test]
        public void Credit_FormsPotionsAndSacrificesFillNothing()
        {
            var hero = new MockCombatUnit("Hero", 10, 5, 50);
            foreach (var cause in new[] { HealthCause.Form, HealthCause.Item, HealthCause.Sacrifice, HealthCause.Clamp })
            {
                var credited = Credited(new HealthChange
                {
                    Target = hero, Source = new HealthSource(hero, cause), Before = 50, After = 30, Landed = 20, Healed = 20
                });
                Assert.IsEmpty(credited, cause.ToString());
            }
        }

        [Test]
        public void GaugeState_IsKeptForTheRun_AndABenchedHeroKeepsTheirs()
        {
            var floorOne = new UltraGaugeState();
            floorOne.Set("warrior", 70);
            floorOne.Set("cleric", 500);
            Assert.AreEqual(UltraOps.Max, floorOne.Get("cleric"), "Clamped at full.");
            Assert.AreEqual(0, floorOne.Get("rogue"), "A hero never seen starts empty.");

            // Floor two fields only the Warrior; the Cleric sat it out.
            var floorTwo = new UltraGaugeState();
            floorTwo.Restore(floorOne.GetSaveData());
            floorTwo.Set("warrior", 10);
            var merged = UltraGaugeState.MergeSaveData(floorOne.GetSaveData(),
                new List<UltraGaugeSaveData> { new UltraGaugeSaveData { HeroKey = "warrior", Gauge = 10 } });

            var floorThree = new UltraGaugeState();
            floorThree.Restore(merged);
            Assert.AreEqual(10, floorThree.Get("warrior"));
            Assert.AreEqual(UltraOps.Max, floorThree.Get("cleric"));
        }

        [Test]
        public void KeepShare_HalfHealthStaysHalf_IntoTheFormAndBack()
        {
            Assert.AreEqual(30, UltraOps.KeepShare(20, 40, 60));
            Assert.AreEqual(20, UltraOps.KeepShare(30, 60, 40));
            Assert.AreEqual(1, UltraOps.KeepShare(1, 100, 10), "A living hero never rounds down to dead.");
            Assert.AreEqual(0, UltraOps.KeepShare(0, 40, 60), "A dead one stays dead.");
        }

        // --- the squad -------------------------------------------------------------------------

        private static SummonSO Troop(string key)
        {
            var troop = ScriptableObject.CreateInstance<SummonSO>();
            troop.Key = key;
            troop.DisplayName = key;
            troop.Kind = SummonKind.ReplaceParty;
            return troop;
        }

        private static SummonSO Army(params SummonSO[] tiers)
        {
            var army = ScriptableObject.CreateInstance<SummonSO>();
            army.Key = "Army";
            army.Kind = SummonKind.ReplaceParty;
            army.SquadTiers = tiers.ToList();
            army.SquadSize = 3;
            army.MaxSquadSize = 4;
            return army;
        }

        [Test]
        public void Squad_StartsAsItsBaseSize_OfTheWeakestTier()
        {
            var imp = Troop("Imp");
            var squad = SummonOps.SquadFor(Army(imp, Troop("Succubus")), new SummonGrant());

            Assert.AreEqual(3, squad.Count);
            Assert.That(squad.All(t => t == imp));
        }

        [Test]
        public void Squad_GrowsWithSizeNodes_UpToItsCap()
        {
            var army = Army(Troop("Imp"));
            Assert.AreEqual(4, SummonOps.SquadFor(army, new SummonGrant { SizeBonus = 1 }).Count);
            Assert.AreEqual(4, SummonOps.SquadFor(army, new SummonGrant { SizeBonus = 5 }).Count);
        }

        [Test]
        public void Squad_PromotionsRaiseTheWeakestTroop_FrontRankFirst()
        {
            var imp = Troop("Imp");
            var succubus = Troop("Succubus");
            var felguard = Troop("Felguard");

            var two = SummonOps.SquadFor(Army(imp, succubus, felguard), new SummonGrant { Promotions = 2 });
            CollectionAssert.AreEqual(new[] { succubus, succubus, imp }, two);

            var four = SummonOps.SquadFor(Army(imp, succubus, felguard), new SummonGrant { Promotions = 4 });
            CollectionAssert.AreEqual(new[] { felguard, succubus, succubus }, four,
                "The weakest goes up first, so a second tier is only reached once everyone has the first.");

            var maxed = SummonOps.SquadFor(Army(imp, succubus), new SummonGrant { Promotions = 99 });
            Assert.That(maxed.All(t => t == succubus), "Promotions stop at the top tier.");
        }

        [Test]
        public void Squad_ASingleUnitSummonBringsItself()
        {
            var golem = Troop("Golem");
            CollectionAssert.AreEqual(new[] { golem }, SummonOps.SquadFor(golem, new SummonGrant()));
        }

        [Test]
        public void DescribeSquad_ReadsLikeAnArmy()
        {
            var imp = Troop("Imp");
            var succubus = Troop("Succubus");
            Assert.AreEqual("3 Imps", SummonOps.DescribeSquad(new List<SummonSO> { imp, imp, imp }));
            Assert.AreEqual("2 Succubi and an Imp", SummonOps.DescribeSquad(new List<SummonSO> { succubus, succubus, imp }));
        }

        [Test]
        public void GridNodes_AddSizeAndPromotions_AndTeachUltras()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "army";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "army", Kind = SphereNodeKind.Summon, GrantedSummonKey = "Army", UnlockedByDefault = true },
                new SphereGridNode { Key = "size", Kind = SphereNodeKind.SummonSize, GrantedSummonKey = "Army", SummonAmount = 1, Neighbors = new List<string> { "army" } },
                new SphereGridNode { Key = "promo", Kind = SphereNodeKind.SummonPromote, GrantedSummonKey = "Army", SummonAmount = 2, Neighbors = new List<string> { "army" } },
                new SphereGridNode { Key = "ultra", Kind = SphereNodeKind.Ultra, GrantedUltraKey = "DemonForm", Neighbors = new List<string> { "army" } }
            };
            var active = new List<string> { "size", "promo", "ultra" };

            var grant = SphereGridOps.SummonsForNodes(grid, active).Single();
            Assert.AreEqual(1, grant.SizeBonus);
            Assert.AreEqual(2, grant.Promotions);
            CollectionAssert.AreEqual(new[] { "DemonForm" }, SphereGridOps.UltrasForNodes(grid, active));
            Assert.IsEmpty(SphereGridOps.UltrasForNodes(grid, new List<string>()));
        }

        // --- content ---------------------------------------------------------------------------

        private static List<UltraSO> AllUltras()
        {
            return AssetDatabase.FindAssets("t:UltraSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<UltraSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(u => u != null)
                .ToList();
        }

        [Test]
        public void EveryUltra_IsInTheCatalog_AndEveryUltraNodeNamesOne()
        {
            var catalog = UltraCatalogSO.Load();
            Assert.IsNotNull(catalog, "Assets/Resources/UltraCatalog.asset is missing.");
            var missing = AllUltras().Where(u => !catalog.Ultras.Contains(u)).Select(u => u.name).ToList();
            Assert.IsEmpty(missing, "Ultra assets not in the catalog: " + string.Join(", ", missing));

            foreach (var grid in AssetDatabase.FindAssets("t:SphereGridSO")
                         .Select(g => AssetDatabase.LoadAssetAtPath<SphereGridSO>(AssetDatabase.GUIDToAssetPath(g))))
            {
                foreach (var node in grid.Nodes.Where(n => n != null && n.Kind == SphereNodeKind.Ultra))
                {
                    Assert.IsNotNull(catalog.Find(node.GrantedUltraKey),
                        $"{grid.name}/{node.Key} names Ultra '{node.GrantedUltraKey}', which is not in the catalog.");
                }
            }
        }

        [Test]
        public void UltraAbilities_AreNeverAHerosMagic()
        {
            var keys = UltraOps.AbilityKeys(AllUltras());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MagicCatalog.prefab");
            var catalog = prefab.GetComponent<MagicCatalog>();
            var listed = catalog.AllMagic.Where(m => m != null && keys.Contains(m.Key)).Select(m => m.Key).ToList();
            Assert.IsEmpty(listed, "The hero magic catalog lists an Ultra's ability: " + string.Join(", ", listed));
        }
        // --- the balance model's Ultras (SimUltras) ---------------------------------------------

        private static UltraSO DemonForm()
        {
            var ultra = ScriptableObject.CreateInstance<UltraSO>();
            ultra.Key = "TestForm";
            ultra.Kind = UltraKind.Transform;
            ultra.Turns = 2;
            ultra.MaxHealthPercent = 50;
            ultra.AttackDamageType = DamageType.Shadow;
            ultra.Abilities = new List<MagicSO>();
            return ultra;
        }

        private static SimUnit SimHero(int health, UltraSO ultra)
        {
            var hero = new SimUnit
            {
                DisplayName = "warlock",
                IsHero = true,
                Stats = TestStats.Make(5, 0, health, 10),
                Effective = TestStats.Block(5, 0, health, 10),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = 5
            };
            hero.Ultras.Add(ultra);
            return hero;
        }

        /// <summary>A blow of <paramref name="amount"/> on <paramref name="hero"/>, raised on the fight's
        /// stream as HealthOps raises it.</summary>
        private static void Hit(CombatEvents events, SimUnit hero, int amount)
        {
            var enemy = new SimUnit { DisplayName = "enemy", Stats = TestStats.Make(1, 0, 50, 5), Effective = TestStats.Block(1, 0, 50, 5) };
            int before = hero.Stats.Health;
            hero.Stats.Health = before - amount;
            events.Publish(new HealthChange
            {
                Target = hero, Source = new HealthSource(enemy, HealthCause.Attack),
                Before = before, After = hero.Stats.Health, Landed = amount
            });
        }

        [Test]
        public void SimUltras_FillOnLoss_TransformKeepsTheShare_AndComesOff()
        {
            var form = DemonForm();
            var hero = SimHero(100, form);
            var ultras = new SimUltras(new[] { hero }) { Events = new CombatEvents() };

            Hit(ultras.Events, hero, 60);    // lost 60 of 100: the fill share
            ultras.Read();
            Assert.AreSame(form, ultras.Ready(hero));

            ultras.Use(hero, form, new List<SimUnit> { hero }, new List<SimUnit>(), new CombatBuffTracker(), new EffectResolver());
            Assert.AreEqual(0, ultras.Gauge(hero));
            Assert.AreEqual(150, hero.GetEffectiveStat(StatType.MaxHealth));
            Assert.AreEqual(60, hero.Stats.Health, "40% of the bar before, 40% after.");
            Assert.AreEqual(DamageType.Shadow, hero.AttackDamageType);
            ultras.Read();
            Assert.AreEqual(0, ultras.Gauge(hero), "A bigger bar is not damage taken.");

            ultras.AfterTurn(hero);          // the turn it was taken on does not count
            ultras.AfterTurn(hero);
            Assert.IsTrue(ultras.IsTransformed(hero));
            ultras.AfterTurn(hero);          // the second of its two turns
            Assert.IsFalse(ultras.IsTransformed(hero));
            Assert.AreEqual(100, hero.GetEffectiveStat(StatType.MaxHealth));
            Assert.AreEqual(40, hero.Stats.Health);
            Assert.AreEqual(DamageType.Normal, hero.AttackDamageType);
        }

        [Test]
        public void SimUltras_TheGaugeCarriesIntoTheNextRoom()
        {
            var hero = SimHero(100, DemonForm());
            var roomOne = new SimUltras(new[] { hero }) { Events = new CombatEvents() };
            Hit(roomOne.Events, hero, 30);
            roomOne.Read();
            int built = roomOne.Gauge(hero);
            Assert.Greater(built, 0);
            Assert.IsFalse(UltraOps.IsFull(built));

            var roomTwo = new SimUltras(new[] { hero }) { Events = new CombatEvents() };
            Assert.AreEqual(built, roomTwo.Gauge(hero), "Kept for the run, not emptied per fight.");
            Hit(roomTwo.Events, hero, 30);
            roomTwo.Read();
            Assert.IsTrue(UltraOps.IsFull(roomTwo.Gauge(hero)), "Half the fill share in each of two rooms fills it.");
        }

        [Test]
        public void SimUltras_EndAll_TakesTheFormOffBetweenRooms()
        {
            var form = DemonForm();
            var hero = SimHero(100, form);
            var ultras = new SimUltras(new[] { hero });
            ultras.Use(hero, form, new List<SimUnit> { hero }, new List<SimUnit>(), new CombatBuffTracker(), new EffectResolver());

            ultras.EndAll();

            Assert.AreEqual(100, hero.GetEffectiveStat(StatType.MaxHealth));
            Assert.AreEqual(100, hero.Stats.Health);
        }

        [Test]
        public void Strike_LandsOnEveryEnemy_ThroughTheResolver()
        {
            var strike = ScriptableObject.CreateInstance<UltraSO>();
            strike.Key = "TestStrike";
            strike.Kind = UltraKind.Strike;
            strike.TargetType = MagicTargetType.AllEnemies;
            strike.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 10, PowerMode = PowerMode.Flat }
            };
            var hero = SimHero(100, strike);
            var enemies = new List<SimUnit>
            {
                new SimUnit { DisplayName = "a", Stats = TestStats.Make(1, 0, 50, 5), Effective = TestStats.Block(1, 0, 50, 5) },
                new SimUnit { DisplayName = "b", Stats = TestStats.Make(1, 0, 50, 5), Effective = TestStats.Block(1, 0, 50, 5) }
            };
            var ultras = new SimUltras(new[] { hero });

            ultras.Use(hero, strike, new List<SimUnit> { hero }, enemies, new CombatBuffTracker(), new EffectResolver());

            Assert.That(enemies.All(e => e.Stats.Health < 50), "Both enemies were hit.");
            StringAssert.StartsWith("All enemies:", UltraOps.Describe(strike));
        }
        // --- Sacrifice ---------------------------------------------------------------------------

        private static UltraSO Rite(out MagicSO strike, out MagicSO whisper)
        {
            strike = ScriptableObject.CreateInstance<MagicSO>();
            strike.Key = "TestMaw";
            whisper = ScriptableObject.CreateInstance<MagicSO>();
            whisper.Key = "TestWhisper";
            var creature = ScriptableObject.CreateInstance<SummonSO>();
            creature.Key = "TestHorror";
            creature.Kind = SummonKind.JoinParty;
            creature.StatPercents = new StatBlock(
                new UnitStat(StatType.MaxHealth, 200),
                new UnitStat(StatType.Strength, 130),
                new UnitStat(StatType.Agility, 130));
            var rite = ScriptableObject.CreateInstance<UltraSO>();
            rite.Key = "TestRite";
            rite.Kind = UltraKind.Sacrifice;
            rite.Creature = creature;
            rite.StatAbilities = new List<UltraStatAbility>
            {
                new UltraStatAbility { Stat = StatType.Strength, Ability = strike },
                new UltraStatAbility { Stat = StatType.Spirit, Ability = whisper }
            };
            return rite;
        }

        [Test]
        public void Sacrifice_PicksTheAttackByTheHeroesHighestStat_TiesToTheFirst()
        {
            var rite = Rite(out var strike, out var whisper);

            Assert.AreSame(strike, UltraOps.PickStatAbility(rite, s => s == StatType.Strength ? 12 : 4));
            Assert.AreSame(whisper, UltraOps.PickStatAbility(rite, s => s == StatType.Spirit ? 12 : 4));
            Assert.AreSame(strike, UltraOps.PickStatAbility(rite, s => 7), "A tie goes to the first listed.");
            Assert.IsTrue(UltraOps.NeedsTarget(rite));
        }

        [Test]
        public void SimSacrifice_TakesTheMostWoundedHero_NeverTheLast_AndTheHorrorOutlastsThem()
        {
            var rite = Rite(out var strike, out _);
            var cultist = SimHero(100, rite);
            var warrior = SimHero(100, DemonForm());
            warrior.Stats.Health = 20;                       // 20%: under the threshold
            var heroes = new List<SimUnit> { cultist, warrior };

            Assert.AreSame(warrior, SimUltras.SacrificeVictim(heroes));
            Assert.IsNull(SimUltras.SacrificeVictim(new List<SimUnit> { warrior }), "Never the last hero standing.");

            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { cultist, warrior });
            var allies = new SimAllies(clock, new List<SimUnit>());
            var ultras = new SimUltras(heroes) { Events = new CombatEvents() };
            Hit(ultras.Events, cultist, 60);
            ultras.Read();                                   // 60 lost: the gauge is full
            Assert.AreSame(rite, ultras.Ready(cultist, heroes));

            ultras.Use(cultist, rite, heroes, new List<SimUnit>(), new CombatBuffTracker(), new EffectResolver(), heroes, allies, clock);

            Assert.IsFalse(warrior.IsAlive, "The sacrificed hero is down.");
            var horror = allies.All.Single();
            Assert.AreEqual(200, horror.Unit.GetEffectiveStat(StatType.MaxHealth), "Built off the hero: 200% of their 100.");
            Assert.AreEqual(200, horror.Unit.Stats.Health, "At full health, whatever the hero had left (20).");
            Assert.AreSame(strike, horror.Attack);
            allies.AfterTurn(cultist);
            Assert.AreEqual(1, allies.All.Count, "Its hero being down does not send it home.");
        }
    }
}
