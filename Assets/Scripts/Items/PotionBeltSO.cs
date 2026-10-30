using System;
using System.Collections.Generic;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// The potion belt (playtest 3 finding 5): a few <b>slots</b>, each holding one potion
    /// <b>type</b> - Healing, Antidote, Energy, Elixir - with that type's own capacity. The slots are
    /// filled at the campfire before a run, refill on the run's first floor only, and everything else
    /// is sold at the Merchant for gold plus materials: more slots, each type's recipe, and each
    /// type's capacity and potency ladders. The later steps ask for materials that only drop on
    /// revisits (<see cref="ItemSO.RevisitFear"/>), so the belt grows with the campaign.
    ///
    /// <para>One asset, <c>Resources/PotionBelt.asset</c>, loaded like <c>Revisits.asset</c>. The
    /// balance model ignores the belt on purpose (owner, 2026-10-27): it still gives every floor a full
    /// healing belt at base potency (<c>docs/BALANCING.md</c> §5am).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Potion Belt")]
    public class PotionBeltSO : ScriptableObject
    {
        public const string ResourcePath = "PotionBelt";

        [Tooltip("Slots a fresh save has.")]
        [Min(1)]
        public int StartingSlots = 2;

        [Tooltip("More slots on sale, smallest first.")]
        public List<BeltSlotTier> SlotTiers = new List<BeltSlotTier>();

        [Tooltip("Every potion type, in the order the campfire cycles them.")]
        public List<BeltPotionType> Types = new List<BeltPotionType>();

        /// <summary>The slots the belt can ever have.</summary>
        public int MaxSlots
        {
            get
            {
                int max = StartingSlots;
                foreach (var tier in SlotTiers)
                {
                    if (tier != null)
                    {
                        max = Mathf.Max(max, tier.Slots);
                    }
                }
                return max;
            }
        }

        public BeltPotionType Find(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Types.Find(t => t != null && t.Key == key);
        }

        /// <summary>The type <paramref name="potion"/> belongs to, or null.</summary>
        public BeltPotionType TypeOf(ItemSO potion)
        {
            return potion == null ? null : Types.Find(t => t != null && t.Potion != null && t.Potion.Key == potion.Key);
        }

        private static PotionBeltSO _cached;

        /// <summary>The project's belt, or null when no asset is authored (the Merchant then sells none).</summary>
        public static PotionBeltSO Load()
        {
            if (_cached == null)
            {
                // Qualified: the game has its own Assets.Scripts.Resources namespace.
                _cached = UnityEngine.Resources.Load<PotionBeltSO>(ResourcePath);
            }
            return _cached;
        }
    }

    /// <summary>
    /// One potion type: the potion it carries and its ladders. Upgrades belong to the type, so a
    /// later second potion of the same type (Frost and Fire Resistance) would share them.
    /// </summary>
    [Serializable]
    public class BeltPotionType
    {
        [Tooltip("Save id. Never rename one a save may hold.")]
        [TextKeys("potion", "name")]
        public string Key;

        [System.NonSerialized] private string _displayName;

        /// <summary>From the text table (<c>potion.{key}.name</c>). Set only on instances built in code.</summary>
        public string DisplayName
        {
            get { return _displayName ?? Loc.AssetText("potion", Key, null, "name"); }
            set { _displayName = value; }
        }

        [Tooltip("The potion a slot of this type carries.")]
        public ItemSO Potion;

        [Tooltip("Known from the start (Healing). Otherwise its recipe is bought at the Merchant.")]
        public bool StartsUnlocked;

        [Tooltip("The recipe is on sale only once this run is cleared. Empty: from the start.")]
        public string RequiredRunKey;

        [Tooltip("What the recipe costs.")]
        public BeltPrice Unlock = new BeltPrice();

        [Tooltip("Potions a slot of this type carries before any capacity step. Healing is 2 (owner); " +
                 "its capacity lives in ResourceMaximums.json, where the balance model reads it.")]
        [Min(1)]
        public int BaseCapacity = 1;

        public List<BeltCapacityTier> CapacityTiers = new List<BeltCapacityTier>();

        public List<BeltPotencyTier> PotencyTiers = new List<BeltPotencyTier>();
    }

    /// <summary>A price: gold plus materials. What every step costs.</summary>
    [Serializable]
    public class BeltPrice
    {
        [Min(0)]
        public int Gold;

        public List<MaterialCost> Materials = new List<MaterialCost>();
    }

    [Serializable]
    public class BeltSlotTier : BeltPrice
    {
        [Tooltip("Slots the belt has once this step is bought.")]
        [Min(1)]
        public int Slots;
    }

    [Serializable]
    public class BeltCapacityTier : BeltPrice
    {
        [Tooltip("Potions a slot of this type carries once this step is bought.")]
        [Min(1)]
        public int Capacity;
    }

    [Serializable]
    public class BeltPotencyTier : BeltPrice
    {
        [Tooltip("Share of the drinker's max health added to a healing potion's own percentage. " +
                 "0.1 = +10% max HP. Cumulative steps: write the total, not the increase.")]
        [Range(0f, 1f)]
        public float HealPercent;

        [Tooltip("Ability charges added to what the potion restores (Energy). Cumulative, like HealPercent.")]
        [Min(0)]
        public int Charges;
    }

    /// <summary>The belt's rules, pure so they are testable without a save (<c>PotionBeltTests</c>).</summary>
    public static class PotionBeltOps
    {
        /// <summary>The type key meaning "nothing in this slot".</summary>
        public const string Empty = "";

        /// <summary>The next slot step for a belt of <paramref name="slots"/>, or null at the top.</summary>
        public static BeltSlotTier NextSlot(PotionBeltSO belt, int slots)
        {
            if (belt == null || belt.SlotTiers == null)
            {
                return null;
            }
            return belt.SlotTiers.Find(t => t != null && t.Slots > slots);
        }

        /// <summary>The next capacity step for a slot of <paramref name="type"/> carrying <paramref name="capacity"/>, or null.</summary>
        public static BeltCapacityTier NextCapacity(BeltPotionType type, int capacity)
        {
            if (type == null || type.CapacityTiers == null)
            {
                return null;
            }
            return type.CapacityTiers.Find(t => t != null && t.Capacity > capacity);
        }

        /// <summary>The potency step after <paramref name="bought"/> steps, or null when all are bought.</summary>
        public static BeltPotencyTier NextPotency(BeltPotionType type, int bought)
        {
            if (type == null || type.PotencyTiers == null || bought < 0 || bought >= type.PotencyTiers.Count)
            {
                return null;
            }
            return type.PotencyTiers[bought];
        }

        /// <summary>The potency step reached after <paramref name="bought"/> steps, or null for none.</summary>
        public static BeltPotencyTier PotencyReached(BeltPotionType type, int bought)
        {
            if (type == null || type.PotencyTiers == null || bought <= 0 || type.PotencyTiers.Count == 0)
            {
                return null;
            }
            return type.PotencyTiers[Mathf.Min(bought, type.PotencyTiers.Count) - 1];
        }

        /// <summary>The share of max health added to a potion of <paramref name="type"/> after <paramref name="bought"/> steps.</summary>
        public static float HealBonus(BeltPotionType type, int bought)
        {
            var tier = PotencyReached(type, bought);
            return tier != null ? Mathf.Max(0f, tier.HealPercent) : 0f;
        }

        /// <summary>Ability charges added to a potion of <paramref name="type"/> after <paramref name="bought"/> steps.</summary>
        public static int ChargeBonus(BeltPotionType type, int bought)
        {
            var tier = PotencyReached(type, bought);
            return tier != null ? Mathf.Max(0, tier.Charges) : 0;
        }

        /// <summary>
        /// The healing belt the balance model assumes (owner, 2026-10-28): every capacity and potency
        /// step is bought the moment all its materials can be had, gold ignored. Steps are bought in
        /// order, so one unavailable step stops its ladder there.
        /// </summary>
        public static void AssumedSteps(BeltPotionType type, Func<ItemSO, bool> available, out int capacity, out int potencySteps)
        {
            capacity = type != null ? type.BaseCapacity : 0;
            potencySteps = 0;
            if (type == null || available == null)
            {
                return;
            }
            foreach (var tier in type.CapacityTiers)
            {
                if (tier == null || !Obtainable(tier, available))
                {
                    break;
                }
                capacity = Math.Max(capacity, tier.Capacity);
            }
            foreach (var tier in type.PotencyTiers)
            {
                if (tier == null || !Obtainable(tier, available))
                {
                    break;
                }
                potencySteps++;
            }
        }

        private static bool Obtainable(BeltPrice price, Func<ItemSO, bool> available)
        {
            if (price.Materials == null)
            {
                return true;
            }
            foreach (var line in price.Materials)
            {
                if (line != null && line.Material != null && !available(line.Material))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The saved loadout made whole: exactly <paramref name="slots"/> entries, unknown or locked
        /// types and repeats emptied, and a save that never chose anything starting with Healing in
        /// slot 1 (the belt every save had before slots existed).
        /// </summary>
        public static List<string> Loadout(PotionBeltSO belt, IList<string> saved, int slots, Func<string, bool> unlocked)
        {
            var result = new List<string>();
            bool never = saved == null || saved.Count == 0;
            for (int i = 0; i < slots; i++)
            {
                string key = !never && i < saved.Count ? saved[i] : Empty;
                bool usable = belt != null && belt.Find(key) != null && unlocked != null && unlocked(key);
                result.Add(usable && !result.Contains(key) ? key : Empty);
            }
            if (never && slots > 0 && belt != null)
            {
                var first = belt.Types.Find(t => t != null && t.StartsUnlocked);
                if (first != null)
                {
                    result[0] = first.Key;
                }
            }
            return result;
        }

        /// <summary>
        /// What slot <paramref name="index"/> holds after one press at the campfire: the next unlocked
        /// type in the belt's order that no other slot holds, then empty, then round again.
        /// </summary>
        public static string Cycle(PotionBeltSO belt, IList<string> loadout, int index, Func<string, bool> unlocked)
        {
            if (belt == null || loadout == null || index < 0 || index >= loadout.Count)
            {
                return Empty;
            }
            var order = new List<string>();
            foreach (var type in belt.Types)
            {
                if (type != null && unlocked != null && unlocked(type.Key))
                {
                    order.Add(type.Key);
                }
            }
            order.Add(Empty);

            int at = order.IndexOf(loadout[index] ?? Empty);
            for (int step = 1; step <= order.Count; step++)
            {
                string next = order[(Math.Max(at, 0) + step) % order.Count];
                bool taken = false;
                for (int other = 0; other < loadout.Count; other++)
                {
                    if (other != index && next != Empty && loadout[other] == next)
                    {
                        taken = true;
                    }
                }
                if (!taken)
                {
                    return next;
                }
            }
            return Empty;
        }

        /// <summary>
        /// What the bags should hold of each type's potion as a run starts: a slotted type's capacity,
        /// and none of an unslotted one. The belt is what the party carries; potions are found nowhere else.
        /// </summary>
        public static List<KeyValuePair<ItemSO, int>> RunStartStock(PotionBeltSO belt, IList<string> loadout, Func<BeltPotionType, int> capacity)
        {
            var stock = new List<KeyValuePair<ItemSO, int>>();
            if (belt == null)
            {
                return stock;
            }
            foreach (var type in belt.Types)
            {
                if (type == null || type.Potion == null)
                {
                    continue;
                }
                bool slotted = loadout != null && loadout.Contains(type.Key);
                stock.Add(new KeyValuePair<ItemSO, int>(type.Potion, slotted && capacity != null ? capacity(type) : 0));
            }
            return stock;
        }
    }
}
