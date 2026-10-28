using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The typed potion belt: slots, ladders, the campfire cycle and run-start stock (playtest 3 finding 5).</summary>
    public class PotionBeltTests
    {
        private static ItemSO Potion(string key)
        {
            var potion = ScriptableObject.CreateInstance<ItemSO>();
            potion.Key = key;
            potion.Category = ItemCategory.Consumable;
            return potion;
        }

        private static PotionBeltSO Belt()
        {
            var belt = ScriptableObject.CreateInstance<PotionBeltSO>();
            belt.StartingSlots = 2;
            belt.SlotTiers.Add(new BeltSlotTier { Slots = 3 });
            belt.SlotTiers.Add(new BeltSlotTier { Slots = 4 });

            var healing = new BeltPotionType { Key = "Healing", Potion = Potion("HealingPotion"), StartsUnlocked = true, BaseCapacity = 2 };
            healing.CapacityTiers.Add(new BeltCapacityTier { Capacity = 3 });
            healing.CapacityTiers.Add(new BeltCapacityTier { Capacity = 4 });
            healing.PotencyTiers.Add(new BeltPotencyTier { HealPercent = 0.1f });
            healing.PotencyTiers.Add(new BeltPotencyTier { HealPercent = 0.2f });

            var energy = new BeltPotionType { Key = "Energy", Potion = Potion("EnergyPotion") };
            energy.PotencyTiers.Add(new BeltPotencyTier { Charges = 1 });

            belt.Types.Add(healing);
            belt.Types.Add(new BeltPotionType { Key = "Antidote", Potion = Potion("AntidoteSalve") });
            belt.Types.Add(energy);
            return belt;
        }

        private static bool AllKnown(string key)
        {
            return true;
        }

        [Test]
        public void MaxSlots_IsTheLargestStep()
        {
            Assert.AreEqual(4, Belt().MaxSlots);
        }

        [Test]
        public void NextSlot_OffersTheFirstStepAboveTheBelt_ThenStops()
        {
            Assert.AreEqual(3, PotionBeltOps.NextSlot(Belt(), 2).Slots);
            Assert.IsNull(PotionBeltOps.NextSlot(Belt(), 4));
        }

        [Test]
        public void NextCapacity_IsPerType()
        {
            var belt = Belt();
            Assert.AreEqual(3, PotionBeltOps.NextCapacity(belt.Find("Healing"), 2).Capacity);
            Assert.IsNull(PotionBeltOps.NextCapacity(belt.Find("Healing"), 4));
            Assert.IsNull(PotionBeltOps.NextCapacity(belt.Find("Antidote"), 1), "a type with no ladder sells none");
        }

        [Test]
        public void HealBonus_IsTheLastStepBought_NotASum()
        {
            var healing = Belt().Find("Healing");
            Assert.AreEqual(0f, PotionBeltOps.HealBonus(healing, 0));
            Assert.AreEqual(0.2f, PotionBeltOps.HealBonus(healing, 2), 1e-5f);
            Assert.AreEqual(0.2f, PotionBeltOps.HealBonus(healing, 9), 1e-5f);
            Assert.IsNull(PotionBeltOps.NextPotency(healing, 2));
        }

        [Test]
        public void ChargeBonus_ReadsTheChargesOfTheStepReached()
        {
            var energy = Belt().Find("Energy");
            Assert.AreEqual(0, PotionBeltOps.ChargeBonus(energy, 0));
            Assert.AreEqual(1, PotionBeltOps.ChargeBonus(energy, 1));
        }

        [Test]
        public void Loadout_ASaveThatNeverChose_StartsWithHealingInSlotOne()
        {
            CollectionAssert.AreEqual(new[] { "Healing", "" }, PotionBeltOps.Loadout(Belt(), null, 2, AllKnown));
        }

        [Test]
        public void Loadout_EmptiesUnknownLockedAndRepeatedTypes_AndFitsTheSlots()
        {
            var saved = new List<string> { "Healing", "Healing", "Gone", "Energy" };
            var loadout = PotionBeltOps.Loadout(Belt(), saved, 3, key => key != "Energy");

            CollectionAssert.AreEqual(new[] { "Healing", "", "" }, loadout);
        }

        [Test]
        public void Cycle_WalksTheUnlockedTypes_SkippingOnesInOtherSlots_ThenEmpty()
        {
            var belt = Belt();
            var loadout = new List<string> { "Healing", "" };

            Assert.AreEqual("Antidote", PotionBeltOps.Cycle(belt, loadout, 1, AllKnown));
            loadout[1] = "Antidote";
            Assert.AreEqual("Energy", PotionBeltOps.Cycle(belt, loadout, 1, AllKnown));
            loadout[1] = "Energy";
            Assert.AreEqual("", PotionBeltOps.Cycle(belt, loadout, 1, AllKnown), "Healing is in slot 1");
        }

        [Test]
        public void Cycle_SkipsLockedTypes()
        {
            var loadout = new List<string> { "Healing", "" };
            Assert.AreEqual("Energy", PotionBeltOps.Cycle(Belt(), loadout, 1, key => key != "Antidote"));
        }

        [Test]
        public void AssumedSteps_BuysEveryStepWhoseMaterialsCanBeHad_InOrder()
        {
            var iron = Potion("ScrapIron");
            var wax = Potion("SealingWax");
            var healing = Belt().Find("Healing");
            healing.CapacityTiers[0].Materials.Add(new MaterialCost { Material = iron, Amount = 3 });
            healing.CapacityTiers[1].Materials.Add(new MaterialCost { Material = wax, Amount = 2 });
            healing.PotencyTiers[0].Materials.Add(new MaterialCost { Material = wax, Amount = 1 });

            PotionBeltOps.AssumedSteps(healing, item => item == iron, out int capacity, out int potency);
            Assert.AreEqual(3, capacity, "the wax step is out of reach, so the ladder stops at 3");
            Assert.AreEqual(0, potency, "the first potency step needs wax, so none after it either");

            PotionBeltOps.AssumedSteps(healing, item => true, out capacity, out potency);
            Assert.AreEqual(4, capacity);
            Assert.AreEqual(2, potency);
        }

        [Test]
        public void RunStartStock_FillsSlottedTypes_AndEmptiesTheRest()
        {
            var stock = PotionBeltOps.RunStartStock(Belt(), new List<string> { "Healing", "Energy" },
                type => type.Key == "Healing" ? 3 : 1);

            var byKey = stock.ToDictionary(p => p.Key.Key, p => p.Value);
            Assert.AreEqual(3, byKey["HealingPotion"]);
            Assert.AreEqual(1, byKey["EnergyPotion"]);
            Assert.AreEqual(0, byKey["AntidoteSalve"]);
        }

        [Test]
        public void HealAmountFor_AddsTheBonusToThePercentage()
        {
            var potion = Potion("HealingPotion");
            potion.ConsumableAmount = 2;
            // Exact binary fractions: 0.3f * 100 is 30.000002 and rounds up to 31.
            potion.ConsumablePercent = 0.25f;

            Assert.AreEqual(27, potion.HealAmountFor(100));
            Assert.AreEqual(52, potion.HealAmountFor(100, 0.25f));
        }

        [Test]
        public void HealAmountFor_AnElixirHeals_AnEnergyPotionDoesNot()
        {
            var elixir = Potion("Elixir");
            elixir.ConsumableEffect = ConsumableEffectType.HealAndCharge;
            elixir.ConsumablePercent = 0.25f;
            var energy = Potion("EnergyPotion");
            energy.ConsumableEffect = ConsumableEffectType.RestoreCharge;
            energy.ConsumablePercent = 0.25f;

            Assert.AreEqual(25, elixir.HealAmountFor(100));
            Assert.AreEqual(0, energy.HealAmountFor(100));
        }

        [Test]
        public void RevisitAllows_RevisitOnlyItems_NeedARevisitAtTheirFear()
        {
            Assert.IsTrue(RunFear.RevisitAllows(-1, false, 0), "an ordinary item drops anywhere");
            Assert.IsFalse(RunFear.RevisitAllows(1, false, 0), "never on a first clear");
            Assert.IsFalse(RunFear.RevisitAllows(1, true, 0), "not below its fear");
            Assert.IsTrue(RunFear.RevisitAllows(1, true, 1));
            Assert.IsTrue(RunFear.RevisitAllows(1, true, 4));
        }

        [Test]
        public void Withholds_ARevisitOnlyMaterial_OnAFirstClear()
        {
            var wax = ScriptableObject.CreateInstance<ItemSO>();
            wax.Key = "SealingWax";
            wax.RevisitFear = 1;

            Assert.IsTrue(RunFear.None.Withholds(wax));
        }
    }
}
