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
    /// The mechanics the Warlock needed:<see cref="SpellEffectType.Drain"/>
    /// (Drain Life), <see cref="SpellEffectType.RestoreCharge"/> (Life Tap), and the summon that fights
    /// beside the party (<see cref="SummonKind.JoinParty"/>, the Imp and the Succubus) - its stage
    /// layout and its balance-model mirror.
    /// </summary>
    public class WarlockMechanicsTests
    {
        private static MagicSO Magic(MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test";
            magic.DisplayName = "Test";
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>(effects);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        private static SpellEffect Flat(SpellEffectType type, int power)
        {
            return new SpellEffect { EffectType = type, Power = power, PowerMode = PowerMode.Flat };
        }

        // --- Drain ---------------------------------------------------------------------------------

        [Test]
        public void Drain_HealsTheCasterAShareOfTheDamageDealt()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            caster.Stats.Health = 10;
            var enemy = new MockCombatUnit("Enemy", 1, 0, 100, isHero: false);
            var magic = Magic(MagicTargetType.SingleEnemy, Flat(SpellEffectType.Damage, 20), Flat(SpellEffectType.Drain, 50));

            new EffectResolver().Execute(
                new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { enemy } },
                new CombatBuffTracker());

            Assert.AreEqual(80, enemy.Stats.Health);
            Assert.AreEqual(20, caster.Stats.Health, "Half of 20 damage comes back.");
        }

        [Test]
        public void Drain_AuthoredFirst_StillReadsTheWholeCastsDamage()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            caster.Stats.Health = 10;
            var a = new MockCombatUnit("A", 1, 0, 100, isHero: false);
            var b = new MockCombatUnit("B", 1, 0, 100, isHero: false);
            var magic = Magic(MagicTargetType.AllEnemies, Flat(SpellEffectType.Drain, 25), Flat(SpellEffectType.Damage, 10));

            new EffectResolver().Execute(
                new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { a, b } },
                new CombatBuffTracker());

            Assert.AreEqual(15, caster.Stats.Health, "25% of 20 total damage, whatever the authoring order.");
        }

        [Test]
        public void Drain_ClampsAtMaxHealth_AndFloorsAtOne()
        {
            Assert.AreEqual(1, Assets.Scripts.Cards.Effects.DrainEffectExecutor.Amount(10, 3));
            Assert.AreEqual(0, Assets.Scripts.Cards.Effects.DrainEffectExecutor.Amount(50, 0));

            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            caster.Stats.Health = 39;
            var enemy = new MockCombatUnit("Enemy", 1, 0, 100, isHero: false);
            var magic = Magic(MagicTargetType.SingleEnemy, Flat(SpellEffectType.Damage, 30), Flat(SpellEffectType.Drain, 100));

            new EffectResolver().Execute(
                new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { enemy } },
                new CombatBuffTracker());

            Assert.AreEqual(40, caster.Stats.Health);
        }

        // --- RestoreCharge ---------------------------------------------------------------------------

        private static List<MagicSlot> Slots(params int[] chargesThenMax)
        {
            var slots = new List<MagicSlot>();
            for (int i = 0; i < chargesThenMax.Length; i += 2)
            {
                slots.Add(new MagicSlot
                {
                    Magic = Magic(MagicTargetType.SingleEnemy),
                    Charges = chargesThenMax[i],
                    MaxCharges = chargesThenMax[i + 1]
                });
            }
            return slots;
        }

        [Test]
        public void RestoreCharges_ChosenSlot_GetsItsChargeBack_CappedAtMax()
        {
            var slots = Slots(0, 2, 1, 3);

            Assert.AreEqual(1, EquippedMagicState.RestoreCharges(slots, 0, 1).Added);
            Assert.AreEqual(1, slots[0].Charges);
            Assert.AreEqual(1, EquippedMagicState.RestoreCharges(slots, 0, 5).Added, "Only one was missing.");
            Assert.AreEqual(2, slots[0].Charges);
            Assert.AreEqual(0, EquippedMagicState.RestoreCharges(slots, 0, 1).Added, "Full: nothing to restore.");
        }

        [Test]
        public void RestoreCharges_NoPick_RefillsTheMostSpentSlot()
        {
            var slots = Slots(2, 3, 0, 3, 1, 3);

            var restored = EquippedMagicState.RestoreCharges(slots, SpellcastAction.AnyChargeSlot, 1);

            Assert.AreSame(slots[1].Magic, restored.Magic);
            Assert.AreEqual(1, slots[1].Charges);
        }

        [Test]
        public void RestorableSlots_LeaveOutTheCastingSlot_AndFullOnes()
        {
            var slots = Slots(0, 2, 2, 2, 1, 3);

            CollectionAssert.AreEqual(new[] { 2 }, EquippedMagicState.RestorableSlots(slots, 0),
                "Slot 0 is the Life Tap itself; slot 1 is full.");
            Assert.AreEqual(-1, EquippedMagicState.MostSpentSlot(Slots(2, 2), -1));
        }

        /// <summary>One slot list per unit; a unit with none carries no abilities (a summon).</summary>
        private class FakeBank : IChargeBank
        {
            public readonly Dictionary<ICombatUnit, List<MagicSlot>> ByUnit = new Dictionary<ICombatUnit, List<MagicSlot>>();
            public List<MagicSlot> Slots;

            public FakeBank(List<MagicSlot> slots) { Slots = slots; }

            public bool Carries(ICombatUnit unit)
            {
                return Slots != null || ByUnit.ContainsKey(unit);
            }

            public ChargeRestore Restore(ICombatUnit unit, int slotIndex, int amount, int excludeSlot)
            {
                var slots = ByUnit.TryGetValue(unit, out var own) ? own : Slots;
                return EquippedMagicState.RestoreCharges(slots, slotIndex, amount, excludeSlot);
            }
        }

        [Test]
        public void RestoreCharge_ThroughTheResolver_LandsOnThePickedSlot()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            var bank = new FakeBank(Slots(0, 2, 0, 2));
            var resolver = new EffectResolver { Charges = bank };
            var lifeTap = Magic(MagicTargetType.Self, Flat(SpellEffectType.RestoreCharge, 1));

            resolver.Execute(new SpellcastAction
            {
                Magic = lifeTap, Caster = caster, Targets = new List<ICombatUnit> { caster }, ChargeSlot = 1
            }, new CombatBuffTracker());

            Assert.AreEqual(0, bank.Slots[0].Charges);
            Assert.AreEqual(1, bank.Slots[1].Charges);
        }

        [Test]
        public void RestoreCharge_WithNoBank_IsInert()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            var lifeTap = Magic(MagicTargetType.Self, Flat(SpellEffectType.RestoreCharge, 1));

            var result = new EffectResolver().Execute(
                new SpellcastAction { Magic = lifeTap, Caster = caster, Targets = new List<ICombatUnit> { caster } },
                new CombatBuffTracker());

            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void RestoreCharge_WithNoPick_NeverRefillsTheSlotItIsCastFrom()
        {
            // Life Tap (slot 0) is the most-spent slot and everything else is full. Refilling it would
            // make the cast free, because its charge is spent after the effects resolve.
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            var bank = new FakeBank(Slots(1, 3, 2, 2));
            var resolver = new EffectResolver { Charges = bank };
            var lifeTap = Magic(MagicTargetType.Self, Flat(SpellEffectType.RestoreCharge, 1));

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = lifeTap, Caster = caster, Targets = new List<ICombatUnit> { caster }, CastSlot = 0
            }, new CombatBuffTracker());

            Assert.AreEqual(1, bank.Slots[0].Charges, "The casting slot is never refilled.");
            Assert.AreEqual("Nothing to restore", result.Entries[0].Text);
            Assert.AreEqual(0, EquippedMagicState.RestoreCharges(bank.Slots, 0, 1, excludeSlot: 0).Added,
                "Not even when it is picked by index.");
        }

        [Test]
        public void RestoreCharge_PartyWide_SkipsUnitsWithNoAbilities_AndFullOnesQuietly()
        {
            var caster = new MockCombatUnit("Cleric", 1, 0, 40);
            var spent = new MockCombatUnit("Warlock", 1, 0, 40);
            var imp = new MockCombatUnit("Imp", 1, 0, 20);
            var bank = new FakeBank(null);
            bank.ByUnit[caster] = Slots(2, 2);
            bank.ByUnit[spent] = Slots(2, 2, 0, 2);
            var resolver = new EffectResolver { Charges = bank };
            var renewal = Magic(MagicTargetType.AllAllies, Flat(SpellEffectType.RestoreCharge, 1));

            var result = resolver.Execute(new SpellcastAction
            {
                Magic = renewal, Caster = caster, Targets = new List<ICombatUnit> { caster, spent, imp }, ChargeSlot = 0
            }, new CombatBuffTracker());

            Assert.AreEqual(1, bank.ByUnit[spent][1].Charges, "A pick is ignored across several targets: most-spent wins.");
            Assert.AreEqual(1, result.Entries.Count, "No line for the full caster, none for the imp.");
            Assert.AreSame(spent, result.Entries[0].Target);
        }

        [Test]
        public void Drain_CountsOnlyTheHealthTheHitTook_NotOverkill()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            caster.Stats.Health = 10;
            var enemy = new MockCombatUnit("Enemy", 1, 0, 100, isHero: false);
            enemy.Stats.Health = 4;
            var magic = Magic(MagicTargetType.SingleEnemy, Flat(SpellEffectType.Damage, 30), Flat(SpellEffectType.Drain, 50));

            new EffectResolver().Execute(
                new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { enemy } },
                new CombatBuffTracker());

            Assert.AreEqual(12, caster.Stats.Health, "Half of the 4 health it had, not half of 30.");
        }

        [Test]
        public void Drain_AtFullHealth_FloatsNothing()
        {
            var caster = new MockCombatUnit("Warlock", 1, 0, 40);
            var enemy = new MockCombatUnit("Enemy", 1, 0, 100, isHero: false);
            var magic = Magic(MagicTargetType.SingleEnemy, Flat(SpellEffectType.Damage, 10), Flat(SpellEffectType.Drain, 50));

            var result = new EffectResolver().Execute(
                new SpellcastAction { Magic = magic, Caster = caster, Targets = new List<ICombatUnit> { enemy } },
                new CombatBuffTracker());

            Assert.IsFalse(result.Entries.Exists(e => ReferenceEquals(e.Target, caster)), "No \"+0\" over the caster.");
        }

        // --- the summon that fights beside the party -----------------------------------------------

        [Test]
        public void AllyLayout_StandsInFrontOfTheParty_AndBehindTheEnemies()
        {
            var slots = HeroFormation.AllyLayout(2, 10f, 5f);

            Assert.AreEqual(2, slots.Count);
            foreach (var slot in slots)
            {
                Assert.Greater(slot.x, 10f * HeroFormation.FrontColumnX, "In front of the front rank.");
                Assert.Greater(slot.x, 10f * HeroFormation.SingleColumnX);
                Assert.Less(slot.x, 10f * EnemyFormation.FrontColumnX - 2f, "Well clear of the enemies.");
            }
            Assert.AreNotEqual(slots[0].y, slots[1].y);
            Assert.IsEmpty(HeroFormation.AllyLayout(0, 10f, 5f));
        }

        [Test]
        public void JoinParty_IsImplemented_AndDescribedAsFightingBeside()
        {
            var imp = AllySummon(turns: 3);
            Assert.IsTrue(new SummonSlot { Summon = imp, Charges = 1 }.CanUse);
            StringAssert.StartsWith("Fights beside the party", SummonOps.Describe(imp, null));
        }

        private static SummonSO AllySummon(int turns)
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "TestImp";
            summon.Kind = SummonKind.JoinParty;
            summon.BaseCharges = 1;
            summon.TurnsActive = turns;
            summon.StatPercents = new StatBlock(
                new UnitStat(StatType.MaxHealth, 100),
                new UnitStat(StatType.Strength, 100),
                new UnitStat(StatType.Agility, 100));
            return summon;
        }

        private static SimUnit SimHero(int strength, int health, int agility)
        {
            return new SimUnit
            {
                DisplayName = "warlock",
                HeroKey = "warlock",
                IsHero = true,
                Stats = TestStats.Make(strength, 0, health, agility),
                Effective = TestStats.Block(strength, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = strength,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit SimEnemy(int attack, int health, int agility)
        {
            return new SimUnit
            {
                DisplayName = "enemy",
                IsHero = false,
                Archetype = EnemyArchetype.Aggressor,
                Stats = TestStats.Make(attack, 0, health, agility),
                Effective = TestStats.Block(attack, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static EncounterSimulator.FloorOutcome Run(SummonSO ally, bool summons, SimUnit enemy)
        {
            var hero = SimHero(10, 100, 10);
            hero.Summons.Add(new SimSummonSlot
            {
                Summon = ally,
                Grant = new SummonGrant { Key = ally.Key },
                Charges = 1,
                MaxCharges = 1
            });
            var party = new PartyBaseline { SourceLabel = "test" };
            party.Heroes.Add(new HeroBaseline { Effective = hero.Effective.Clone(), Unit = hero });
            return EncounterSimulator.RunFloor(party, new List<IList<SimUnit>> { new List<SimUnit> { enemy } },
                new EncounterSimulator.FloorSimSettings
                {
                    Trials = 10,
                    Seed = 4242,
                    MaxTurns = 300,
                    Policy = SimPolicy.Adaptive,
                    Combos = new List<MagicComboSO>(),
                    UseSummons = summons
                });
        }

        [Test]
        public void Simulator_CallsTheAlly_AndItSharesTheFight()
        {
            // A bound demon as strong as the hero, for six turns: two attackers where there was one,
            // and a second body to take blows. The party ends the fight healthier with it.
            var enemy = SimEnemy(attack: 8, health: 120, agility: 10);
            var with = Run(AllySummon(turns: 6), true, enemy);
            var without = Run(AllySummon(turns: 6), false, enemy);

            Assert.AreEqual(1f, with.AverageSummonsUsed, 1e-4f);
            Assert.Greater(with.AverageEndHealthFraction, without.AverageEndHealthFraction);
        }
        // --- SimAllies: the balance model's ally bookkeeping ----------------------------------------

        private static (SimAllies allies, TurnManager clock, SimUnit summoner, List<SimUnit> enemies) Field()
        {
            var summoner = SimHero(10, 100, 10);
            var enemies = new List<SimUnit> { SimEnemy(5, 50, 10) };
            var clock = new TurnManager();
            clock.Initialize(new List<ICombatUnit> { summoner, enemies[0] });
            return (new SimAllies(clock, enemies), clock, summoner, enemies);
        }

        [Test]
        public void SimAllies_AnAllyActsNext_JoinsTheTargets_AndLeavesAfterItsStay()
        {
            var (allies, clock, summoner, enemies) = Field();
            var ally = allies.Arrive(summoner, AllySummon(turns: 2), new SummonGrant { Key = "TestImp" });

            Assert.AreSame(ally.Unit, clock.GetNextUnit(), "It acts at once, ahead of the clock.");
            CollectionAssert.Contains(allies.With(new List<SimUnit> { summoner }), ally.Unit);
            Assert.IsTrue(allies.HasOut(summoner));

            allies.AfterTurn(ally.Unit);
            Assert.AreEqual(1, allies.All.Count, "One turn of two.");
            allies.AfterTurn(ally.Unit);
            Assert.AreEqual(0, allies.All.Count, "Its stay is over.");
            Assert.IsFalse(clock.GetTurnOrder(6).Contains(ally.Unit), "And it is off the clock.");
        }

        [Test]
        public void SimAllies_ASummonerWhoFalls_TakesTheirAllyAtOnce_WhoeverTookTheTurn()
        {
            // The tick path: the summoner dies at the start of their own turn and the loop moves on.
            var (allies, clock, summoner, enemies) = Field();
            var ally = allies.Arrive(summoner, AllySummon(turns: 5), new SummonGrant { Key = "TestImp" });
            enemies[0].ChargeTarget = ally.Unit;

            summoner.Stats.Health = 0;
            allies.AfterTurn(summoner);

            Assert.IsEmpty(allies.All);
            Assert.IsFalse(clock.GetTurnOrder(6).Contains(ally.Unit), "It never acts again.");
            Assert.IsNull(enemies[0].ChargeTarget, "A wind-up aimed at it lets go.");
        }

        [Test]
        public void SimAllies_CallingTheSameAgain_SendsTheFirstHome_AndAReplacementSendsEveryoneHome()
        {
            var (allies, clock, summoner, enemies) = Field();
            var imp = AllySummon(turns: 5);
            var first = allies.Arrive(summoner, imp, new SummonGrant { Key = "TestImp" });
            var second = allies.Arrive(summoner, imp, new SummonGrant { Key = "TestImp" });

            Assert.AreEqual(1, allies.All.Count);
            Assert.AreSame(second, allies.All[0]);
            Assert.IsTrue(first.Stay.HasLeft);

            allies.DismissAll();
            Assert.IsEmpty(allies.All);
        }

        [Test]
        public void SimAllies_OneOfEachKind_ADifferentSummonJoinsBesideTheFirst()
        {
            var (allies, clock, summoner, enemies) = Field();
            var spawn = AllySummon(turns: 5);
            var watcher = AllySummon(turns: 5);
            allies.Arrive(summoner, spawn, new SummonGrant { Key = "TestImp" });
            allies.Arrive(summoner, watcher, new SummonGrant { Key = "TestImp" });

            Assert.AreEqual(2, allies.All.Count, "The Spawn and the Watcher stand together.");
            Assert.IsTrue(allies.HasOut(summoner, spawn));
            Assert.IsTrue(allies.HasOut(summoner, watcher));
            Assert.IsFalse(allies.HasOut(summoner, AllySummon(turns: 5)), "A third kind is not out yet.");
        }

        // --- The Blood Idol and the Abyssal Nightmare ------------------------------------------------

        private static SummonSO RotatingIdol(params MagicSO[] rites)
        {
            var idol = AllySummon(turns: 4);
            idol.RotateActions = true;
            idol.Actions = new List<MagicSO>(rites);
            return idol;
        }

        [Test]
        public void RotatingAbility_CyclesThroughTheActions_OnePerTurn()
        {
            var a = Magic(MagicTargetType.AllAllies, Flat(SpellEffectType.Heal, 1));
            var b = Magic(MagicTargetType.AllAllies, Flat(SpellEffectType.Heal, 2));
            var idol = RotatingIdol(a, b);

            Assert.AreSame(a, SummonOps.RotatingAbility(idol, 0));
            Assert.AreSame(b, SummonOps.RotatingAbility(idol, 1));
            Assert.AreSame(a, SummonOps.RotatingAbility(idol, 2));
            idol.RotateActions = false;
            Assert.IsNull(SummonOps.RotatingAbility(idol, 0), "Only a rotating summon rotates.");
        }

        [Test]
        public void SimAllies_ARotatingAlly_MovesToItsNextRiteEachTurn()
        {
            var (allies, clock, summoner, enemies) = Field();
            var a = Magic(MagicTargetType.AllAllies, Flat(SpellEffectType.Heal, 1));
            var b = Magic(MagicTargetType.AllAllies, Flat(SpellEffectType.Heal, 2));
            var ally = allies.Arrive(summoner, RotatingIdol(a, b), new SummonGrant { Key = "TestImp" });

            Assert.AreSame(a, ally.Attack);
            allies.AfterTurn(ally.Unit);
            Assert.AreSame(b, ally.Attack);
            allies.AfterTurn(ally.Unit);
            Assert.AreSame(a, ally.Attack);
        }

        [Test]
        public void HealthCost_IsAShareOfMaxHealth_AndRefusedWhenItWouldKill()
        {
            var idol = AllySummon(turns: 4);
            idol.SummonerHealthCostPercent = 20;
            var cultist = new MockCombatUnit("Cultist", 1, 0, 50);

            Assert.AreEqual(10, SummonOps.HealthCost(idol, cultist));
            Assert.IsTrue(SummonOps.CanAfford(idol, cultist));
            cultist.Stats.Health = 10;
            Assert.IsFalse(SummonOps.CanAfford(idol, cultist), "Paying 10 of 10 would leave him at 0.");
            idol.SummonerHealthCostPercent = 0;
            Assert.AreEqual(0, SummonOps.HealthCost(idol, cultist));
            Assert.IsTrue(SummonOps.CanAfford(idol, cultist), "A free summon is always affordable.");
        }

        [Test]
        public void BuildRandomCastable_CarriesTheRandomEffects_LengthenedByDuration()
        {
            var nightmare = ScriptableObject.CreateInstance<SummonSO>();
            nightmare.Key = "TestNightmare";
            nightmare.Kind = SummonKind.SpecialAttack;
            nightmare.RandomTargetEffects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Debuff, BuffType = BuffType.Silenced, Power = 1, Duration = 2 }
            };

            var castable = SummonOps.BuildRandomCastable(nightmare, new SummonGrant { Key = "TestNightmare", DurationBonus = 1 });

            Assert.AreEqual(MagicTargetType.SingleEnemy, castable.TargetType);
            Assert.AreEqual(BuffType.Silenced, castable.Effects.Single().BuffType);
            Assert.AreEqual(3, castable.Effects.Single().Duration);
            nightmare.RandomTargetEffects.Clear();
            Assert.IsNull(SummonOps.BuildRandomCastable(nightmare, null));
        }
    }
}
