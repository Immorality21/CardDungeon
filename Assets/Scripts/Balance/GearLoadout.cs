using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
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
    /// <para><b>Resistance counts.</b> The first version ranked on the stat line alone, so the Ruby
    /// Amulet (25% Fire) and the Leather Cap (10% Lightning) were bought for their bonuses and their
    /// resistance was worth nothing — while <c>PartyBaseline</c> handed that same resistance to the
    /// simulator, where it changed the fight. It is now priced as the equivalent health it buys
    /// against a given <see cref="IncomingDamageMix"/>, which is what makes it *conditional*: a Fire
    /// ward is worth a great deal on Emberfall and nothing on a floor that deals no fire. See
    /// <c>docs/BALANCING.md</c> §5q.</para>
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
            Func<StatType, float> weightFor,
            IncomingDamageMix incoming = null,
            Func<HeroSO, List<Resistance>> baseResistancesFor = null)
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
            var baseResists = new Dictionary<HeroSO, List<Resistance>>();
            var worn = new Dictionary<HeroSO, Dictionary<SlotType, ItemSO>>();
            foreach (var hero in heroes)
            {
                baseStats[hero] = baseStatsFor != null ? baseStatsFor(hero) : null;
                // A hero who already resists Fire from a grid node gets less out of a Fire ward, so
                // the innate layer has to be in the score - stacking the same element is the one
                // place a greedy spend would otherwise double-count itself.
                baseResists[hero] = baseResistancesFor != null
                    ? baseResistancesFor(hero)
                    : new List<Resistance>();
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

                    float current = Score(stats, worn[hero], weightFor, incoming, baseResists[hero]);

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
                        float after = Score(stats, worn[hero], weightFor, incoming, baseResists[hero]);
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
            Func<StatType, float> weightFor,
            IncomingDamageMix incoming = null)
        {
            return Spend(
                party,
                hero => StatsAtXp(hero, xpBudgetPerHero),
                catalog,
                goldBudget,
                weightFor,
                incoming,
                hero => ResistancesAtXp(hero, xpBudgetPerHero));
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
        /// A hero's resistances from grid nodes alone, at the same XP the stats are taken at. The
        /// gear spend needs these because an item's worth depends on what the hero already has:
        /// resistance is scored as the margin it adds, and that margin is not linear, so a hero whose
        /// grid already wards Fire values a Fire item differently from one who does not.
        /// </summary>
        public static List<Resistance> ResistancesAtXp(HeroSO hero, int xpBudget)
        {
            if (hero == null)
            {
                return new List<Resistance>();
            }
            var nodes = SphereGridOps.GreedySpend(hero.SphereGrid, null, Mathf.Max(0, xpBudget), out _);
            return SphereGridOps.ResistancesForNodes(hero.SphereGrid, nodes);
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

        /// <summary>
        /// Effective-HP multiplier a resistance set is worth against <paramref name="incoming"/>,
        /// capped here so a near-immunity does not price as infinite value.
        /// </summary>
        public const float MaxResistanceEffectiveHealthMultiplier = 10f;

        private static float Score(
            Stats baseStats,
            Dictionary<SlotType, ItemSO> worn,
            Func<StatType, float> weightFor,
            IncomingDamageMix incoming,
            List<Resistance> innate)
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

            return score + ResistanceValue(effective[StatType.MaxHealth], gear, innate, incoming, weightFor);
        }

        /// <summary>
        /// What a loadout's resistances are worth, in the same power-score units as the stat line.
        ///
        /// <para><b>The conversion.</b> Resistance does not add a stat; it makes the health pool go
        /// further. If a fraction <c>share</c> of incoming damage carries an element the hero resists
        /// by <c>r</c>, total damage taken scales by <c>1 - share*r</c>, so effective health scales by
        /// the reciprocal. Expressing that as *equivalent MaxHealth* and weighting it with MaxHealth's
        /// own power weight keeps resistance in one currency with everything else, and needs no new
        /// tuning constant — which matters, because a constant invented here would be a second,
        /// invisible balance lever.</para>
        ///
        /// <para>It also compounds the way the real thing does: the same ward is worth more on a
        /// bigger health bar, so the gold axis and the XP axis pull together rather than being
        /// independent. Gear resistance is summed with the hero's innate (grid) resistance and the
        /// value is the *margin* the gear adds — which for resistance <b>rises</b> as it stacks,
        /// since <c>1/(1-r)</c> is convex and 40%→80% halves incoming damage again just as 0%→50%
        /// did. That is what <c>DamageCalculator</c> actually does, so it is what the ranking has to
        /// say; <see cref="MaxResistanceEffectiveHealthMultiplier"/> is the only thing keeping a
        /// near-immunity from pricing as infinite.</para>
        /// </summary>
        public static float ResistanceValue(
            int maxHealth,
            IList<ItemSO> gear,
            IList<Resistance> innate,
            IncomingDamageMix incoming,
            Func<StatType, float> weightFor)
        {
            if (incoming == null || incoming.IsEmpty || maxHealth <= 0 || weightFor == null)
            {
                return 0f;
            }

            float withGear = EffectiveHealthMultiplier(Total(innate, gear), incoming);
            float without = EffectiveHealthMultiplier(Total(innate, null), incoming);
            return maxHealth * (withGear - without) * weightFor(StatType.MaxHealth);
        }

        private static Dictionary<DamageType, float> Total(IList<Resistance> innate, IList<ItemSO> gear)
        {
            var totals = new Dictionary<DamageType, float>();

            if (innate != null)
            {
                foreach (var resistance in innate)
                {
                    if (resistance == null)
                    {
                        continue;
                    }
                    totals.TryGetValue(resistance.DamageType, out float existing);
                    totals[resistance.DamageType] = existing + resistance.Percent;
                }
            }

            if (gear != null)
            {
                // The same summation the live game does when it equips - going through
                // InventoryOperations rather than re-adding the lists here keeps one definition of
                // "what this loadout resists".
                foreach (var resistance in InventoryOperations.ComputeResistances(gear))
                {
                    if (resistance == null)
                    {
                        continue;
                    }
                    totals.TryGetValue(resistance.DamageType, out float existing);
                    totals[resistance.DamageType] = existing + resistance.Percent;
                }
            }

            return totals;
        }

        private static float EffectiveHealthMultiplier(
            Dictionary<DamageType, float> resistances, IncomingDamageMix incoming)
        {
            float reduction = 0f;
            foreach (var kvp in resistances)
            {
                float share = incoming.ShareOf(kvp.Key);
                if (share <= 0f)
                {
                    continue;
                }
                // Clamped exactly as DamageCalculator clamps it, so an item cannot be ranked on
                // resistance the combat maths would refuse to apply.
                reduction += share * (Mathf.Clamp(kvp.Value, -100f, 200f) / 100f);
            }

            float remaining = 1f - reduction;
            float floor = 1f / MaxResistanceEffectiveHealthMultiplier;
            return 1f / Mathf.Max(floor, remaining);
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
