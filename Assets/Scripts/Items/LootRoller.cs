using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// Pure loot-drop math: how likely an item is to drop given its rarity and how its item level
    /// compares to the current run depth. Deterministic (the caller supplies the random roll) so it
    /// is unit-testable in the same spirit as <c>DamageCalculator</c>.
    ///
    /// Rarer items drop less often; items whose <see cref="ItemSO.ItemLevel"/> sits above the
    /// current depth are increasingly suppressed, so stronger gear naturally surfaces deeper in a run.
    ///
    /// <para>A <see cref="LootDrop"/> may opt out of that math with a flat <c>Chance</c>, and may pay
    /// out a quantity range. See <see cref="Roll"/> — the whole-table entry point every drop site
    /// (kills, caches) goes through, so "what does this drop" has one answer.</para>
    /// </summary>
    public static class LootRoller
    {
        // Base drop chance per rarity (before level scaling).
        private const float CommonChance = 0.60f;
        private const float UncommonChance = 0.40f;
        private const float RareChance = 0.25f;
        private const float EpicChance = 0.12f;
        private const float LegendaryChance = 0.05f;

        // Each item level above the current depth multiplies the chance by this factor.
        private const float OverLevelFalloff = 0.5f;

        /// <summary>Probability [0,1] that <paramref name="item"/> drops at the given 0-based run level.</summary>
        public static float DropChance(ItemSO item, int runLevelIndex)
        {
            if (item == null)
            {
                return 0f;
            }

            float chance = RarityBaseChance(item.Rarity);

            // Depth is 1-based (level index 0 == depth 1). Items above the current depth are rarer.
            int depth = Mathf.Max(1, runLevelIndex + 1);
            int levelsOver = item.ItemLevel - depth;
            if (levelsOver > 0)
            {
                chance *= Mathf.Pow(OverLevelFalloff, levelsOver);
            }

            return Mathf.Clamp01(chance);
        }

        /// <summary>
        /// Whether the item drops, given a caller-supplied <paramref name="roll"/> in [0,1)
        /// (e.g. <c>Random.Range(0f, 1f)</c>). Kept explicit so drops are deterministic under test.
        /// </summary>
        public static bool ShouldDrop(ItemSO item, int runLevelIndex, float roll)
        {
            return roll < DropChance(item, runLevelIndex);
        }

        // ============================================================
        //  DROP TABLES
        // ============================================================

        /// <summary>
        /// Probability [0,1] that one table entry pays out. An entry with an explicit
        /// <see cref="LootDrop.Chance"/> is that flat number at any depth; otherwise it falls back on
        /// the item's rarity and level, which is what gear has always done.
        /// </summary>
        public static float DropChance(LootDrop drop, int runLevelIndex)
        {
            if (drop == null || drop.Item == null)
            {
                return 0f;
            }
            if (drop.HasExplicitChance)
            {
                return Mathf.Clamp01(drop.Chance);
            }
            return DropChance(drop.Item, runLevelIndex);
        }

        /// <summary>
        /// Units this entry pays when it hits: uniform over [MinQuantity, MaxQuantity], picked with a
        /// caller-supplied <paramref name="roll"/> in [0,1). Non-stacking items (equipment) are always
        /// one, because an inventory entry for them carries an equipped slot and cannot be a pile.
        /// </summary>
        public static int RollQuantity(LootDrop drop, float roll)
        {
            if (drop == null || drop.Item == null)
            {
                return 0;
            }

            if (!drop.Item.Stacks)
            {
                return 1;
            }

            int min = Mathf.Max(1, drop.MinQuantity);
            int max = Mathf.Max(min, drop.MaxQuantity);
            int span = max - min + 1;
            if (span <= 1)
            {
                return min;
            }

            int offset = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll) * span), 0, span - 1);
            return min + offset;
        }

        /// <summary>Average units an entry is worth per roll — its chance times its mean quantity.</summary>
        public static float ExpectedQuantity(LootDrop drop, int runLevelIndex)
        {
            if (drop == null || drop.Item == null)
            {
                return 0f;
            }

            int min = Mathf.Max(1, drop.MinQuantity);
            int max = Mathf.Max(min, drop.MaxQuantity);
            float mean = drop.Item.Stacks ? (min + max) * 0.5f : 1f;
            return DropChance(drop, runLevelIndex) * mean;
        }

        /// <summary>
        /// Rolls a whole drop table. <b>Every</b> entry is rolled independently — a table is a list of
        /// things this kill can yield, not a pick-one — so an enemy can carry both a signature weapon
        /// and the scrap it is made of. One roll decides whether an entry lands and a second sizes it,
        /// and the caller supplies the source so a drop is reproducible under test.
        /// </summary>
        public static List<LootAward> Roll(IList<LootDrop> table, int runLevelIndex, Func<float> roll)
        {
            var awards = new List<LootAward>();
            if (table == null || roll == null)
            {
                return awards;
            }

            foreach (var drop in table)
            {
                if (drop == null || drop.Item == null)
                {
                    continue;
                }
                if (roll() >= DropChance(drop, runLevelIndex))
                {
                    continue;
                }

                int quantity = RollQuantity(drop, roll());
                if (quantity > 0)
                {
                    awards.Add(new LootAward(drop.Item, quantity));
                }
            }

            return awards;
        }

        private static float RarityBaseChance(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:
                    return CommonChance;
                case ItemRarity.Uncommon:
                    return UncommonChance;
                case ItemRarity.Rare:
                    return RareChance;
                case ItemRarity.Epic:
                    return EpicChance;
                case ItemRarity.Legendary:
                    return LegendaryChance;
                default:
                    return CommonChance;
            }
        }
    }
}
