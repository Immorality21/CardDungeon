using System.Collections.Generic;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// The potion belt as this save has it: slots, recipes known, each type's capacity and potency,
    /// and what the campfire put in each slot. Reads and writes <see cref="MetaProgressManager"/>; the
    /// rules themselves are <see cref="PotionBeltOps"/>.
    ///
    /// <para><b>Healing is kept where it always was</b>: its capacity is the healing-potion carry cap in
    /// <c>ResourceMaximums.json</c> (which the balance model reads) and its potency is
    /// <c>MetaProgressSaveData.PotionPotency</c>, so saves from before the slots keep what they bought.
    /// Every other type lives in <c>MetaProgressSaveData.PotionTypes</c>.</para>
    /// </summary>
    public static class PotionBeltState
    {
        public const string HealingKey = "Healing";

        private static MetaProgressManager Meta => MetaProgressManager.Instance;

        public static int Slots(PotionBeltSO belt)
        {
            return belt == null ? 0 : System.Math.Max(belt.StartingSlots, Meta.BeltSlots);
        }

        public static void SetSlots(int slots)
        {
            Meta.SetBeltSlots(slots);
        }

        public static bool Unlocked(BeltPotionType type)
        {
            return type != null && (type.StartsUnlocked || Meta.IsPotionTypeUnlocked(type.Key));
        }

        public static void Unlock(BeltPotionType type)
        {
            Meta.UnlockPotionType(type.Key);
        }

        /// <summary>Whether the recipe is on sale: not known yet, and its run (if any) cleared.</summary>
        public static bool OnSale(BeltPotionType type)
        {
            return type != null && !Unlocked(type)
                && (string.IsNullOrEmpty(type.RequiredRunKey) || Meta.HasCompletedRun(type.RequiredRunKey));
        }

        public static int Capacity(BeltPotionType type)
        {
            if (type == null)
            {
                return 0;
            }
            if (type.Key == HealingKey)
            {
                return PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
            }
            int bought = Meta.PotionTypeCapacity(type.Key);
            return bought > 0 ? bought : type.BaseCapacity;
        }

        public static void SetCapacity(BeltPotionType type, int capacity)
        {
            if (type.Key == HealingKey)
            {
                PartyResourceManager.Instance.SetMax(PartyResourceType.HealingPotion, capacity);
                return;
            }
            Meta.SetPotionTypeCapacity(type.Key, capacity);
        }

        public static int Potency(BeltPotionType type)
        {
            if (type == null)
            {
                return 0;
            }
            return type.Key == HealingKey ? Meta.PotionPotency : Meta.PotionTypePotency(type.Key);
        }

        public static void AddPotency(BeltPotionType type)
        {
            if (type.Key == HealingKey)
            {
                Meta.AddPotionPotency();
                return;
            }
            Meta.AddPotionTypePotency(type.Key);
        }

        /// <summary>What each slot holds, made whole (<see cref="PotionBeltOps.Loadout"/>).</summary>
        public static List<string> Loadout(PotionBeltSO belt)
        {
            return PotionBeltOps.Loadout(belt, Meta.BeltLoadout, Slots(belt), key => Unlocked(belt != null ? belt.Find(key) : null));
        }

        public static void SetLoadout(List<string> loadout)
        {
            Meta.SetBeltLoadout(loadout);
        }

        /// <summary>The share of max health a bought potency adds to <paramref name="potion"/>; 0 outside a save.</summary>
        public static float HealBonusFor(ItemSO potion)
        {
            var type = TypeOf(potion);
            return type != null ? PotionBeltOps.HealBonus(type, Potency(type)) : 0f;
        }

        /// <summary>Ability charges a bought potency adds to <paramref name="potion"/>; 0 outside a save.</summary>
        public static int ChargeBonusFor(ItemSO potion)
        {
            var type = TypeOf(potion);
            return type != null ? PotionBeltOps.ChargeBonus(type, Potency(type)) : 0;
        }

        private static BeltPotionType TypeOf(ItemSO potion)
        {
            if (!MetaProgressManager.HasInstance)
            {
                return null;
            }
            var belt = PotionBeltSO.Load();
            return belt != null ? belt.TypeOf(potion) : null;
        }
    }
}
