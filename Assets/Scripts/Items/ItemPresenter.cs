using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// How an item reads to the player - slot and rarity names, its bonuses as sentences, what a
    /// consumable does - and the swap arithmetic behind the equipment screen's before/after preview.
    /// Pure, so the hub inventory, the merchant and the tests all word an item the same way
    /// (<c>ItemPresenterTests</c>).
    /// </summary>
    public static class ItemPresenter
    {
        /// <summary>Every equipment slot, in the order the equipment screen lists them.</summary>
        public static readonly SlotType[] SlotOrder =
        {
            SlotType.MainHand,
            SlotType.OffHand,
            SlotType.Head,
            SlotType.Chest,
            SlotType.Hands,
            SlotType.Legs,
            SlotType.Necklace,
        };

        /// <summary>The slot as a player says it - "Main Hand", never the enum's "MainHand".</summary>
        public static string SlotLabel(SlotType slot)
        {
            switch (slot)
            {
                case SlotType.MainHand:
                    return "Main Hand";
                case SlotType.OffHand:
                    return "Off Hand";
                case SlotType.Head:
                    return "Head";
                case SlotType.Chest:
                    return "Chest";
                case SlotType.Legs:
                    return "Legs";
                case SlotType.Hands:
                    return "Hands";
                case SlotType.Necklace:
                    return "Neck";
                default:
                    return slot.ToString();
            }
        }

        public static string RarityLabel(ItemRarity rarity)
        {
            return rarity.ToString();
        }

        /// <summary>The theme class that colours a name or an icon frame by rarity.</summary>
        public static string RarityClass(ItemRarity rarity)
        {
            return "cd-rarity--" + rarity.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// One line per bonus: "+3 Strength", "+10% Max Health", "-2 Agility". Zero-value rows are
        /// skipped - they are half-authored, not a bonus.
        /// </summary>
        public static List<string> BonusLines(ItemSO item)
        {
            var lines = new List<string>();
            if (item == null || item.Bonuses == null)
            {
                return lines;
            }

            foreach (var bonus in item.Bonuses)
            {
                if (bonus == null || bonus.StatType == StatType.None || Mathf.Approximately(bonus.Value, 0f))
                {
                    continue;
                }

                string amount = FormatNumber(bonus.Value);
                string sign = bonus.Value > 0f ? "+" : string.Empty;
                string unit = bonus.BonusType == BonusType.Percentage ? "%" : string.Empty;
                lines.Add($"{sign}{amount}{unit} {StatCatalog.DisplayName(bonus.StatType)}");
            }
            return lines;
        }

        /// <summary>
        /// The same bonuses in short form - "+3 STR", "+10% HP" - for places that show them as a row
        /// of chips rather than a list (the equipment detail pane, which must fit without scrolling).
        /// </summary>
        public static List<string> BonusChips(ItemSO item)
        {
            var chips = new List<string>();
            if (item == null || item.Bonuses == null)
            {
                return chips;
            }

            foreach (var bonus in item.Bonuses)
            {
                if (bonus == null || bonus.StatType == StatType.None || Mathf.Approximately(bonus.Value, 0f))
                {
                    continue;
                }

                string sign = bonus.Value > 0f ? "+" : string.Empty;
                string unit = bonus.BonusType == BonusType.Percentage ? "%" : string.Empty;
                chips.Add($"{sign}{FormatNumber(bonus.Value)}{unit} {StatCatalog.ShortName(bonus.StatType)}");
            }
            return chips;
        }

        /// <summary>One line per resistance the item grants: "Fire +25%", "Ice -10%".</summary>
        public static List<string> ResistanceLines(ItemSO item)
        {
            var lines = new List<string>();
            if (item == null || item.Resistances == null)
            {
                return lines;
            }

            foreach (var resistance in item.Resistances)
            {
                if (resistance == null || Mathf.Approximately(resistance.Percent, 0f))
                {
                    continue;
                }
                lines.Add($"{DamageTypeName(resistance.DamageType)} {Signed(resistance.Percent)}%");
            }
            return lines;
        }

        /// <summary>
        /// What a consumable does, as one sentence. <paramref name="maxHealth"/> is optional: with
        /// it, a healing item says what it restores for that hero rather than its formula.
        /// </summary>
        public static string ConsumableEffectLine(ItemSO item, int maxHealth = 0)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.ConsumableEffect == ConsumableEffectType.CureStatus)
            {
                return "Cures burning, poison, bleeding, freezing, slow and silence.";
            }

            if (maxHealth > 0)
            {
                return $"Restores {item.HealAmountFor(maxHealth)} health.";
            }

            int percent = Mathf.RoundToInt(item.ConsumablePercent * 100f);
            if (item.ConsumableAmount > 0 && percent > 0)
            {
                return $"Restores {item.ConsumableAmount} health plus {percent}% of the drinker's maximum.";
            }
            if (percent > 0)
            {
                return $"Restores {percent}% of the drinker's maximum health.";
            }
            return $"Restores {item.ConsumableAmount} health.";
        }

        /// <summary>
        /// The loadout with <paramref name="candidate"/> worn: whatever sat in its slot comes off,
        /// the candidate goes on. The input list is not modified.
        /// </summary>
        public static List<ItemSO> SwapIn(IEnumerable<ItemSO> equipped, ItemSO candidate)
        {
            var result = new List<ItemSO>();
            if (equipped != null)
            {
                foreach (var item in equipped)
                {
                    if (item != null && (candidate == null || item.SlotType != candidate.SlotType))
                    {
                        result.Add(item);
                    }
                }
            }
            if (candidate != null)
            {
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>The loadout with <paramref name="slot"/> emptied. The input list is not modified.</summary>
        public static List<ItemSO> SwapOut(IEnumerable<ItemSO> equipped, SlotType slot)
        {
            var result = new List<ItemSO>();
            if (equipped == null)
            {
                return result;
            }
            foreach (var item in equipped)
            {
                if (item != null && item.SlotType != slot)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Total resistance per damage type from a loadout plus any extra sources (the sphere grid's
        /// resistance nodes), summed the way combat sums them. Types that total zero are left out.
        /// </summary>
        public static Dictionary<DamageType, float> SumResistances(
            IEnumerable<ItemSO> gear, IEnumerable<Resistance> extra = null)
        {
            var totals = new Dictionary<DamageType, float>();
            if (gear != null)
            {
                foreach (var item in gear)
                {
                    if (item == null || item.Resistances == null)
                    {
                        continue;
                    }
                    foreach (var resistance in item.Resistances)
                    {
                        AddTo(totals, resistance);
                    }
                }
            }
            if (extra != null)
            {
                foreach (var resistance in extra)
                {
                    AddTo(totals, resistance);
                }
            }

            var zero = new List<DamageType>();
            foreach (var entry in totals)
            {
                if (Mathf.Approximately(entry.Value, 0f))
                {
                    zero.Add(entry.Key);
                }
            }
            foreach (var type in zero)
            {
                totals.Remove(type);
            }
            return totals;
        }

        /// <summary>"Physical" for Normal, the element's own name otherwise.</summary>
        public static string DamageTypeName(DamageType type)
        {
            return type == DamageType.Normal ? "Physical" : type.ToString();
        }

        private static void AddTo(Dictionary<DamageType, float> totals, Resistance resistance)
        {
            if (resistance == null)
            {
                return;
            }
            totals.TryGetValue(resistance.DamageType, out float current);
            totals[resistance.DamageType] = current + resistance.Percent;
        }

        private static string Signed(float value)
        {
            return (value > 0f ? "+" : string.Empty) + FormatNumber(value);
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
        }
    }
}
