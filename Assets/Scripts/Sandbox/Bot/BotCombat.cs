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
    /// <para><b>Summons</b> (2026-11-09): a hero calls one in a boss fight, or when the party is down to
    /// <see cref="SummonEmergency"/> of its health - a summon is once per level, so it is saved for those.
    /// A summoned unit plays <see cref="ActAsSummon"/>.</para>
    ///
    /// <para><b>Not played:</b> the heroes' own non-damaging abilities (buffs, debuffs, shields).</para>
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
            if (unit is SummonUnit summoned)
            {
                ActAsSummon(summoned, enemies, fight);
                return;
            }
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

            if (!silenced && TrySummon(hero, party, fight, weakest))
            {
                return;
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

        /// <summary>The party's health share (fallen heroes count as empty) at which a summon is called
        /// outside a boss fight.</summary>
        public const float SummonEmergency = 0.5f;

        /// <summary>Calls the hero's first usable summon when the fight is a boss's or the party is in
        /// trouble. A summon that needs a target it cannot find (a revive with nobody fallen) is passed over.</summary>
        private static bool TrySummon(Hero hero, Party party, BotFight fight, ICombatUnit weakest)
        {
            var heroes = party.Heroes.Where(h => h != null).ToList();
            float partyShare = heroes.Count > 0 ? heroes.Sum(h => h.IsAlive ? HealthShare(h) : 0f) / heroes.Count : 1f;
            if (!fight.Boss && partyShare > SummonEmergency)
            {
                return false;
            }
            var combat = CombatManager.Instance;
            var slots = DungeonManager.Instance.Summons?.GetSummons(hero.HeroKey) ?? new List<SummonSlot>();
            if (fight.SummonsUsed == 0 && !fight.SummonNote.Contains(hero.HeroKey + ":"))
            {
                string known = slots.Count == 0 ? "none" : string.Join(",", slots.Where(s => s != null && s.Summon != null)
                    .Select(s => s.Summon.Key + " " + s.Charges + "/" + s.MaxCharges));
                fight.SummonNote = (fight.SummonNote + " " + hero.HeroKey + ":" + known).Trim();
            }
            foreach (var slot in slots.Where(s => s != null && s.CanUse && SummonOps.CanAfford(s.Summon, hero)))
            {
                ICombatUnit target = null;
                if (SummonOps.NeedsTarget(slot.Summon))
                {
                    switch (slot.Summon.TargetType)
                    {
                        case MagicTargetType.SingleEnemy:
                            target = weakest;
                            break;
                        case MagicTargetType.FallenAlly:
                            target = combat.GetFallenHeroes().FirstOrDefault();
                            break;
                        default:
                            target = heroes.Where(h => h.IsAlive).OrderBy(HealthShare).FirstOrDefault();
                            break;
                    }
                    if (target == null)
                    {
                        continue;
                    }
                }
                fight.SummonsUsed++;
                combat.SubmitSummonAction(slot, target);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A summoned unit's turn, as a plain player would take it: its Signature whenever it is ready;
        /// a move that mends or shields it when it is at <see cref="HealThreshold"/> or under; a move that
        /// does no damage (a buff, a debuff) when it has not used that one in its last 3 turns, about as
        /// long as such effects last; a damaging move, the one that hits everyone when there are several
        /// enemies; else its Attack. A rotating summon (the Blood Idol) only has this turn's rite and its
        /// Signature. Never Dismiss.
        /// </summary>
        public static void ActAsSummon(SummonUnit summon, List<ICombatUnit> enemies, BotFight fight)
        {
            var combat = CombatManager.Instance;
            fight.SummonTurns++;
            var weakest = enemies.OrderBy(e => e.Stats.Health).FirstOrDefault();
            bool silenced = combat.BuffTracker != null && combat.BuffTracker.HasStatusEffect(summon, BuffType.Silenced);
            var definition = summon.Summon;

            if (!silenced && definition != null && summon.Stay != null && summon.Stay.CanUseSignature)
            {
                fight.SummonSignatures++;
                combat.SubmitSummonAbility(definition.Signature, SummonTargets(definition.Signature, summon, weakest, enemies));
                return;
            }

            if (!fight.MoveTurns.TryGetValue(summon, out var lastUsed))
            {
                lastUsed = new Dictionary<string, int>();
                fight.MoveTurns[summon] = lastUsed;
            }
            int turn = summon.Stay != null ? summon.Stay.TurnsTaken : 0;
            float share = summon.Stats.MaxHealth > 0 ? (float)summon.Stats.Health / summon.Stats.MaxHealth : 1f;
            // The rule the balance simulator plays too.
            var pick = SummonMovePolicy.Pick(definition, silenced, share, enemies.Count, turn, lastUsed);
            if (pick != null)
            {
                fight.SummonMoves++;
                combat.SubmitSummonAbility(pick, SummonTargets(pick, summon, weakest, enemies));
                return;
            }

            if (summon.AttackAbility != null)
            {
                combat.SubmitSummonAbility(summon.AttackAbility, SummonTargets(summon.AttackAbility, summon, weakest, enemies));
                return;
            }
            combat.SubmitAttackAction(weakest);
        }

        private static List<ICombatUnit> SummonTargets(MagicSO magic, SummonUnit summon, ICombatUnit weakest, List<ICombatUnit> enemies)
        {
            switch (magic.TargetType)
            {
                case MagicTargetType.AllEnemies:
                    return enemies;
                case MagicTargetType.Self:
                    return new List<ICombatUnit> { summon };
                case MagicTargetType.SingleEnemy:
                    return weakest != null ? new List<ICombatUnit> { weakest } : null;
                default:
                    // Allies, or a rite on the side: let the combat code choose, as it does for the live menu.
                    return null;
            }
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
