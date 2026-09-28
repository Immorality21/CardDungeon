using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Summons (docs/plans/SPECIALIZATION.md §4b): what the grid teaches, what the upgrades add, the
    /// per-run charges, and the percentage Strength buff the Bloodfang Boar lands.
    /// </summary>
    public class SummonTests
    {
        // --- fixtures ------------------------------------------------------

        private static SphereGridNode Node(string key, SphereNodeKind kind, string summon = null, int amount = 1, params string[] neighbors)
        {
            return new SphereGridNode
            {
                Key = key,
                Kind = kind,
                XpCost = 10,
                GrantedSummonKey = summon,
                SummonAmount = amount,
                Neighbors = new List<string>(neighbors)
            };
        }

        /// <summary>start — boar — fury(+10) — endurance(+1) — charge(+1), plus a stray upgrade for a summon nobody teaches.</summary>
        private static SphereGridSO BoarGrid()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>
            {
                Node("start", SphereNodeKind.Stat, null, 1, "boar"),
                Node("boar", SphereNodeKind.Summon, "Boar", 1, "fury"),
                Node("fury", SphereNodeKind.SummonPower, "Boar", 10, "endurance"),
                Node("endurance", SphereNodeKind.SummonDuration, "Boar", 1, "charge"),
                Node("charge", SphereNodeKind.SummonCharge, "Boar", 1),
                Node("stray", SphereNodeKind.SummonPower, "Nobody", 99, "start"),
            };
            return grid;
        }

        private static SummonSO Boar()
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "Boar";
            summon.DisplayName = "Bloodfang Boar";
            summon.TargetType = MagicTargetType.AllAllies;
            summon.BaseCharges = 1;
            summon.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Buff, Power = 50, PowerMode = PowerMode.PercentOfTargetStat, BuffType = BuffType.Strength, Duration = 3 }
            };
            return summon;
        }

        // --- what the grid teaches -------------------------------------------

        [Test]
        public void SummonsForNodes_NothingActivated_TeachesNothing()
        {
            Assert.IsEmpty(SphereGridOps.SummonsForNodes(BoarGrid(), new[] { "start" }));
        }

        [Test]
        public void SummonsForNodes_SummonNode_TeachesItWithNoBonuses()
        {
            var grants = SphereGridOps.SummonsForNodes(BoarGrid(), new[] { "start", "boar" });

            Assert.AreEqual(1, grants.Count);
            Assert.AreEqual("Boar", grants[0].Key);
            Assert.AreEqual(0, grants[0].PowerBonus + grants[0].DurationBonus + grants[0].ChargeBonus);
        }

        [Test]
        public void SummonsForNodes_UpgradeNodes_AddTheirAmounts()
        {
            var grants = SphereGridOps.SummonsForNodes(BoarGrid(), new[] { "start", "boar", "fury", "endurance", "charge" });

            Assert.AreEqual(10, grants[0].PowerBonus);
            Assert.AreEqual(1, grants[0].DurationBonus);
            Assert.AreEqual(1, grants[0].ChargeBonus);
        }

        [Test]
        public void SummonsForNodes_UpgradeForAnUnknownSummon_DoesNothing()
        {
            var grants = SphereGridOps.SummonsForNodes(BoarGrid(), new[] { "start", "boar", "stray" });

            Assert.AreEqual(1, grants.Count, "The stray upgrade must not invent a summon.");
            Assert.AreEqual(0, grants[0].PowerBonus, "Nor may it upgrade a different one.");
        }

        // --- what a summon does ------------------------------------------------

        [Test]
        public void EffectsFor_AppliesPowerAndDuration_WithoutTouchingTheAsset()
        {
            var boar = Boar();
            var grant = new SummonGrant { Key = "Boar", PowerBonus = 10, DurationBonus = 1 };

            var effects = SummonOps.EffectsFor(boar, grant);

            Assert.AreEqual(60, effects[0].Power);
            Assert.AreEqual(4, effects[0].Duration);
            Assert.AreEqual(50, boar.Effects[0].Power, "The asset is never modified.");
            Assert.AreEqual(3, boar.Effects[0].Duration);
        }

        [Test]
        public void MaxCharges_IsBasePlusUpgrades_AndNeverBelowOne()
        {
            var boar = Boar();
            Assert.AreEqual(1, SummonOps.MaxCharges(boar, null));
            Assert.AreEqual(2, SummonOps.MaxCharges(boar, new SummonGrant { ChargeBonus = 1 }));
            boar.BaseCharges = 0;
            Assert.AreEqual(1, SummonOps.MaxCharges(boar, null));
        }

        [Test]
        public void BuildCastable_IsAPrefixedTaglessMagic()
        {
            var castable = SummonOps.BuildCastable(Boar(), new SummonGrant { Key = "Boar" });
            try
            {
                StringAssert.StartsWith(SummonOps.CastKeyPrefix, castable.Key, "So it can never be Forge-upgraded as an ability.");
                Assert.IsEmpty(castable.Tags, "A summon triggers no combo.");
                Assert.AreEqual(MagicTargetType.AllAllies, castable.TargetType);
                Assert.AreEqual(1, castable.Effects.Count);
            }
            finally
            {
                Object.DestroyImmediate(castable);
            }
        }

        [Test]
        public void Describe_ReadsLikeTheGridTooltip()
        {
            Assert.AreEqual("+50% Strength for 3 turns · 1 charge per run", SummonOps.Describe(Boar(), null));
        }

        // --- the percentage buff ----------------------------------------------

        [Test]
        public void Summon_BuffsEveryHeroByHalfOfTheirOwnStrength()
        {
            var warrior = new MockCombatUnit("Warrior", strength: 10, endurance: 5, health: 30);
            var rogue = new MockCombatUnit("Rogue", strength: 8, endurance: 5, health: 30);
            var tracker = new CombatBuffTracker();

            Cast(Boar(), warrior, new List<ICombatUnit> { warrior, rogue }, tracker);

            Assert.AreEqual(5, tracker.GetBuffAmount(warrior, StatType.Strength));
            Assert.AreEqual(4, tracker.GetBuffAmount(rogue, StatType.Strength));
        }

        [Test]
        public void Summon_RoundsDown_WithAFloorOfOne()
        {
            var weak = new MockCombatUnit("Weak", strength: 1, endurance: 1, health: 10);
            var odd = new MockCombatUnit("Odd", strength: 7, endurance: 1, health: 10);
            var tracker = new CombatBuffTracker();

            Cast(Boar(), weak, new List<ICombatUnit> { weak, odd }, tracker);

            Assert.AreEqual(1, tracker.GetBuffAmount(weak, StatType.Strength), "50% of 1 floors to 1, not 0.");
            Assert.AreEqual(3, tracker.GetBuffAmount(odd, StatType.Strength), "50% of 7 is 3.5, rounded down.");
        }

        [Test]
        public void Summon_Again_RefreshesRatherThanDoubling()
        {
            var warrior = new MockCombatUnit("Warrior", strength: 10, endurance: 5, health: 30);
            var tracker = new CombatBuffTracker();
            var party = new List<ICombatUnit> { warrior };

            Cast(Boar(), warrior, party, tracker);
            tracker.TickBuffs(warrior);
            Cast(Boar(), warrior, party, tracker);

            Assert.AreEqual(5, tracker.GetBuffAmount(warrior, StatType.Strength), "Still +50%, not +100%.");
            tracker.TickBuffs(warrior);
            tracker.TickBuffs(warrior);
            Assert.AreEqual(5, tracker.GetBuffAmount(warrior, StatType.Strength), "The timer went back to 3.");
            tracker.TickBuffs(warrior);
            Assert.AreEqual(0, tracker.GetBuffAmount(warrior, StatType.Strength), "And then it expires.");
        }

        [Test]
        public void Summon_StacksWithAFlatStrengthBuff()
        {
            var warrior = new MockCombatUnit("Warrior", strength: 10, endurance: 5, health: 30);
            var tracker = new CombatBuffTracker();
            tracker.ApplyBuff(warrior, StatType.Strength, 3, 3);   // War Cry

            Cast(Boar(), warrior, new List<ICombatUnit> { warrior }, tracker);

            Assert.AreEqual(8, tracker.GetBuffAmount(warrior, StatType.Strength),
                "+3 flat and +5 (half of the Warrior's own 10, not of 13) together.");
        }

        private static void Cast(SummonSO summon, ICombatUnit caster, List<ICombatUnit> targets, CombatBuffTracker tracker)
        {
            var castable = SummonOps.BuildCastable(summon, new SummonGrant { Key = summon.Key });
            try
            {
                new EffectResolver().Execute(new SpellcastAction { Magic = castable, Caster = caster, Targets = targets }, tracker);
            }
            finally
            {
                Object.DestroyImmediate(castable);
            }
        }

        // --- charges ------------------------------------------------------------

        private static SummonState StateWith(int chargeBonus)
        {
            var state = new SummonState();
            var boar = Boar();
            state.SetHero("Warrior", new List<SummonGrant> { new SummonGrant { Key = "Boar", ChargeBonus = chargeBonus } },
                key => key == "Boar" ? boar : null);
            return state;
        }

        [Test]
        public void SummonState_StartsFull_AndSpendsOneChargePerSummon()
        {
            var state = StateWith(0);

            Assert.IsTrue(state.Knows("Warrior"));
            Assert.IsTrue(state.TryUse("Warrior", "Boar"));
            Assert.IsFalse(state.TryUse("Warrior", "Boar"), "One charge, then it is spent.");
            Assert.IsFalse(state.HasAnyUsable("Warrior"));
            Assert.IsTrue(state.Knows("Warrior"), "Spent is not forgotten - the command stays, greyed.");
        }

        [Test]
        public void SummonState_Refill_RestoresEveryCharge()
        {
            var state = StateWith(1);
            state.TryUse("Warrior", "Boar");
            state.TryUse("Warrior", "Boar");

            state.RefillCharges();

            Assert.AreEqual(2, state.GetSummons("Warrior")[0].Charges);
        }

        [Test]
        public void SummonState_SaveAndRestore_KeepsWhatWasLeft_ClampedToTheMax()
        {
            var state = StateWith(1);
            state.TryUse("Warrior", "Boar");
            var saved = state.GetSaveData();

            var resumed = StateWith(1);
            resumed.Restore(saved);
            Assert.AreEqual(1, resumed.GetSummons("Warrior")[0].Charges);

            var shrunk = StateWith(0);
            shrunk.Restore(new List<SummonChargeSaveData> { new SummonChargeSaveData { HeroKey = "Warrior", SummonKey = "Boar", Charges = 5 } });
            Assert.AreEqual(1, shrunk.GetSummons("Warrior")[0].Charges, "Clamped to the current max.");
        }

        [Test]
        public void Summon_OnTheSummonersOwnTurn_LastsThreeOfTheirTurns_LikeEveryoneElses()
        {
            var warrior = new MockCombatUnit("Warrior", strength: 10, endurance: 5, health: 30);
            var rogue = new MockCombatUnit("Rogue", strength: 8, endurance: 5, health: 30);
            var tracker = new CombatBuffTracker();

            tracker.BeginTurn(warrior);
            Cast(Boar(), warrior, new List<ICombatUnit> { warrior, rogue }, tracker);
            tracker.TickBuffs(warrior);   // the upkeep of the turn it was summoned on

            for (int turn = 1; turn <= 3; turn++)
            {
                Assert.AreEqual(5, tracker.GetBuffAmount(warrior, StatType.Strength), $"Warrior, turn {turn}");
                Assert.AreEqual(4, tracker.GetBuffAmount(rogue, StatType.Strength), $"Rogue, turn {turn}");
                tracker.BeginTurn(warrior);
                tracker.TickBuffs(warrior);
                tracker.BeginTurn(rogue);
                tracker.TickBuffs(rogue);
            }

            Assert.AreEqual(0, tracker.GetBuffAmount(warrior, StatType.Strength));
            Assert.AreEqual(0, tracker.GetBuffAmount(rogue, StatType.Strength));
        }

        [Test]
        public void MergeSaveData_BenchedHero_KeepsTheirSpentCharge()
        {
            // Floor 1: the Warrior summoned. Floor 2: benched, so this floor's save does not name him.
            var afterFloor1 = new List<SummonChargeSaveData>
            {
                new SummonChargeSaveData { HeroKey = "Warrior", SummonKey = "Boar", Charges = 0 }
            };
            var afterFloor2 = SummonState.MergeSaveData(afterFloor1, new List<SummonChargeSaveData>());

            var floor3 = StateWith(0);
            floor3.Restore(afterFloor2);

            Assert.AreEqual(0, floor3.GetSummons("Warrior")[0].Charges, "Sitting a floor out is not a refill.");
        }

        [Test]
        public void MergeSaveData_FieldedHero_ReplacesTheirOlderEntries()
        {
            var previous = new List<SummonChargeSaveData>
            {
                new SummonChargeSaveData { HeroKey = "Warrior", SummonKey = "Boar", Charges = 2 },
                new SummonChargeSaveData { HeroKey = "Paladin", SummonKey = "Other", Charges = 1 }
            };
            var current = new List<SummonChargeSaveData>
            {
                new SummonChargeSaveData { HeroKey = "Warrior", SummonKey = "Boar", Charges = 1 }
            };

            var merged = SummonState.MergeSaveData(previous, current);

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(1, merged.Single(e => e.HeroKey == "Warrior").Charges);
            Assert.AreEqual(1, merged.Single(e => e.HeroKey == "Paladin").Charges);
        }

        [Test]
        public void MergeSaveData_NullInputs_AreEmpty()
        {
            Assert.IsEmpty(SummonState.MergeSaveData(null, null));
        }

        [Test]
        public void SummonState_ReplacePartyKind_IsUsable_AndSpendsItsCharge()
        {
            var golem = Boar();
            golem.Key = "Golem";
            golem.Kind = SummonKind.ReplaceParty;
            var state = new SummonState();
            state.SetHero("Warrior", new List<SummonGrant> { new SummonGrant { Key = "Golem" } }, key => golem);

            Assert.IsTrue(state.HasAnyUsable("Warrior"));
            Assert.IsTrue(state.TryUse("Warrior", "Golem"));
            Assert.AreEqual(0, state.GetSummons("Warrior")[0].Charges);
        }

        // --- the party-replacing kind -----------------------------------------

        private static SummonSO Golem()
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "Golem";
            summon.DisplayName = "Cairn Golem";
            summon.Kind = SummonKind.ReplaceParty;
            summon.TurnsActive = 3;
            summon.StatPercents = new StatBlock(
                new UnitStat(StatType.MaxHealth, 250),
                new UnitStat(StatType.Endurance, 150),
                new UnitStat(StatType.Strength, 60),
                new UnitStat(StatType.Agility, 80));
            return summon;
        }

        private static int WarriorStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return 60;
                case StatType.Endurance: return 7;
                case StatType.Strength: return 21;
                case StatType.Agility: return 7;
                case StatType.Luck: return 6;
                default: return 0;
            }
        }

        [Test]
        public void StatsFor_ScalesEachStatByItsPercentage_RoundingDown()
        {
            var block = SummonOps.StatsFor(Golem(), null, WarriorStat);

            Assert.AreEqual(150, block[StatType.MaxHealth], "A ~60 HP Warrior brings a ~150 HP body.");
            Assert.AreEqual(10, block[StatType.Endurance], "7 x 150% = 10.5, rounded down.");
            Assert.AreEqual(12, block[StatType.Strength], "21 x 60% = 12.6, rounded down.");
            Assert.AreEqual(5, block[StatType.Agility], "7 x 80% = 5.6, rounded down.");
            Assert.AreEqual(0, block[StatType.Luck], "A stat with no percentage is 0, not copied.");
        }

        [Test]
        public void StatsFor_SummonPowerAddsHealthRatioPoints()
        {
            var block = SummonOps.StatsFor(Golem(), new SummonGrant { Key = "Golem", PowerBonus = 50 }, WarriorStat);

            Assert.AreEqual(180, block[StatType.MaxHealth], "250% + 50 points = 300% of 60.");
            Assert.AreEqual(12, block[StatType.Strength], "Power touches health only.");
        }

        [Test]
        public void StatsFor_NeverArrivesDead()
        {
            var block = SummonOps.StatsFor(Golem(), null, _ => 0);

            Assert.AreEqual(1, block[StatType.MaxHealth]);
        }

        [Test]
        public void TurnsFor_IsItsOwnPlusDurationUpgrades_AtLeastOne()
        {
            Assert.AreEqual(3, SummonOps.TurnsFor(Golem(), null));
            Assert.AreEqual(4, SummonOps.TurnsFor(Golem(), new SummonGrant { DurationBonus = 1 }));
            var brief = Golem();
            brief.TurnsActive = 0;
            Assert.AreEqual(1, SummonOps.TurnsFor(brief, null));
        }

        [Test]
        public void Describe_ReplacementReadsAsAPlaceTaken()
        {
            Assert.AreEqual("Takes the party's place · 250% of the summoner's health · 3 turns · 1 charge per run",
                SummonOps.Describe(Golem(), null));
        }

        [Test]
        public void AbilityKeys_AreTheActionsAndTheSignature()
        {
            var golem = Golem();
            var brace = ScriptableObject.CreateInstance<MagicSO>();
            brace.Key = "Brace";
            var quake = ScriptableObject.CreateInstance<MagicSO>();
            quake.Key = "Quake";
            golem.Actions = new List<MagicSO> { brace, null };
            golem.Signature = quake;

            CollectionAssert.AreEquivalent(new[] { "Brace", "Quake" },
                SummonOps.AbilityKeys(new[] { golem, Boar(), null }).ToList());
        }

        [Test]
        public void SummonStay_ImmediateTurnIsTurnOne_AndTheLastTurnSendsItHome()
        {
            var stay = new SummonStay(Golem(), 3);

            Assert.IsFalse(stay.EndTurn(), "The immediate turn: 1 of 3.");
            Assert.IsFalse(stay.EndTurn());
            Assert.IsTrue(stay.EndTurn(), "Its third turn is its last.");
            Assert.AreEqual(0, stay.TurnsLeft);
        }

        [Test]
        public void SummonStay_SignatureIsOncePerSummoning()
        {
            var golem = Golem();
            golem.Signature = ScriptableObject.CreateInstance<MagicSO>();
            var stay = new SummonStay(golem, 3);

            Assert.IsTrue(stay.CanUseSignature);
            stay.MarkSignatureUsed();
            Assert.IsFalse(stay.CanUseSignature);
            Assert.IsTrue(new SummonStay(golem, 3).CanUseSignature, "A new summoning brings it back.");
        }

        [Test]
        public void SummonStay_FirstReasonToLeaveWins()
        {
            var stay = new SummonStay(Golem(), 3);
            stay.Leave(SummonExit.Fell);
            stay.Leave(SummonExit.Dismissed);

            Assert.AreEqual(SummonExit.Fell, stay.Exit);
            Assert.IsFalse(stay.EndTurn(), "A summon that has left takes no more turns.");
        }

        [Test]
        public void SummonState_UnknownSummonKey_IsSkipped()
        {
            var state = new SummonState();
            state.SetHero("Warrior", new List<SummonGrant> { new SummonGrant { Key = "Missing" } }, key => null);

            Assert.IsFalse(state.Knows("Warrior"));
            Assert.IsEmpty(state.GetSaveData());
        }
    }
}
