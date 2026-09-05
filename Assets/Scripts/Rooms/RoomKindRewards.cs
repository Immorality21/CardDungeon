using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What a non-combat room pays out. Pure, so the numbers can be asserted and the balance model
    /// can price a level's refuges against its fights instead of guessing.
    /// </summary>
    public static class RoomKindRewards
    {
        /// <summary>
        /// Share of a hero's maximum health a refuge restores. A fraction rather than a flat number so
        /// it keeps its meaning as bars grow - the flat-heal mistake the analyzer already caught once
        /// with potions.
        /// </summary>
        public const float RestHealFraction = 0.35f;

        /// <summary>Gold in a cache at depth 1, before the per-depth step.</summary>
        public const int TreasureGoldBase = 15;

        /// <summary>Extra gold per floor of depth, so a cache deeper in a run is worth more.</summary>
        public const int TreasureGoldPerDepth = 10;

        /// <summary>
        /// Health one hero recovers in a refuge: a share of their <b>effective</b> maximum, rounded
        /// down, floor of 1. Never more than they are missing - that clamp is the caller's, since only
        /// it knows current health.
        /// </summary>
        public static int RestHealAmount(int effectiveMaxHealth)
        {
            if (effectiveMaxHealth <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.FloorToInt(effectiveMaxHealth * RestHealFraction));
        }

        /// <summary>Gold in a cache on the given 0-based run level. Depth is 1-based, like loot rolls.</summary>
        public static int TreasureGold(int runLevelIndex)
        {
            int depth = Mathf.Max(1, runLevelIndex + 1);
            return TreasureGoldBase + TreasureGoldPerDepth * (depth - 1);
        }

        /// <summary>
        /// The one item a cache yields, or null. Walks <paramref name="candidates"/> in the order given
        /// and returns the first that passes its <see cref="LootRoller"/> roll, so depth scaling and
        /// rarity are the same rules a kill drop follows.
        ///
        /// <para><b>At most one item</b>, deliberately: rolling the whole catalog would empty it into
        /// the party's bags. The caller shuffles, which is what makes *which* item vary.</para>
        ///
        /// <para>Materials are skipped here and rolled separately by
        /// <see cref="TreasureMaterials"/>. A material is common by design, so leaving them in this
        /// walk would mean the first shuffled material almost always won the single slot and a cache
        /// stopped producing gear entirely.</para>
        /// </summary>
        public static ItemSO PickTreasureItem(
            IList<ItemSO> candidates, int runLevelIndex, Func<float> roll)
        {
            if (candidates == null || roll == null)
            {
                return null;
            }

            foreach (var item in candidates)
            {
                if (item == null || item.Category == ItemCategory.Material)
                {
                    continue;
                }

                if (LootRoller.ShouldDrop(item, runLevelIndex, roll()))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// The raw stuff a cache yields, from the level's own <c>MaterialTable</c>. Every entry rolls
        /// separately, so a floor can be authored to reliably hand over its common material and
        /// occasionally its rare one.
        ///
        /// <para>Separate from <see cref="PickTreasureItem"/>'s single slot on purpose: gear from a
        /// cache is a find, materials from a cache are the floor's yield, and the two should not
        /// compete for the same roll. This is the room-side tap the enemy drop tables pair with.</para>
        /// </summary>
        public static List<LootAward> TreasureMaterials(
            IList<LootDrop> materialTable, int runLevelIndex, Func<float> roll)
        {
            return LootRoller.Roll(materialTable, runLevelIndex, roll);
        }

        /// <summary>
        /// Health a level's refuges are expected to give a party back — what the balance model adds to
        /// the sustain pool. Kept here so the model and the game read one number.
        /// </summary>
        public static int ExpectedRestHealing(int restRooms, int partyHealthPool)
        {
            if (restRooms <= 0 || partyHealthPool <= 0)
            {
                return 0;
            }

            // The clamp against missing health is not modelled: a player who rests at full health
            // wastes it, and pricing that in would credit the level for a mistake.
            return Mathf.FloorToInt(restRooms * partyHealthPool * RestHealFraction);
        }
    }
}
