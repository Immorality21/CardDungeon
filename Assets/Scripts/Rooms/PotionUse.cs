using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What a potion does, shared by the fight's Item command and the walking party's potion window
    /// (owner, 2026-11-01), so a potion is worth the same in both.
    /// </summary>
    public static class PotionUse
    {
        /// <summary>The careful drinker's line: a hero this low may drink from the reserve.</summary>
        public const float EmergencyHealth = 0.2f;

        /// <summary>A careful drinker's rule, shared by the balance model and the bot: a floor's share is
        /// what is left of the belt over the floors still ahead, so it lasts to the finale. The rest is
        /// the reserve, which <see cref="MayDrink"/> opens only to save a hero.</summary>
        public static int ReserveFor(int potionsLeft, int floorsAhead)
        {
            return potionsLeft - Mathf.CeilToInt((float)potionsLeft / Mathf.Max(1, floorsAhead));
        }

        public static bool MayDrink(int potionsLeft, int reserve, float healthShare)
        {
            return potionsLeft > reserve || (potionsLeft > 0 && healthShare <= EmergencyHealth);
        }

        /// <summary>A cure has nothing to do outside a fight: every status it clears ends with the fight.</summary>
        public static bool WorksOutsideCombat(ItemSO item)
        {
            return item != null && item.Category == ItemCategory.Consumable
                && item.ConsumableEffect != ConsumableEffectType.CureStatus;
        }

        public static bool Heals(ItemSO item)
        {
            return item.ConsumableEffect == ConsumableEffectType.RestoreHealth
                || item.ConsumableEffect == ConsumableEffectType.HealAndCharge;
        }

        public static bool RestoresCharges(ItemSO item)
        {
            return item.ConsumableEffect == ConsumableEffectType.RestoreCharge
                || item.ConsumableEffect == ConsumableEffectType.HealAndCharge;
        }

        /// <summary>The heal scales off the target's bar: a potion is worth what it is worth to whoever drinks it.</summary>
        public static int HealAmount(ItemSO item, ICombatUnit target)
        {
            int max = target.GetEffectiveStat(StatType.MaxHealth);
            return RunFear.Current.ScaleHealing(target, item.HealAmountFor(max, PotionBeltState.HealBonusFor(item)));
        }

        public static int ChargesOf(ItemSO item)
        {
            return Mathf.Max(1, item.ConsumableCharges + PotionBeltState.ChargeBonusFor(item));
        }

        /// <summary>Whether drinking does anything: a fallen hero drinks nothing, and a full bar and
        /// full charges would only waste the potion.</summary>
        public static bool Helps(ItemSO item, bool alive, int health, int maxHealth, bool chargesSpent)
        {
            return alive && ((Heals(item) && health < maxHealth) || (RestoresCharges(item) && chargesSpent));
        }
    }
}
