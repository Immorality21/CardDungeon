using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The gear spend: what a gold budget buys, deterministically.
    ///
    /// <para>This exists so gear can be an investment axis at all. The only route gear had into the
    /// balance model was <c>ReferencePartyUsesSavedGear</c>, which reads the local save file — so it
    /// was machine-specific, so the regression suite could not use it, so it defaulted to off, so
    /// every published number described a party wearing nothing. A greedy spend off the item catalog
    /// is reproducible, which is the whole point. (<c>docs/BALANCING.md</c> §5p.)</para>
    /// </summary>
    public class GearLoadoutTests
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

        private static float Weight(StatType stat)
        {
            return StatCatalog.Of(stat).PowerWeight;
        }

        private HeroSO Hero(string key, int strength = 10, int health = 40)
        {
            var hero = Make<HeroSO>();
            hero.Key = key;
            hero.Label = key;
            hero.BaseStats[StatType.Strength] = strength;
            hero.BaseStats[StatType.Endurance] = 3;
            hero.BaseStats[StatType.MaxHealth] = health;
            hero.BaseStats[StatType.Agility] = 5;
            return hero;
        }

        private ItemSO Item(string key, SlotType slot, ItemRarity rarity, int level, StatType stat, int amount)
        {
            var item = Make<ItemSO>();
            item.Key = key;
            item.DisplayName = key;
            item.Category = ItemCategory.Equipment;
            item.SlotType = slot;
            item.Rarity = rarity;
            item.ItemLevel = level;
            item.Bonuses = new List<ItemBonus>
            {
                new ItemBonus { StatType = stat, BonusType = BonusType.Raw, Value = amount }
            };
            return item;
        }

        private ItemSO Warded(
            string key, SlotType slot, ItemRarity rarity, int level,
            StatType stat, int amount, Assets.Scripts.Combat.DamageType element, float percent)
        {
            var item = Item(key, slot, rarity, level, stat, amount);
            item.Resistances = new List<Assets.Scripts.Combat.Resistance>
            {
                new Assets.Scripts.Combat.Resistance { DamageType = element, Percent = percent }
            };
            return item;
        }

        /// <summary>A mix that is entirely one element, so a ward against it is worth its full value.</summary>
        private static IncomingDamageMix AllOf(Assets.Scripts.Combat.DamageType element)
        {
            var mix = new IncomingDamageMix();
            mix.Add(element, 100f);
            return mix;
        }

        private ItemSO Potion()
        {
            var item = Make<ItemSO>();
            item.Key = "Potion";
            item.DisplayName = "Potion";
            item.Category = ItemCategory.Consumable;
            item.ConsumableAmount = 5;
            return item;
        }

        private System.Func<HeroSO, Stats> Flat(IList<HeroSO> heroes)
        {
            return hero => new Stats(hero.BaseStats.Clone());
        }

        // --- what a budget buys ---------------------------------------------------------

        [Test]
        public void Spend_ZeroBudget_BuysNothing()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var catalog = new List<ItemSO> { Item("Sword", SlotType.MainHand, ItemRarity.Common, 1, StatType.Strength, 4) };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, 0, Weight);

            Assert.AreEqual(0, spend.GoldSpent);
            Assert.AreEqual(0, spend.Purchases.Count);
            Assert.AreEqual(0, spend.For(heroes[0]).Count);
        }

        /// <summary>
        /// A budget under the cheapest price buys nothing rather than going into debt — the greedy
        /// loop has to check affordability before value, not after.
        /// </summary>
        [Test]
        public void Spend_BudgetBelowTheCheapestItem_BuysNothing()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var sword = Item("Sword", SlotType.MainHand, ItemRarity.Common, 1, StatType.Strength, 4);
            var catalog = new List<ItemSO> { sword };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, ShopPricing.BuyPrice(sword) - 1, Weight);

            Assert.AreEqual(0, spend.GoldSpent);
            Assert.AreEqual(0, spend.Purchases.Count);
        }

        [Test]
        public void Spend_NeverExceedsTheBudget()
        {
            var heroes = new List<HeroSO> { Hero("A"), Hero("B"), Hero("C") };
            var catalog = new List<ItemSO>
            {
                Item("Sword", SlotType.MainHand, ItemRarity.Common, 1, StatType.Strength, 4),
                Item("Plate", SlotType.Chest, ItemRarity.Uncommon, 3, StatType.MaxHealth, 12),
                Item("Boots", SlotType.Legs, ItemRarity.Uncommon, 3, StatType.Agility, 2)
            };

            for (int budget = 0; budget <= 400; budget += 17)
            {
                var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, budget, Weight);
                Assert.LessOrEqual(spend.GoldSpent, budget, $"Overspent at a budget of {budget}.");
            }
        }

        // --- one item per slot ----------------------------------------------------------

        /// <summary>
        /// A hero wears one thing per slot. Without this the spend would happily buy every sword in
        /// the catalog and stack their bonuses, which is not a loadout the game can produce.
        /// </summary>
        [Test]
        public void Spend_OneItemPerSlotPerHero()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var catalog = new List<ItemSO>
            {
                Item("Dagger", SlotType.MainHand, ItemRarity.Common, 1, StatType.Strength, 2),
                Item("Sword", SlotType.MainHand, ItemRarity.Common, 2, StatType.Strength, 4),
                Item("Axe", SlotType.MainHand, ItemRarity.Uncommon, 1, StatType.Strength, 6)
            };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, 10000, Weight);

            Assert.AreEqual(1, spend.For(heroes[0]).Count, "Three main-hand weapons, one hand.");
        }

        /// <summary>
        /// Given enough gold the spend converges on the best item in each slot, replacing anything it
        /// bought on the way. A greedy loop that could not upgrade would stall on the first cheap
        /// pickup and report a much weaker party than the gold can actually field.
        /// </summary>
        [Test]
        public void Spend_UpgradesASlotItAlreadyFilled()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var cheap = Item("Dagger", SlotType.MainHand, ItemRarity.Common, 1, StatType.Strength, 2);
            var best = Item("Axe", SlotType.MainHand, ItemRarity.Uncommon, 1, StatType.Strength, 12);
            var catalog = new List<ItemSO> { cheap, best };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, 10000, Weight);

            CollectionAssert.Contains(spend.For(heroes[0]), best);
            CollectionAssert.DoesNotContain(spend.For(heroes[0]), cheap);
        }

        // --- what it refuses to buy -----------------------------------------------------

        [Test]
        public void Spend_IgnoresConsumables()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var catalog = new List<ItemSO> { Potion() };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, 10000, Weight);

            Assert.AreEqual(0, spend.GoldSpent, "A potion belt is not a gear loadout.");
        }

        /// <summary>
        /// An item that adds nothing is not an investment however cheap it is. The project ships one
        /// (Simple Sword, 20g, no bonuses), so this is the real catalog's behaviour, not a hypothetical.
        /// </summary>
        [Test]
        public void Spend_SkipsItemsThatAddNoPower()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var blank = Make<ItemSO>();
            blank.Key = "Stick";
            blank.DisplayName = "Stick";
            blank.Category = ItemCategory.Equipment;
            blank.SlotType = SlotType.MainHand;
            blank.Rarity = ItemRarity.Common;
            blank.ItemLevel = 1;
            blank.Bonuses = new List<ItemBonus>();

            var spend = GearLoadout.Spend(heroes, Flat(heroes), new List<ItemSO> { blank }, 10000, Weight);

            Assert.AreEqual(0, spend.GoldSpent);
        }

        // --- determinism ----------------------------------------------------------------

        /// <summary>
        /// The whole reason this replaces the save-file route: the same inputs must give the same
        /// loadout every time, or the regression suite cannot assert on a number derived from it.
        /// Catalog order is shuffled here because authoring order is exactly the kind of thing that
        /// changes without anyone meaning to.
        /// </summary>
        [Test]
        public void Spend_IsDeterministicAndIndependentOfCatalogOrder()
        {
            var heroes = new List<HeroSO> { Hero("A"), Hero("B") };
            var sword = Item("Sword", SlotType.MainHand, ItemRarity.Common, 2, StatType.Strength, 4);
            var plate = Item("Plate", SlotType.Chest, ItemRarity.Uncommon, 3, StatType.MaxHealth, 12);
            var boots = Item("Boots", SlotType.Legs, ItemRarity.Uncommon, 3, StatType.Agility, 2);
            var cap = Item("Cap", SlotType.Head, ItemRarity.Common, 1, StatType.MaxHealth, 6);

            var forward = GearLoadout.Spend(
                heroes, Flat(heroes), new List<ItemSO> { sword, plate, boots, cap }, 200, Weight);
            var reversed = GearLoadout.Spend(
                heroes, Flat(heroes), new List<ItemSO> { cap, boots, plate, sword }, 200, Weight);

            Assert.AreEqual(forward.GoldSpent, reversed.GoldSpent);
            CollectionAssert.AreEqual(forward.For(heroes[0]), reversed.For(heroes[0]));
            CollectionAssert.AreEqual(forward.For(heroes[1]), reversed.For(heroes[1]));
        }

        /// <summary>More gold is never worth less power — the axis has to be monotonic to be swept.</summary>
        [Test]
        public void Spend_MoreGoldIsNeverWorseValue()
        {
            var heroes = new List<HeroSO> { Hero("A"), Hero("B") };
            var catalog = new List<ItemSO>
            {
                Item("Sword", SlotType.MainHand, ItemRarity.Common, 2, StatType.Strength, 4),
                Item("Plate", SlotType.Chest, ItemRarity.Uncommon, 3, StatType.MaxHealth, 12),
                Item("Amulet", SlotType.Necklace, ItemRarity.Rare, 4, StatType.Strength, 6)
            };

            float previous = -1f;
            for (int budget = 0; budget <= 800; budget += 40)
            {
                var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, budget, Weight);

                float power = 0f;
                foreach (var hero in heroes)
                {
                    var stats = HeroStatCalculator.WithGear(new Stats(hero.BaseStats.Clone()), spend.For(hero));
                    foreach (var stat in StatCatalog.Types)
                    {
                        power += stats[stat] * Weight(stat);
                    }
                }

                Assert.GreaterOrEqual(power, previous, $"Power went down between budgets at {budget}.");
                previous = power;
            }
        }

        /// <summary>
        /// The axis saturates: past a full loadout for everyone there is nothing left to buy, so a
        /// sweep step above that is a dearer mix for no gain. <c>MeasureMix</c> charges what was
        /// actually spent for exactly this reason.
        /// </summary>
        [Test]
        public void Spend_SaturatesAtAFullLoadoutForEveryone()
        {
            var heroes = new List<HeroSO> { Hero("A"), Hero("B") };
            var catalog = new List<ItemSO>
            {
                Item("Sword", SlotType.MainHand, ItemRarity.Common, 2, StatType.Strength, 4),
                Item("Plate", SlotType.Chest, ItemRarity.Uncommon, 3, StatType.MaxHealth, 12)
            };

            int perHero = GearLoadout.FullLoadoutPrice(catalog);
            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, 100000, Weight);

            Assert.AreEqual(perHero * heroes.Count, spend.GoldSpent);
            Assert.AreEqual(2, spend.For(heroes[0]).Count);
            Assert.AreEqual(2, spend.For(heroes[1]).Count);
        }

        // --- the lookup PartyBaseline consumes ------------------------------------------

        [Test]
        public void Lookup_FeedsPartyBaselineTheBoughtGear()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var sword = Item("Sword", SlotType.MainHand, ItemRarity.Common, 2, StatType.Strength, 8);
            var spend = GearLoadout.Spend(heroes, Flat(heroes), new List<ItemSO> { sword }, 1000, Weight);

            var bare = PartyBaseline.Build(heroes, 0);
            var geared = PartyBaseline.Build(heroes, 0, spend.Lookup);

            Assert.AreEqual(0, bare.Heroes[0].Gear.Count);
            CollectionAssert.Contains(geared.Heroes[0].Gear, sword);
            Assert.Greater(geared.Heroes[0].Effective[StatType.Strength],
                bare.Heroes[0].Effective[StatType.Strength],
                "Gear the model bought has to reach the stats it measures.");
        }

        // --- resistance is part of the ranking ------------------------------------------

        /// <summary>
        /// The bug §5q fixes. Both items cost the same and carry the same stat line; one also wards
        /// the element the floor actually deals. Ranking on the stat line alone made them a tie
        /// broken by name, so the Ruby Amulet's 25% Fire was bought for its Strength and its
        /// resistance counted for nothing — while <c>PartyBaseline</c> handed that same resistance to
        /// the simulator, where it changed the fight.
        /// </summary>
        [Test]
        public void Spend_PrefersTheWardedItemWhenTheFloorDealsThatElement()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var plain = Item("APlain", SlotType.Chest, ItemRarity.Common, 1, StatType.MaxHealth, 6);
            var warded = Warded("BWarded", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 6, Assets.Scripts.Combat.DamageType.Fire, 25f);
            var catalog = new List<ItemSO> { plain, warded };

            // Name order puts the plain one first, so a tie would pick it.
            var spend = GearLoadout.Spend(
                heroes, Flat(heroes), catalog, ShopPricing.BuyPrice(plain), Weight, AllOf(
                    Assets.Scripts.Combat.DamageType.Fire));

            Assert.AreEqual(1, spend.Purchases.Count);
            Assert.AreSame(warded, spend.Purchases[0].Item,
                "A ward against the element the floor deals has to beat an identical item without one.");
        }

        /// <summary>
        /// And it is <i>conditional</i>: the same ward against an element nothing deals is worth
        /// nothing, so the tie falls back to the stat line. This is why the mix is a parameter rather
        /// than a constant — a Fire ward should be a purchase on Emberfall and a waste on the Mire.
        /// </summary>
        [Test]
        public void Spend_IgnoresAWardAgainstAnElementTheFloorNeverDeals()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var plain = Item("APlain", SlotType.Chest, ItemRarity.Common, 1, StatType.MaxHealth, 6);
            var warded = Warded("BWarded", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 6, Assets.Scripts.Combat.DamageType.Fire, 25f);
            var catalog = new List<ItemSO> { plain, warded };

            var spend = GearLoadout.Spend(
                heroes, Flat(heroes), catalog, ShopPricing.BuyPrice(plain), Weight, AllOf(
                    Assets.Scripts.Combat.DamageType.Ice));

            Assert.AreEqual(1, spend.Purchases.Count);
            Assert.AreSame(plain, spend.Purchases[0].Item,
                "With no fire incoming the two items are identical, so the stable tie-break decides.");
        }

        /// <summary>
        /// No mix means no claim about what the party will face, so resistance prices at nothing
        /// rather than at a guess. Every caller that does not sweep a floor reads unchanged.
        /// </summary>
        [Test]
        public void Spend_WithNoDamageMix_RanksExactlyAsItDidBefore()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var plain = Item("APlain", SlotType.Chest, ItemRarity.Common, 1, StatType.MaxHealth, 6);
            var warded = Warded("BWarded", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 6, Assets.Scripts.Combat.DamageType.Fire, 25f);
            var catalog = new List<ItemSO> { plain, warded };

            var spend = GearLoadout.Spend(heroes, Flat(heroes), catalog, ShopPricing.BuyPrice(plain), Weight);

            Assert.AreEqual(1, spend.Purchases.Count);
            Assert.AreSame(plain, spend.Purchases[0].Item);
        }

        /// <summary>
        /// Resistance is priced as the health it effectively buys, so it compounds with the bar it
        /// protects — the same ward is worth more to a hero with more health. That is what makes the
        /// gold axis and the XP axis pull together instead of adding independently.
        /// </summary>
        [Test]
        public void ResistanceValue_ScalesWithTheHealthPoolItProtects()
        {
            var warded = Warded("Ward", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 0, Assets.Scripts.Combat.DamageType.Fire, 50f);
            var gear = new List<ItemSO> { warded };
            var none = new List<Assets.Scripts.Combat.Resistance>();
            var mix = AllOf(Assets.Scripts.Combat.DamageType.Fire);

            float small = GearLoadout.ResistanceValue(20, gear, none, mix, Weight);
            float large = GearLoadout.ResistanceValue(60, gear, none, mix, Weight);

            Assert.Greater(small, 0f);
            Assert.AreEqual(3f, large / small, 0.01f, "Three times the bar, three times the ward.");
        }

        /// <summary>
        /// The value is the <i>marginal</i> difference the gear makes over what the hero already
        /// has, which is the only way to rank an item for a specific hero. And because effective
        /// health is <c>1/(1-r)</c>, that margin <b>rises</b> as resistance stacks — 40% to 80% halves
        /// incoming damage again, exactly as 0% to 50% did. That is not a quirk of the scoring: it is
        /// what <c>DamageCalculator</c> does, and the ranking has to agree with the fight.
        /// </summary>
        [Test]
        public void ResistanceValue_IsWorthMoreToAHeroWhoAlreadyResistsThatElement()
        {
            var warded = Warded("Ward", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 0, Assets.Scripts.Combat.DamageType.Fire, 40f);
            var gear = new List<ItemSO> { warded };
            var mix = AllOf(Assets.Scripts.Combat.DamageType.Fire);

            float bare = GearLoadout.ResistanceValue(
                40, gear, new List<Assets.Scripts.Combat.Resistance>(), mix, Weight);
            float onTop = GearLoadout.ResistanceValue(
                40, gear,
                new List<Assets.Scripts.Combat.Resistance>
                {
                    new Assets.Scripts.Combat.Resistance
                    {
                        DamageType = Assets.Scripts.Combat.DamageType.Fire, Percent = 40f
                    }
                },
                mix, Weight);

            Assert.Greater(bare, 0f);
            Assert.Greater(onTop, bare);
        }

        /// <summary>
        /// Which is exactly why there is a cap. Effective health goes to infinity as resistance
        /// approaches immunity, and an unbounded score would let one 100% ward outbid the entire
        /// rest of the catalog on a floor that deals one element.
        /// </summary>
        [Test]
        public void ResistanceValue_IsCappedSoNearImmunityIsNotWorthInfinity()
        {
            var immune = Warded("Ward", SlotType.Chest, ItemRarity.Common, 1,
                StatType.MaxHealth, 0, Assets.Scripts.Combat.DamageType.Fire, 100f);
            var mix = AllOf(Assets.Scripts.Combat.DamageType.Fire);

            float value = GearLoadout.ResistanceValue(
                40, new List<ItemSO> { immune }, new List<Assets.Scripts.Combat.Resistance>(), mix, Weight);

            float ceiling = 40 * (GearLoadout.MaxResistanceEffectiveHealthMultiplier - 1f)
                * Weight(StatType.MaxHealth);
            Assert.AreEqual(ceiling, value, 0.01f);
        }

        [Test]
        public void Lookup_UnknownHeroGetsAnEmptyLoadoutRatherThanThrowing()
        {
            var heroes = new List<HeroSO> { Hero("A") };
            var spend = GearLoadout.Spend(heroes, Flat(heroes), new List<ItemSO>(), 100, Weight);

            Assert.AreEqual(0, spend.For(Hero("Stranger")).Count);
            Assert.AreEqual(0, spend.For(null).Count);
        }
    }
}
