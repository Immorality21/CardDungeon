using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// Applies a resolved <see cref="RoomEventOutcome"/> to the live game. The impure half of the
    /// event system: <see cref="RoomEventResolver"/> decides <i>what</i> happens and is unit-tested,
    /// this decides <i>to whom</i> and reaches for the singletons.
    ///
    /// <para>Nothing here is a parallel effect system. Damage and healing run through the same
    /// <see cref="IEffectExecutor"/>s magic uses (with <c>flatPower</c>, because an event's numbers
    /// belong to the event and there is no caster), loot rolls through <c>LootRoller</c>, gold goes
    /// into the run's pending pool, and buffs land in <see cref="LevelAfflictionTracker"/>.</para>
    /// </summary>
    public class RoomEventRunner
    {
        private static readonly Color DamageColor = new Color(1f, 0.35f, 0.35f);
        private static readonly Color HealColor = Color.green;

        private readonly EffectExecutorFactory _factory = new EffectExecutorFactory();

        /// <summary>
        /// Runs one outcome. <paramref name="actingHero"/> is the hero the check was resolved
        /// against - the one who reached in, and the default target for anything that bites back.
        /// </summary>
        public RoomEventOutcomeReport Apply(
            RoomEventOutcome outcome,
            bool succeeded,
            Room room,
            Party party,
            Hero actingHero,
            LevelAfflictionTracker afflictions)
        {
            var report = new RoomEventOutcomeReport
            {
                Succeeded = succeeded,
                Text = outcome != null ? outcome.Text : string.Empty
            };

            if (outcome == null)
            {
                return report;
            }

            ApplyEffects(outcome, party, actingHero, afflictions, report);
            ApplyGold(outcome, report);
            ApplyLoot(outcome, report);
            ApplyConsumableLoss(outcome, report);
            ApplyAwakenedEnemies(outcome, room, report);

            return report;
        }

        // ============================================================
        //  EFFECTS
        // ============================================================

        private void ApplyEffects(
            RoomEventOutcome outcome,
            Party party,
            Hero actingHero,
            LevelAfflictionTracker afflictions,
            RoomEventOutcomeReport report)
        {
            if (outcome.Effects == null || outcome.Effects.Count == 0)
            {
                return;
            }

            var targets = ResolveTargets(outcome.Targets, party, actingHero);
            if (targets.Count == 0)
            {
                return;
            }

            // Buffs and debuffs are recorded rather than executed: there is no combat running to
            // hold them, and a curse picked up in a corridor is meant to be paid for in the fights
            // that follow. The immediate effects need a tracker to satisfy the executors, so they
            // get a scratch one - no buffs are in play outside combat anyway.
            var scratchTracker = new CombatBuffTracker();
            var result = new EffectResult();

            // Afflictions are collected rather than reported inline: the immediate effects only
            // produce their lines after the whole loop, and reporting as we go would print
            // "cursed for the level" above "takes 4 damage" for an outcome authored the other way.
            var afflictionLines = new List<string>();

            foreach (var effect in outcome.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (effect.EffectType == SpellEffectType.Buff || effect.EffectType == SpellEffectType.Debuff)
                {
                    RecordAffliction(effect, targets, afflictions, afflictionLines);
                    continue;
                }

                var executor = _factory.GetExecutor(effect.EffectType);
                executor.Execute(effect, actingHero, targets, scratchTracker, result, flatPower: true);
            }

            ReportEffectEntries(result, report);
            KeepEveryoneStanding(targets, report);
            report.Lines.AddRange(afflictionLines);
        }

        private List<ICombatUnit> ResolveTargets(RoomEventTargets scope, Party party, Hero actingHero)
        {
            var targets = new List<ICombatUnit>();

            if (scope == RoomEventTargets.WholeParty && party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.IsAlive)
                    {
                        targets.Add(hero);
                    }
                }
                return targets;
            }

            if (actingHero != null && actingHero.IsAlive)
            {
                targets.Add(actingHero);
            }
            return targets;
        }

        private void RecordAffliction(
            SpellEffect effect,
            List<ICombatUnit> targets,
            LevelAfflictionTracker afflictions,
            List<string> lines)
        {
            if (afflictions == null || effect.BuffType == BuffType.None)
            {
                return;
            }

            int signed = effect.EffectType == SpellEffectType.Debuff ? -effect.Power : effect.Power;
            if (signed == 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                var hero = target as Hero;
                if (hero == null)
                {
                    continue;
                }

                afflictions.Add(hero.HeroKey, effect.BuffType, signed);
                lines.Add(signed < 0
                    ? $"{hero.DisplayName}: {effect.BuffType} {signed} for the rest of the level."
                    : $"{hero.DisplayName}: {effect.BuffType} +{signed} for the rest of the level.");
            }
        }

        private void ReportEffectEntries(EffectResult result, RoomEventOutcomeReport report)
        {
            foreach (var entry in result.Entries)
            {
                if (entry == null || entry.Target == null)
                {
                    continue;
                }

                bool isHeal = !string.IsNullOrEmpty(entry.Text) && entry.Text.StartsWith("+");
                report.Lines.Add(isHeal
                    ? $"{entry.Target.DisplayName} recovers {entry.Text.TrimStart('+')} health."
                    : $"{entry.Target.DisplayName} takes {entry.Text} damage.");

                ShowFloatingText(entry.Target, entry.Text, isHeal ? HealColor : DamageColor);
            }
        }

        /// <summary>
        /// Event damage never drops a hero below 1 health. Failure costs the party something; it does
        /// not end the run - and there is no combat loop out here to run a death through, so a wipe
        /// in a corridor would strand the game rather than show a death screen.
        /// </summary>
        private void KeepEveryoneStanding(List<ICombatUnit> targets, RoomEventOutcomeReport report)
        {
            foreach (var target in targets)
            {
                if (target.Stats != null && target.Stats.Health < 1)
                {
                    target.Stats.Health = 1;
                    report.Lines.Add($"{target.DisplayName} is barely standing.");
                }
            }
        }

        private void ShowFloatingText(ICombatUnit unit, string text, Color color)
        {
            if (!FloatingTextHandler.HasInstance)
            {
                return;
            }

            // Heroes have no sprite of their own outside combat - the party travels as one blob -
            // so the text goes over the party, wherever that is.
            var anchor = unit.Transform != null ? unit.Transform.position : Vector3.zero;
            FloatingTextHandler.Instance.CreateFloatingText(anchor, text, color);
        }

        // ============================================================
        //  REWARDS AND COSTS
        // ============================================================

        private void ApplyGold(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (outcome.Gold <= 0 || !MetaProgressManager.HasInstance)
            {
                return;
            }

            MetaProgressManager.Instance.AddPendingGold(outcome.Gold);
            report.Lines.Add($"+{outcome.Gold} gold.");
        }

        private void ApplyLoot(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (outcome.LootTable == null || outcome.LootTable.Count == 0 || !InventoryManager.HasInstance)
            {
                return;
            }

            bool anyDropped = false;
            foreach (var item in outcome.LootTable)
            {
                if (item == null)
                {
                    continue;
                }

                if (!LootRoller.ShouldDrop(item, DungeonManager.RunLevelIndex, Random.Range(0f, 1f)))
                {
                    continue;
                }

                InventoryManager.Instance.AddItem(item);
                report.Lines.Add($"Found: {item.DisplayName}.");
                anyDropped = true;
            }

            if (!anyDropped)
            {
                report.Lines.Add("Nothing worth carrying.");
            }
        }

        private void ApplyConsumableLoss(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (!outcome.LoseAConsumable || !InventoryManager.HasInstance)
            {
                return;
            }

            var consumables = InventoryManager.Instance.GetConsumables();
            foreach (var entry in consumables)
            {
                if (entry == null || entry.Quantity <= 0)
                {
                    continue;
                }

                if (InventoryManager.Instance.TryConsume(entry.ItemKey))
                {
                    var item = InventoryManager.Instance.GetItemSO(entry.ItemKey);
                    string name = item != null ? item.DisplayName : entry.ItemKey;
                    report.Lines.Add($"Lost: {name}.");
                    return;
                }
            }

            report.Lines.Add("You had nothing left to lose.");
        }

        private void ApplyAwakenedEnemies(RoomEventOutcome outcome, Room room, RoomEventOutcomeReport report)
        {
            if (outcome.AwakenedEnemies == null || outcome.AwakenedEnemies.Count == 0
                || room == null || !EnemyManager.HasInstance)
            {
                return;
            }

            int spawned = 0;
            foreach (var definition in outcome.AwakenedEnemies)
            {
                if (definition == null)
                {
                    continue;
                }

                var enemy = EnemyManager.Instance.SpawnSingle(definition, room);
                if (enemy != null)
                {
                    spawned++;
                }
            }

            if (spawned > 0)
            {
                // The room is already revealed, but Reveal() is also what enables enemy renderers -
                // a fresh spawn has to go through it or it stands there invisible.
                room.Reveal();
                report.SpawnedEnemies = true;
                report.Lines.Add(spawned == 1
                    ? "Something in here is awake now."
                    : $"{spawned} of them are awake now.");
            }
        }
    }
}
