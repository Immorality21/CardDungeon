using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// How the equipment screen words an item, and the swap arithmetic behind its before/after
    /// preview. The preview is only worth showing if it is the loadout the player would actually be
    /// wearing, so the swap rules are pinned here.
    /// </summary>
    public class ItemPresenterTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private ItemSO Item(string key, SlotType slot, params ItemBonus[] bonuses)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.Key = key;
            item.DisplayName = key;
            item.SlotType = slot;
            item.Bonuses = new List<ItemBonus>(bonuses);
            _created.Add(item);
            return item;
        }

        private static ItemBonus Raw(StatType stat, float value)
        {
            return new ItemBonus { StatType = stat, BonusType = BonusType.Raw, Value = value };
        }

        private static List<ItemSO> LoadEveryItem()
        {
            var items = new List<ItemSO>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ItemSO"))
            {
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        /// <summary>
        /// Every item shows its icon in the inventory, the merchant and the combat item picker. All
        /// ten materials once shared one placeholder log and every potion a spell icon; each item got
        /// its own PixelLab icon on 2026-09-28, and these two keep it that way.
        /// </summary>
        [Test]
        public void EveryItem_HasAnIcon()
        {
            var missing = new List<string>();
            foreach (var item in LoadEveryItem())
            {
                if (item.Icon == null)
                {
                    missing.Add(item.name);
                }
            }
            CollectionAssert.IsEmpty(missing, "Item(s) with no icon: " + string.Join(", ", missing));
        }

        [Test]
        public void NoTwoItems_ShareAnIcon()
        {
            var byIcon = new Dictionary<Sprite, string>();
            var shared = new List<string>();
            foreach (var item in LoadEveryItem())
            {
                if (item.Icon == null)
                {
                    continue;
                }
                if (byIcon.TryGetValue(item.Icon, out var other))
                {
                    shared.Add($"{item.name} and {other}");
                }
                else
                {
                    byIcon[item.Icon] = item.name;
                }
            }
            CollectionAssert.IsEmpty(shared, "Items sharing one icon (a placeholder crept back?): "
                + string.Join("; ", shared));
        }

        [Test]
        public void SlotLabel_ReadsLikeWords_NotLikeTheEnum()
        {
            Assert.AreEqual("Main Hand", ItemPresenter.SlotLabel(SlotType.MainHand));
            Assert.AreEqual("Off Hand", ItemPresenter.SlotLabel(SlotType.OffHand));
            Assert.AreEqual("Neck", ItemPresenter.SlotLabel(SlotType.Necklace));
        }

        [Test]
        public void SlotOrder_ListsEverySlotExactlyOnce()
        {
            var seen = new HashSet<SlotType>(ItemPresenter.SlotOrder);
            Assert.AreEqual(System.Enum.GetValues(typeof(SlotType)).Length, ItemPresenter.SlotOrder.Length);
            Assert.AreEqual(ItemPresenter.SlotOrder.Length, seen.Count);
        }

        [Test]
        public void BonusLines_SignsAndUnits()
        {
            var item = Item("Blade", SlotType.MainHand,
                Raw(StatType.Strength, 3),
                new ItemBonus { StatType = StatType.MaxHealth, BonusType = BonusType.Percentage, Value = 10 },
                Raw(StatType.Agility, -2));

            var lines = ItemPresenter.BonusLines(item);

            Assert.AreEqual(3, lines.Count);
            StringAssert.StartsWith("+3 ", lines[0]);
            StringAssert.StartsWith("+10% ", lines[1]);
            StringAssert.StartsWith("-2 ", lines[2]);
        }

        [Test]
        public void BonusChips_UseShortNames()
        {
            var item = Item("Blade", SlotType.MainHand, Raw(StatType.Strength, 3));

            var chips = ItemPresenter.BonusChips(item);

            CollectionAssert.AreEqual(new[] { "+3 " + StatCatalog.ShortName(StatType.Strength) }, chips);
        }

        [Test]
        public void BonusLines_SkipsHalfAuthoredRows()
        {
            var item = Item("Odd", SlotType.Head, Raw(StatType.None, 5), Raw(StatType.Strength, 0));

            CollectionAssert.IsEmpty(ItemPresenter.BonusLines(item));
        }

        [Test]
        public void ResistanceLines_NameTheElement()
        {
            var item = Item("Cloak", SlotType.Chest);
            item.Resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 25 },
                new Resistance { DamageType = DamageType.Normal, Percent = -10 },
            };

            var lines = ItemPresenter.ResistanceLines(item);

            CollectionAssert.AreEqual(new[] { "Fire +25%", "Physical -10%" }, lines);
        }

        [Test]
        public void SwapIn_ReplacesOnlyTheSameSlot()
        {
            var sword = Item("Sword", SlotType.MainHand);
            var shield = Item("Shield", SlotType.OffHand);
            var axe = Item("Axe", SlotType.MainHand);

            var result = ItemPresenter.SwapIn(new List<ItemSO> { sword, shield }, axe);

            CollectionAssert.AreEquivalent(new[] { shield, axe }, result);
        }

        [Test]
        public void SwapIn_DoesNotTouchTheInput()
        {
            var sword = Item("Sword", SlotType.MainHand);
            var equipped = new List<ItemSO> { sword };

            ItemPresenter.SwapIn(equipped, Item("Axe", SlotType.MainHand));

            CollectionAssert.AreEqual(new[] { sword }, equipped);
        }

        [Test]
        public void SwapOut_EmptiesJustThatSlot()
        {
            var sword = Item("Sword", SlotType.MainHand);
            var helm = Item("Helm", SlotType.Head);

            var result = ItemPresenter.SwapOut(new List<ItemSO> { sword, helm }, SlotType.MainHand);

            CollectionAssert.AreEqual(new[] { helm }, result);
        }

        [Test]
        public void SumResistances_AddsGearAndGridAndDropsZeroes()
        {
            var cloak = Item("Cloak", SlotType.Chest);
            cloak.Resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 20 },
                new Resistance { DamageType = DamageType.Ice, Percent = 10 },
            };
            var grid = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 5 },
                new Resistance { DamageType = DamageType.Ice, Percent = -10 },
            };

            var totals = ItemPresenter.SumResistances(new[] { cloak }, grid);

            Assert.AreEqual(25f, totals[DamageType.Fire]);
            Assert.IsFalse(totals.ContainsKey(DamageType.Ice), "a resistance that nets to zero is not worth a row");
        }

        [Test]
        public void ConsumableEffectLine_CurePotion_SaysWhatItCures()
        {
            var salve = Item("Salve", SlotType.MainHand);
            salve.Category = ItemCategory.Consumable;
            salve.ConsumableEffect = ConsumableEffectType.CureStatus;

            StringAssert.StartsWith("Cures", ItemPresenter.ConsumableEffectLine(salve));
        }

        [Test]
        public void ConsumableEffectLine_HealingWithAHero_UsesTheSameNumberCombatDoes()
        {
            var potion = Item("Potion", SlotType.MainHand);
            potion.Category = ItemCategory.Consumable;
            potion.ConsumableEffect = ConsumableEffectType.RestoreHealth;
            potion.ConsumableAmount = 5;
            potion.ConsumablePercent = 0.2f;

            string line = ItemPresenter.ConsumableEffectLine(potion, 40);

            StringAssert.Contains(potion.HealAmountFor(40).ToString(), line);
        }
    }
}
