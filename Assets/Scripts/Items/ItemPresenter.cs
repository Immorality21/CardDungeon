using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;
using ImmoralityGaming.Localization;

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
                    return Loc.Get("items.slot.main-hand");
                case SlotType.OffHand:
                    return Loc.Get("items.slot.off-hand");
                case SlotType.Head:
                    return Loc.Get("items.slot.head");
                case SlotType.Chest:
                    return Loc.Get("items.slot.chest");
                case SlotType.Legs:
                    return Loc.Get("items.slot.legs");
                case SlotType.Hands:
                    return Loc.Get("items.slot.hands");
                case SlotType.Necklace:
                    return Loc.Get("items.slot.neck");
                default:
                    return slot.ToString();
            }
        }

        public static string RarityLabel(ItemRarity rarity)
        {
            return Loc.Get("items.rarity." + Loc.Slug(rarity.ToString()));
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
                lines.Add(Loc.Format("items.bonus.line", ("value", $"{sign}{amount}{unit}"), ("stat", StatCatalog.DisplayName(bonus.StatType))));
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
                chips.Add(Loc.Format("items.bonus.chip", ("value", $"{sign}{FormatNumber(bonus.Value)}{unit}"), ("stat", StatCatalog.ShortName(bonus.StatType))));
            }
            return chips;
        }

        /// <summary>
        /// One line per milestone (<see cref="ItemSO.Milestones"/>): "50 kills: +3 STR", with this
        /// copy's progress when its save entry is known - "50 kills: +3 STR (12/50)", or a tick once
        /// reached. What counts is the item's own business, so it is spelled out rather than hidden.
        /// </summary>
        public static List<string> MilestoneLines(ItemSO item, ItemSaveData entry = null)
        {
            var lines = new List<string>();
            if (item == null || item.Milestones == null)
            {
                return lines;
            }

            foreach (var milestone in item.Milestones)
            {
                if (milestone == null)
                {
                    continue;
                }
                var grants = new List<string>();
                if (milestone.Bonuses != null)
                {
                    foreach (var bonus in milestone.Bonuses)
                    {
                        if (bonus == null || bonus.StatType == StatType.None || Mathf.Approximately(bonus.Value, 0f))
                        {
                            continue;
                        }
                        string sign = bonus.Value > 0f ? "+" : string.Empty;
                        string unit = bonus.BonusType == BonusType.Percentage ? "%" : string.Empty;
                        grants.Add(Loc.Format("items.bonus.chip", ("value", $"{sign}{FormatNumber(bonus.Value)}{unit}"), ("stat", StatCatalog.ShortName(bonus.StatType))));
                    }
                }
                if (grants.Count == 0)
                {
                    continue;
                }

                string line = Loc.Format("items.milestone.line", ("count", milestone.Threshold), ("counter", CounterNoun(milestone.Counter, milestone.Threshold)), ("grants", string.Join(", ", grants)));
                if (entry != null)
                {
                    int have = ItemGrowth.Get(entry, milestone.Counter);
                    line = have >= milestone.Threshold
                        ? Loc.Format("items.milestone.reached", ("line", line))
                        : Loc.Format("items.milestone.progress", ("line", line), ("have", have), ("need", milestone.Threshold));
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>One line per reaction the item gives its wearer (<see cref="ItemSO.Triggers"/>), as
        /// authored in its <c>Text</c>; a reaction with no text is not listed.</summary>
        public static List<string> ReactionLines(ItemSO item)
        {
            var lines = new List<string>();
            if (item == null || item.Triggers == null)
            {
                return lines;
            }
            foreach (var trigger in item.Triggers)
            {
                if (trigger != null && !string.IsNullOrEmpty(trigger.Text))
                {
                    lines.Add(trigger.Text);
                }
            }
            return lines;
        }

        /// <summary>The set a piece belongs to, one line per tier: "2 pieces: +10% Shadow damage".</summary>
        public static List<string> SetLines(ItemSO item)
        {
            var lines = new List<string>();
            if (item == null || item.Set == null || item.Set.Tiers == null)
            {
                return lines;
            }
            foreach (var tier in item.Set.Tiers)
            {
                if (tier != null && !string.IsNullOrEmpty(tier.Text))
                {
                    lines.Add(Loc.Format("items.set.tier-line", ("pieces", tier.Pieces), ("text", tier.Text)));
                }
            }
            return lines;
        }

        private static string CounterNoun(ItemCounterKind kind, int count)
        {
            bool one = count == 1;
            switch (kind)
            {
                case ItemCounterKind.Kills:
                    return one ? Loc.Get("items.counter.kill") : Loc.Get("items.counter.kills");
                case ItemCounterKind.BossKills:
                    return one ? Loc.Get("items.counter.boss-slain") : Loc.Get("items.counter.bosses-slain");
                case ItemCounterKind.Victories:
                    return one ? Loc.Get("items.counter.victory") : Loc.Get("items.counter.victories");
                case ItemCounterKind.AbilitiesUsed:
                    return one ? Loc.Get("items.counter.ability-used") : Loc.Get("items.counter.abilities-used");
                default:
                    return kind.ToString();
            }
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
                lines.Add(Loc.Format("items.resistance.line", ("type", DamageTypeName(resistance.DamageType)), ("value", Signed(resistance.Percent))));
            }
            return lines;
        }

        /// <summary>
        /// What a consumable does, as one sentence. <paramref name="maxHealth"/> is optional: with
        /// it, a healing item says what it restores for that hero rather than its formula.
        /// </summary>
        public static string ConsumableEffectLine(ItemSO item, int maxHealth = 0, float potencyBonus = 0f, int extraCharges = 0)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.ConsumableEffect == ConsumableEffectType.CureStatus)
            {
                return Loc.Get("items.consumable.cure-status");
            }

            int charges = Mathf.Max(1, item.ConsumableCharges + extraCharges);
            string chargeText = charges == 1 ? Loc.Get("items.consumable.one-charge") : Loc.Format("items.consumable.charges", ("charges", charges));
            if (item.ConsumableEffect == ConsumableEffectType.RestoreCharge)
            {
                return Loc.Format("items.consumable.restore-charge", ("charges", chargeText));
            }
            if (item.ConsumableEffect == ConsumableEffectType.HealAndCharge)
            {
                return HealLine(item, maxHealth, potencyBonus) + " " + Loc.Format("items.consumable.also-restores", ("charges", chargeText));
            }
            return HealLine(item, maxHealth, potencyBonus);
        }

        private static string HealLine(ItemSO item, int maxHealth, float potencyBonus)
        {
            if (maxHealth > 0)
            {
                return Loc.Format("items.consumable.heal-amount", ("amount", item.HealAmountFor(maxHealth, potencyBonus)));
            }

            int percent = Mathf.RoundToInt((item.ConsumablePercent + Mathf.Max(0f, potencyBonus)) * 100f);
            if (item.ConsumableAmount > 0 && percent > 0)
            {
                return Loc.Format("items.consumable.heal-flat-plus-percent", ("amount", item.ConsumableAmount), ("percent", percent));
            }
            if (percent > 0)
            {
                return Loc.Format("items.consumable.heal-percent", ("percent", percent));
            }
            return Loc.Format("items.consumable.heal-amount", ("amount", item.ConsumableAmount));
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
        /// <see cref="SwapIn(IEnumerable{ItemSO}, ItemSO)"/> over worn copies, so the pieces left on
        /// keep their grown bonuses and the candidate brings its own. The input list is not modified.
        /// </summary>
        public static List<GearPiece> SwapIn(IEnumerable<GearPiece> equipped, GearPiece candidate)
        {
            var slot = candidate?.Item != null ? candidate.Item.SlotType : (SlotType?)null;
            var result = new List<GearPiece>();
            if (equipped != null)
            {
                foreach (var piece in equipped)
                {
                    if (piece?.Item != null && (slot == null || piece.Item.SlotType != slot.Value))
                    {
                        result.Add(piece);
                    }
                }
            }
            if (candidate?.Item != null)
            {
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>The worn copies with <paramref name="slot"/> emptied. The input list is not modified.</summary>
        public static List<GearPiece> SwapOut(IEnumerable<GearPiece> equipped, SlotType slot)
        {
            var result = new List<GearPiece>();
            if (equipped == null)
            {
                return result;
            }
            foreach (var piece in equipped)
            {
                if (piece?.Item != null && piece.Item.SlotType != slot)
                {
                    result.Add(piece);
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
            return type == DamageType.Normal ? Loc.Get("items.damage-type.physical") : type.ToString();
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
