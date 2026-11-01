using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>What a potion may do while the party walks (<see cref="PotionUse"/>).</summary>
    public class PotionUseTests
    {
        private ItemSO _item;

        [SetUp]
        public void SetUp()
        {
            _item = ScriptableObject.CreateInstance<ItemSO>();
            _item.Category = ItemCategory.Consumable;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_item);
        }

        [Test]
        public void WorksOutsideCombat_ACureDoesNot_EveryStatusEndsWithTheFight()
        {
            _item.ConsumableEffect = ConsumableEffectType.CureStatus;
            Assert.IsFalse(PotionUse.WorksOutsideCombat(_item));

            _item.ConsumableEffect = ConsumableEffectType.RestoreHealth;
            Assert.IsTrue(PotionUse.WorksOutsideCombat(_item));
        }

        [Test]
        public void WorksOutsideCombat_AnythingElseInTheBag_DoesNot()
        {
            _item.Category = ItemCategory.Equipment;
            Assert.IsFalse(PotionUse.WorksOutsideCombat(_item));
            Assert.IsFalse(PotionUse.WorksOutsideCombat(null));
        }

        [Test]
        public void Helps_AHealingPotion_NeedsAWoundedLivingHero()
        {
            _item.ConsumableEffect = ConsumableEffectType.RestoreHealth;

            Assert.IsTrue(PotionUse.Helps(_item, true, 5, 10, false));
            Assert.IsFalse(PotionUse.Helps(_item, true, 10, 10, true), "A full bar would waste it.");
            Assert.IsFalse(PotionUse.Helps(_item, false, 0, 10, false), "A potion is not a revive.");
        }

        [Test]
        public void Helps_AnEnergyPotion_NeedsASpentCharge()
        {
            _item.ConsumableEffect = ConsumableEffectType.RestoreCharge;

            Assert.IsTrue(PotionUse.Helps(_item, true, 10, 10, true));
            Assert.IsFalse(PotionUse.Helps(_item, true, 5, 10, false));
        }

        [Test]
        public void ReserveFor_KeepsAShareForEachFloorAhead()
        {
            Assert.AreEqual(4, PotionUse.ReserveFor(5, 5), "One of five potions for this floor.");
            Assert.AreEqual(2, PotionUse.ReserveFor(5, 2), "Rounds in favour of the floor being played.");
            Assert.AreEqual(0, PotionUse.ReserveFor(5, 1), "Nothing to save on the last floor.");
            Assert.AreEqual(0, PotionUse.ReserveFor(0, 3));
        }

        [Test]
        public void MayDrink_TheReserveOpensOnlyToSaveAHero()
        {
            Assert.IsTrue(PotionUse.MayDrink(5, 4, 0.4f), "The floor's share is free.");
            Assert.IsFalse(PotionUse.MayDrink(4, 4, 0.4f), "The reserve is not.");
            Assert.IsTrue(PotionUse.MayDrink(4, 4, PotionUse.EmergencyHealth), "Unless a hero is about to fall.");
            Assert.IsFalse(PotionUse.MayDrink(0, 0, 0.1f), "An empty belt saves nobody.");
        }

        [Test]
        public void Helps_AnElixir_EitherHalfIsEnough()
        {
            _item.ConsumableEffect = ConsumableEffectType.HealAndCharge;

            Assert.IsTrue(PotionUse.Helps(_item, true, 5, 10, false));
            Assert.IsTrue(PotionUse.Helps(_item, true, 10, 10, true));
            Assert.IsFalse(PotionUse.Helps(_item, true, 10, 10, false));
        }
    }
}
