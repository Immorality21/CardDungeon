using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>
    /// One hero's turn, in the order the balance model's Adaptive policy takes it
    /// (<c>EncounterSimulator</c>), so a gap between the bot and the model is the model's and not a
    /// different way of playing: a heal for whoever is low, then an Ultra the moment it is ready, then
    /// a damaging ability while charges last, then an attack on the weakest foe.
    ///
    /// <para><b>Not played:</b> summons and non-damaging abilities (buffs, debuffs, shields).</para>
    /// </summary>
    public static class BotCombat
    {
        /// <summary>The model's <c>HealThreshold</c>.</summary>
        public const float HealThreshold = 0.45f;

        /// <param name="potionReserve">Potions kept for the floors ahead, as the model keeps them.</param>
        public static void Act(ICombatUnit unit, Party party, BotFight fight, int potionReserve)
        {
            var combat = CombatManager.Instance;
            var enemies = combat.GetTargetableEnemies();
            var weakest = enemies.OrderBy(e => e.Stats.Health).FirstOrDefault();
            if (!(unit is Hero hero))
            {
                combat.SubmitAttackAction(weakest);
                return;
            }

            fight.HeroTurns++;
            bool silenced = combat.BuffTracker != null && combat.BuffTracker.HasStatusEffect(hero, BuffType.Silenced);
            var slots = DungeonManager.Instance.MagicState?.GetSlots(hero.HeroKey) ?? new List<MagicSlot>();

            var wounded = party.Heroes.Where(h => h != null && h.IsAlive)
                .OrderBy(HealthShare).FirstOrDefault();
            if (wounded != null && HealthShare(wounded) <= HealThreshold)
            {
                var potion = FindPotion();
                if (potion != null && PotionUse.MayDrink(
                    InventoryManager.Instance.GetConsumableQuantity(potion.Key), potionReserve, HealthShare(wounded)))
                {
                    fight.PotionsUsed++;
                    combat.SubmitUseItemAction(potion, wounded);
                    return;
                }
                if (!silenced && TryCast(hero, slots, SpellEffectType.Heal, wounded, enemies, fight))
                {
                    return;
                }
            }

            var ultra = combat.KnownUltras(hero).FirstOrDefault(u => combat.CanUseUltra(hero, u));
            if (ultra != null)
            {
                fight.UltrasUsed++;
                combat.SubmitUltra(ultra, combat.UltraTargets(ultra, hero).FirstOrDefault());
                return;
            }

            if (!silenced && TryCast(hero, slots, SpellEffectType.Damage, weakest, enemies, fight))
            {
                return;
            }
            combat.SubmitAttackAction(weakest);
        }

        public static float HealthShare(Hero hero)
        {
            int max = hero.GetEffectiveMaxHealth();
            return max > 0 ? (float)hero.Stats.Health / max : 0f;
        }

        private static bool TryCast(Hero hero, List<MagicSlot> slots, SpellEffectType effect, ICombatUnit target,
            List<ICombatUnit> enemies, BotFight fight)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.CanCast || slot.Magic.Effects == null
                    || !slot.Magic.Effects.Exists(e => e != null && e.EffectType == effect))
                {
                    continue;
                }
                var targets = TargetsOf(slot.Magic, hero, target, enemies);
                if (targets == null)
                {
                    continue;
                }
                fight.Casts++;
                CombatManager.Instance.SubmitCastAction(slot.Magic, i, hero, targets);
                return true;
            }
            return false;
        }

        private static List<ICombatUnit> TargetsOf(MagicSO magic, Hero hero, ICombatUnit target, List<ICombatUnit> enemies)
        {
            switch (magic.TargetType)
            {
                case MagicTargetType.AllEnemies:
                    return enemies;
                case MagicTargetType.Self:
                    return new List<ICombatUnit> { hero };
                case MagicTargetType.AllAllies:
                    return CombatManager.Instance.HeroSideUnits().Where(u => u.IsAlive).ToList();
                case MagicTargetType.FallenAlly:
                    return null;
                default:
                    return new List<ICombatUnit> { target };
            }
        }

        public static ItemSO FindPotion()
        {
            var inventory = InventoryManager.Instance;
            foreach (var entry in inventory.GetConsumables())
            {
                var item = inventory.GetItemSO(entry.ItemKey);
                if (item != null && item.ConsumableEffect == ConsumableEffectType.RestoreHealth)
                {
                    return item;
                }
            }
            return null;
        }
    }
}
