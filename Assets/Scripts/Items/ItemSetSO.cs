using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// A set of items that grants more the more of it a hero wears - the four Shadowweave pieces.
    /// Each piece points here (<see cref="ItemSO.Set"/>); the set lists what each count of pieces
    /// adds (<see cref="Tiers"/>). Every tier reached applies, so 4/4 also has 2/4 and 3/4.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Item Set")]
    public class ItemSetSO : ScriptableObject
    {
        [TextKeys("set", "name")]
        public string Key;
        [System.NonSerialized] private string _displayName;

        /// <summary>From the text table (<c>set.{key}.name</c>). Set only on instances built in code.</summary>
        public string DisplayName
        {
            get { return _displayName ?? Loc.AssetText("set", Key, this, "name"); }
            set { _displayName = value; }
        }

        [Tooltip("What wearing at least that many different pieces adds. Every tier reached applies.")]
        public List<ItemSetTier> Tiers = new List<ItemSetTier>();
    }

    /// <summary>What one count of worn pieces adds.</summary>
    [Serializable]
    public class ItemSetTier
    {
        [Tooltip("Different pieces of the set worn at once.")]
        [Min(2)] public int Pieces = 2;

        [System.NonSerialized] private string _text;

        /// <summary>From the text table (<see cref="TextKey"/>). Set only on instances built in code.</summary>
        public string Text
        {
            get { return _text ?? Loc.GetOrEmpty(TextKey); }
            set { _text = value; }
        }

        [Tooltip("Text table key (Tools > Texts).")]
        [TextKey]
        public string TextKey;

        [Tooltip("Stat bonuses, added through the same gear path as an item's own.")]
        public List<ItemBonus> Bonuses = new List<ItemBonus>();

        [Tooltip("More damage of one element from the wearer's abilities.")]
        public List<ElementDamageBonus> DamageBonuses = new List<ElementDamageBonus>();

        [Tooltip("Every ability hit also deals this percent of its raw power as Ability Extra Type damage.")]
        public float AbilityExtraPercent;

        public DamageType AbilityExtraType = DamageType.Shadow;
    }

    /// <summary>
    /// The rules for item sets. Pure, over the definitions a hero wears, so the live hero and the
    /// balance model's stand-in (<c>PartyBaseline</c>) read a set the same way.
    /// </summary>
    public static class ItemSets
    {
        /// <summary>How many different pieces of <paramref name="set"/> are in <paramref name="worn"/>.</summary>
        public static int PiecesWorn(ItemSetSO set, IEnumerable<ItemSO> worn)
        {
            if (set == null || worn == null)
            {
                return 0;
            }
            var seen = new List<ItemSO>();
            foreach (var item in worn)
            {
                if (item != null && item.Set == set && !seen.Contains(item))
                {
                    seen.Add(item);
                }
            }
            return seen.Count;
        }

        /// <summary>Every set tier the worn items reach.</summary>
        public static List<ItemSetTier> ActiveTiers(IEnumerable<ItemSO> worn)
        {
            var tiers = new List<ItemSetTier>();
            if (worn == null)
            {
                return tiers;
            }
            var items = new List<ItemSO>(worn);
            var sets = new List<ItemSetSO>();
            foreach (var item in items)
            {
                if (item != null && item.Set != null && !sets.Contains(item.Set))
                {
                    sets.Add(item.Set);
                }
            }
            foreach (var set in sets)
            {
                int pieces = PiecesWorn(set, items);
                foreach (var tier in set.Tiers)
                {
                    if (tier != null && pieces >= tier.Pieces)
                    {
                        tiers.Add(tier);
                    }
                }
            }
            return tiers;
        }

        /// <summary>The stat bonuses of every tier reached - summed with the items' own.</summary>
        public static IEnumerable<ItemBonus> BonusesOf(IEnumerable<ItemSO> worn)
        {
            foreach (var tier in ActiveTiers(worn))
            {
                if (tier.Bonuses == null)
                {
                    continue;
                }
                foreach (var bonus in tier.Bonuses)
                {
                    yield return bonus;
                }
            }
        }

        /// <summary>What the tiers reached add to the wearer's ability damage.</summary>
        public static OutgoingDamage OutgoingFor(IEnumerable<ItemSO> worn)
        {
            var tiers = ActiveTiers(worn);
            if (tiers.Count == 0)
            {
                return OutgoingDamage.None;
            }
            var outgoing = new OutgoingDamage();
            foreach (var tier in tiers)
            {
                if (tier.DamageBonuses != null)
                {
                    foreach (var bonus in tier.DamageBonuses)
                    {
                        if (bonus != null && bonus.Percent != 0f)
                        {
                            outgoing.Add(bonus.DamageType, bonus.Percent);
                        }
                    }
                }
                if (tier.AbilityExtraPercent > 0f)
                {
                    outgoing.AbilityExtraPercent += tier.AbilityExtraPercent;
                    outgoing.AbilityExtraType = tier.AbilityExtraType;
                }
            }
            return outgoing;
        }
    }
}
