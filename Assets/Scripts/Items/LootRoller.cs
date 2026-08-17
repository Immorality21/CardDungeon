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
