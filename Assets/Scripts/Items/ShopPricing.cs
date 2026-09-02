using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// Pure gold-pricing math for the merchant: what a piece of gear costs to buy and what it
    /// fetches when sold back (at a loss). Price scales with rarity and item level. Deterministic
    /// and stateless so it's unit-testable in the same spirit as <c>LootRoller</c>/<c>DamageCalculator</c>.
    /// </summary>
    public static class ShopPricing
    {
        // Fraction of the buy price the merchant pays when buying gear back from the player.
        public const float SellFraction = 0.4f;

        // Each item level above 1 adds this fraction of the base cost.
        private const float LevelStep = 0.25f;

        /// <summary>Gold cost to buy <paramref name="item"/> from the merchant.</summary>
        public static int BuyPrice(ItemSO item)
        {
            if (item == null)
            {
                return 0;
            }

            float levelFactor = 1f + LevelStep * Mathf.Max(0, item.ItemLevel - 1);
            return Mathf.Max(1, Mathf.RoundToInt(RarityBaseCost(item.Rarity) * levelFactor));
        }

        /// <summary>Gold the player receives for selling <paramref name="item"/> (buy price × sell fraction).</summary>
        public static int SellPrice(ItemSO item)
        {
            if (item == null)
            {
                return 0;
            }
            return Mathf.Max(1, Mathf.RoundToInt(BuyPrice(item) * SellFraction));
        }

        /// <summary>
        /// Gold the tavern charges for <paramref name="hero"/>. Uses the authored
        /// <c>HeroSO.RecruitCost</c> when set; otherwise derives one from the stat line so a hero
        /// added to the catalog is immediately hireable without also needing a price. The weights
        /// mirror the balance model's view that a point of Attack or Agility is worth more than a
        /// point of HP.
        /// </summary>
        public static int RecruitPrice(Assets.Scripts.Heroes.HeroSO hero)
        {
            if (hero == null)
            {
                return 0;
            }
            if (hero.RecruitCost > 0)
            {
                return hero.RecruitCost;
            }

            // Weights come from StatCatalog, so a stat added to the enum is priced by its own row
            // rather than being silently worth nothing here - which is how Intelligence, Spirit and
            // Luck ended up contributing zero to a caster's price.
            float score = 0f;
            foreach (var stat in StatCatalog.Types)
            {
                score += hero.BaseStats[stat] * StatCatalog.Of(stat).RecruitWeight;
            }
            return Mathf.Max(25, Mathf.RoundToInt(score / 5f) * 5);
        }

        /// <summary>
        /// Base gold for a rarity, before <see cref="LevelStep"/> scales it by item level.
        ///
        /// <para><b>Raised ~10% on 2026-09-02</b>, from 20/45/90/180/350. Gear measured as roughly
        /// 2.4x the survivability per investment point that the sphere grid buys
        /// (<c>docs/BALANCING.md</c> §5p), and the call was that the *items* are about right and the
        /// *rate* was wrong - so almost all of that correction lives in
        /// <c>BalanceRulesSO.InvestmentPointsPerGold</c> and only a light nudge lives here. Rounded to
        /// readable numbers rather than exact multiples: a shop price the player reads is worth more
        /// than a tenth of a gold piece of precision.</para>
        /// </summary>
        private static int RarityBaseCost(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:
                    return 22;
                case ItemRarity.Uncommon:
                    return 50;
                case ItemRarity.Rare:
                    return 100;
                case ItemRarity.Epic:
                    return 200;
                case ItemRarity.Legendary:
                    return 385;
                default:
                    return 22;
            }
        }
    }
}
