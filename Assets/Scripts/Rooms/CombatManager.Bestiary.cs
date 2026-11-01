using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Assets.Scripts.Events;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Rooms
{
    /// <summary>The fight, by job: what the player learns by fighting.</summary>
    public partial class CombatManager
    {
        // ============================================================
        //  BESTIARY (what the player learns by fighting)
        // ============================================================
        //
        // Knowledge is recorded from the combat path, at the moment the player sees the thing, so
        // an Inspect window and the hub bestiary only ever show what was actually observed. All of
        // these use MetaProgressManager.Instance (auto-creates + loads Meta.json) for the same
        // reason ExecuteCastAction does: the manager may not exist yet mid-combat. Each mutator is
        // idempotent and persists only on a real change, so calling them per hit is cheap.

        /// <summary>The bestiary key for a combat unit, or null when it is not a keyed enemy.</summary>
        private static string BestiaryKeyOf(ICombatUnit unit)
        {
            var enemy = unit as Enemy;
            return enemy != null && enemy.Definition != null ? enemy.Definition.SaveKey : null;
        }

        /// <summary>Meeting an enemy is what puts it in the bestiary at all.</summary>
        private static void RecordEnemySeen(Enemy enemy)
        {
            if (enemy != null && enemy.Definition != null)
            {
                MetaProgressManager.Instance.MarkEnemySeen(enemy.Definition.SaveKey);
            }
        }

        /// <summary>
        /// A hit of this element landed on the enemy, so its resistance to that element is now
        /// observed. Recorded for <b>every</b> type including Normal - "Lightning does nothing
        /// special to it" is a real finding, and gating on a non-Normal classification (as the
        /// original plan did) would leave every neutral element permanently unreadable.
        /// </summary>
        private static void RecordDamageObserved(ICombatUnit target, DamageType type)
        {
            string key = BestiaryKeyOf(target);
            if (key != null)
            {
                MetaProgressManager.Instance.MarkResistanceObserved(key, type);
            }
        }

        /// <summary>
        /// Watching an enemy actually throw a spell is what names it on that enemy's Bestiary page.
        ///
        /// <para>This carries what Draw used to. The spell list was the Draw list, and pulling from
        /// it was both the acquisition and the reveal; with Draw gone the list is purely the
        /// monster's own repertoire, so the only honest way to learn it is to be on the receiving
        /// end. Recorded per enemy, not globally - seeing a Cinder Imp throw Fireball says nothing
        /// about what the Dragon has.</para>
        /// </summary>
        private static void RecordEnemySpellObserved(Enemy enemy, MagicSO magic)
        {
            if (enemy == null || enemy.Definition == null || magic == null)
            {
                return;
            }
            MetaProgressManager.Instance.MarkEnemySpellObserved(enemy.Definition.SaveKey, magic.Key);
        }

        /// <summary>Watching an enemy swing reveals the element it swings with.</summary>
        private static void RecordAttackTypeObserved(ICombatUnit attacker)
        {
            string key = BestiaryKeyOf(attacker);
            if (key != null)
            {
                MetaProgressManager.Instance.MarkAttackTypeObserved(key);
            }
        }

        /// <summary>Every element the cast actually delivered, against every enemy it landed on.</summary>
        private static void RecordCastDamageObserved(SpellcastAction castAction)
        {
            if (castAction == null || castAction.Magic == null ||
                castAction.Magic.Effects == null || castAction.Targets == null)
            {
                return;
            }

            foreach (var effect in castAction.Magic.Effects)
            {
                // Skip effects gated behind an upgrade level - abilities have none since the Forge
                // stopped upgrading them, so those never fired and taught the player nothing.
                if (effect == null || effect.EffectType != SpellEffectType.Damage ||
                    effect.UnlockLevel > 0)
                {
                    continue;
                }

                foreach (var target in castAction.Targets)
                {
                    RecordDamageObserved(target, effect.DamageType);
                }
            }
        }

        private static void RecordEnemyKilled(Enemy enemy)
        {
            if (enemy != null && enemy.Definition != null)
            {
                MetaProgressManager.Instance.MarkEnemyKilled(enemy.Definition.SaveKey);
            }
        }

        private static void RecordLootObserved(Enemy enemy, ItemSO loot)
        {
            if (enemy != null && enemy.Definition != null && loot != null)
            {
                MetaProgressManager.Instance.MarkLootObserved(enemy.Definition.SaveKey, loot.Key);
            }
        }
    }
}
