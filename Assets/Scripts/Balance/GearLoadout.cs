using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One item bought for one hero, and what it was worth.</summary>
    public class GearPurchase
    {
        public HeroSO Hero;
        public ItemSO Item;

        /// <summary>The item this one displaced in that slot, if any. Nothing is refunded for it.</summary>
        public ItemSO Replaced;

        public int Price;

        /// <summary>Power score the purchase added, at the moment it was made.</summary>
        public float PowerGain;
    }

    /// <summary>What a gold budget bought, and who is wearing it.</summary>
    public class GearSpend
    {
        public int GoldBudget;
        public int GoldSpent;

        public readonly List<GearPurchase> Purchases = new List<GearPurchase>();

        private readonly Dictionary<HeroSO, List<ItemSO>> _byHero = new Dictionary<HeroSO, List<ItemSO>>();

        public int GoldLeft => Mathf.Max(0, GoldBudget - GoldSpent);

        public List<ItemSO> For(HeroSO hero)
        {
            if (hero != null && _byHero.TryGetValue(hero, out var gear))
            {
                return gear;
            }
            return new List<ItemSO>();
        }

        /// <summary>
        /// The form <see cref="PartyBaseline.Build"/> wants. Handing this over instead of the
        /// dictionary keeps the baseline ignorant of where a loadout came from — a save audit's
        /// real equipment and a designed budget's greedy spend arrive through the same door.
        /// </summary>
        public Func<HeroSO, List<ItemSO>> Lookup => For;

        internal void Record(HeroSO hero, List<ItemSO> gear)
        {
            if (hero != null)
            {
                _byHero[hero] = gear;
            }
        }
    }

    /// <summary>
    /// Spends a gold budget on equipment, deterministically — the gear counterpart of
    /// <see cref="SphereGridOps.GreedySpend"/>, and for the same reason: the model needs *a* build
    /// at a given investment, reproducibly, without asking a player what they actually bought.
    ///
    /// <para><b>Why this exists.</b> Until now the only way gear reached the balance model was
    /// <c>BalanceRulesSO.ReferencePartyUsesSavedGear</c>, which reads the local save file. That is
    /// machine-specific, so it is excluded from <c>BalanceRegressionTests</c>, so it defaulted to
    /// off, so every published number described a party in <b>no gear at all</b> — and the
    /// investment frontier could not price gear as a way to pay for depth, even though the merchant
    /// sells it and the player buys it with the same gold that buys a party slot. See
    /// <c>docs/BALANCING.md</c> §5p.</para>
    ///
    /// <para><b>Gear is a between-run axis.</b> Equipping happens only in <c>InventoryHubUI</c>, so
    /// a party's loadout is fixed for the whole run — unlike XP, which the run curve banks and
    /// re-spends per floor. That is what makes one loadout per mix the right model rather than a
    /// per-floor loop, and it is why loot picked up mid-run contributes gold and a *next* run's
    /// options, never power in the run that found it.</para>
    ///
    /// <para><b>Read the numbers as optimistic</b>, exactly as with the grid spend: this buys the
    /// best power-per-gold available at every step, which no player does perfectly.</para>
    /// </summary>
    public static class GearLoadout
    {
        /// <summary>
        /// Greedy best-value-first: repeatedly buy the affordable item that adds the most power
        /// score per gold, until nothing affordable is an improvement.
        ///
        /// <para><paramref name="baseStatsFor"/> supplies each hero's stats *before* gear — node
        /// gains included, since a percentage bonus is worth more on a bigger number. Callers that
        /// only have an XP budget should use the overload below.</para>
        ///
        /// <para>Ties are broken on gain, then price, then name, so the same inputs always produce
        /// the same loadout. A regression suite cannot assert on a spend that wanders.</para>
        /// </summary>
        public static GearSpend Spend(
            IList<HeroSO> party,
            Func<HeroSO, Stats> baseStatsFor,
            IList<ItemSO> catalog,
            int goldBudget,
            Func<StatType, float> weightFor)
        {
            var spend = new GearSpend { GoldBudget = Mathf.Max(0, goldBudget) };

            var heroes = new List<HeroSO>();
            if (party != null)
            {
                foreach (var hero in party)
                {
                    if (hero != null && !heroes.Contains(hero))
                    {
                        heroes.Add(hero);
                    }
                }
            }

            var equipment = Equipment(catalog);
            var baseStats = new Dictionary<HeroSO, Stats>();
            var worn = new Dictionary<HeroSO, Dictionary<SlotType, ItemSO>>();
            foreach (var hero in heroes)
            {
                baseStats[hero] = baseStatsFor != null ? baseStatsFor(hero) : null;
                worn[hero] = new Dictionary<SlotType, ItemSO>();
            }

            if (heroes.Count == 0 || equipment.Count == 0 || spend.GoldBudget <= 0 || weightFor == null)
            {
                Publish(spend, heroes, worn);
                return spend;
            }

            int remaining = spend.GoldBudget;
            while (true)
            {
                GearPurchase best = null;
                float bestValue = 0f;

                foreach (var hero in heroes)
                {
                    var stats = baseStats[hero];
                    if (stats == null)
                    {
                        continue;
                    }

                    float current = Score(stats, worn[hero], weightFor);

                    foreach (var item in equipment)
                    {
                        int price = ShopPricing.BuyPrice(item);
                        if (price <= 0 || price > remaining)
                        {
                            continue;
                        }

                        worn[hero].TryGetValue(item.SlotType, out var displaced);
                        if (displaced == item)
                        {
                            continue;
                        }

                        worn[hero][item.SlotType] = item;
                        float after = Score(stats, worn[hero], weightFor);
                        if (displaced != null)
                        {
                            worn[hero][item.SlotType] = displaced;
                        }
                        else
                        {
                            worn[hero].Remove(item.SlotType);
                        }

                        float gain = after - current;
                        if (gain <= 0f)
                        {
                            // A sidegrade or a downgrade is not an investment, whatever it costs.
                            continue;
                        }

                        var candidate = new GearPurchase
                        {
                            Hero = hero,
                            Item = item,
                            Replaced = displaced,
                            Price = price,
                            PowerGain = gain
                        };
                        float value = gain / price;

                        if (best == null || Beats(candidate, value, best, bestValue))
                        {
                            best = candidate;
                            bestValue = value;
                        }
                    }
                }

                if (best == null)
                {
                    break;
                }

                worn[best.Hero][best.Item.SlotType] = best.Item;
                remaining -= best.Price;
                spend.GoldSpent += best.Price;
                spend.Purchases.Add(best);
            }

            Publish(spend, heroes, worn);
            return spend;
        }

        /// <summary>
        /// The convenience form: derives each hero's pre-gear stats from a flat XP budget the same
        /// way <see cref="PartyBaseline"/> does, so a caller holding only "this much XP, this much
        /// gold" gets a loadout consistent with the party that will wear it.
        /// </summary>
        public static GearSpend Spend(
            IList<HeroSO> party,
            int xpBudgetPerHero,
            IList<ItemSO> catalog,
            int goldBudget,
            Func<StatType, float> weightFor)
        {
            return Spend(party, hero => StatsAtXp(hero, xpBudgetPerHero), catalog, goldBudget, weightFor);
        }

        /// <summary>A hero's stats after greedy-spending <paramref name="xpBudget"/> and no gear.</summary>
        public static Stats StatsAtXp(HeroSO hero, int xpBudget)
        {
            if (hero == null)
            {
                return null;
            }
            var nodes = SphereGridOps.GreedySpend(hero.SphereGrid, null, Mathf.Max(0, xpBudget), out _);
            return HeroStatCalculator.BaseStatsForNodes(hero, nodes);
        }

        /// <summary>
        /// Every piece of equipment in <paramref name="catalog"/>, in a stable order. Consumables
        /// are not gear and a null row is an authoring accident, so both are dropped here rather
        /// than guarded at three call sites.
        /// </summary>
        public static List<ItemSO> Equipment(IList<ItemSO> catalog)
        {
            var equipment = new List<ItemSO>();
            if (catalog == null)
            {
                return equipment;
            }

            foreach (var item in catalog)
            {
                if (item != null && item.Category == ItemCategory.Equipment && !equipment.Contains(item))
                {
                    equipment.Add(item);
                }
            }

            equipment.Sort((a, b) => string.CompareOrdinal(NameOf(a), NameOf(b)));
            return equipment;
        }

        /// <summary>
        /// What the whole catalog costs one hero — the gold past which the gear axis saturates,
        /// so a sweep can stop laddering. One item per slot, dearest first, since that is what a
        /// greedy spend converges on given enough gold.
        /// </summary>
        public static int FullLoadoutPrice(IList<ItemSO> catalog)
        {
            var bySlot = new Dictionary<SlotType, int>();
            foreach (var item in Equipment(catalog))
            {
                int price = ShopPricing.BuyPrice(item);
                if (!bySlot.TryGetValue(item.SlotType, out int best) || price > best)
                {
                    bySlot[item.SlotType] = price;
                }
            }

            int total = 0;
            foreach (var price in bySlot.Values)
            {
                total += price;
            }
            return total;
        }

        private static void Publish(
            GearSpend spend, List<HeroSO> heroes, Dictionary<HeroSO, Dictionary<SlotType, ItemSO>> worn)
        {
            foreach (var hero in heroes)
            {
                var gear = new List<ItemSO>();
                foreach (var slot in worn[hero].Values)
                {
                    gear.Add(slot);
                }
                gear.Sort((a, b) => string.CompareOrdinal(NameOf(a), NameOf(b)));
                spend.Record(hero, gear);
            }
        }

        /// <summary>
        /// Best value per gold wins; ties fall to the bigger absolute gain, then the cheaper item,
        /// then the name. Every tier of that is needed for determinism - two items at the same
        /// value-per-gold is not a hypothetical when prices come off a five-row rarity table.
        /// </summary>
        private static bool Beats(GearPurchase candidate, float value, GearPurchase best, float bestValue)
        {
            if (!Mathf.Approximately(value, bestValue))
            {
                return value > bestValue;
            }
            if (!Mathf.Approximately(candidate.PowerGain, best.PowerGain))
            {
                return candidate.PowerGain > best.PowerGain;
            }
            if (candidate.Price != best.Price)
            {
                return candidate.Price < best.Price;
            }

            int byItem = string.CompareOrdinal(NameOf(candidate.Item), NameOf(best.Item));
            if (byItem != 0)
            {
                return byItem < 0;
            }
            return string.CompareOrdinal(HeroNameOf(candidate.Hero), HeroNameOf(best.Hero)) < 0;
        }

        private static float Score(
            Stats baseStats, Dictionary<SlotType, ItemSO> worn, Func<StatType, float> weightFor)
        {
            var gear = new List<ItemSO>();
            foreach (var item in worn.Values)
            {
                gear.Add(item);
            }

            var effective = HeroStatCalculator.WithGear(baseStats, gear);

            float score = 0f;
            foreach (var stat in StatCatalog.Types)
            {
                score += effective[stat] * weightFor(stat);
            }
            return score;
        }

        private static string NameOf(ItemSO item)
        {
            if (item == null)
            {
                return "";
            }
            if (!string.IsNullOrEmpty(item.Key))
            {
                return item.Key;
            }
            return string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;
        }

        private static string HeroNameOf(HeroSO hero)
        {
            return hero == null ? "" : hero.SaveKey;
        }
    }
}
