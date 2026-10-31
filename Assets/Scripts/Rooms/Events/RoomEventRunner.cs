using ImmoralityGaming.Localization;
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
using Assets.Scripts.UnitStats;
using UnityEngine;
using Assets.Scripts.Events;

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

        private static readonly StatType[] AllStats =
        {
            StatType.Strength, StatType.Endurance, StatType.Agility,
            StatType.Intelligence, StatType.Spirit, StatType.Luck
        };

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
            ApplyCure(outcome, afflictions, report);
            ApplyAllStats(outcome, party, actingHero, afflictions, report);
            ApplyGold(outcome, report);
            ApplyXp(outcome, party, report);
            ApplyCharges(outcome, report);
            ApplyRunStatLoss(outcome, actingHero, afflictions, report);
            ApplyLoot(outcome, report);
            ApplyGuaranteedLoot(outcome, room, report);
            ApplyLevelMaterial(outcome, report);
            ApplyPoolRecipe(outcome, report);
            ApplyConsumableLoss(outcome, report);
            ApplyAwakenedEnemies(outcome, room, report);
            ApplyJoin(outcome, report);

            return report;
        }

        /// <summary>
        /// Takes what choosing <paramref name="option"/> costs, ahead of its outcome: the floor's gold
        /// and the item that opens it. One line per thing paid.
        /// </summary>
        public List<string> PayFor(RoomEventOption option)
        {
            var lines = new List<string>();
            if (option == null)
            {
                return lines;
            }

            if (option.GoldCost > 0 && MetaProgressManager.HasInstance
                && MetaProgressManager.Instance.SpendPendingGold(option.GoldCost, EconomySource.RoomEvent))
            {
                lines.Add(Loc.Format("event-text.report.paid-gold", ("amount", option.GoldCost)));
            }
            if (option.RequiredItem != null && InventoryManager.HasInstance
                && InventoryManager.Instance.RemoveOne(option.RequiredItem, ItemRemovalReason.Used))
            {
                lines.Add(Loc.Format("event-text.report.used-item", ("item", option.RequiredItem.DisplayName)));
            }
            return lines;
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
                    ? Loc.Format("event-text.affliction.minus", ("hero", hero.DisplayName), ("buff", effect.BuffType), ("amount", signed))
                    : Loc.Format("event-text.affliction.plus", ("hero", hero.DisplayName), ("buff", effect.BuffType), ("amount", signed)));
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

                // Read the amounts, never the text: a heal's Text is a bare number, so sniffing for a
                // "+" reported every event heal as damage (revisit playtest finding 7).
                string line = DescribeEntry(entry);
                if (line == null)
                {
                    continue;
                }
                report.Lines.Add(line);

                bool isHeal = entry.Healed > 0;
                ShowFloatingText(entry.Target, isHeal ? Loc.Format("event-text.float.heal", ("amount", entry.Healed)) : entry.Text, isHeal ? HealColor : DamageColor);
            }
        }

        /// <summary>
        /// One report line for an effect entry, from its amounts: a heal says how much it restored
        /// (or that the hero was already whole), a hit how much it took. Null for an entry that is
        /// neither, so a status label is never worded as damage.
        /// </summary>
        public static string DescribeEntry(EffectEntry entry)
        {
            if (entry == null || entry.Target == null)
            {
                return null;
            }
            if (entry.Healed > 0)
            {
                return Loc.Format("event-text.report.healed", ("unit", entry.Target.DisplayName), ("amount", entry.Healed));
            }
            if (entry.Impact > 0)
            {
                return Loc.Format("event-text.report.damaged", ("unit", entry.Target.DisplayName), ("amount", entry.Impact));
            }
            if (entry.Text == "0")
            {
                return Loc.Format("event-text.report.already-whole", ("unit", entry.Target.DisplayName));
            }
            return null;
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
                    HealthOps.Set(target, 1, new HealthSource(null, HealthCause.RoomEvent), null);
                    report.Lines.Add(Loc.Format("event-text.report.barely-standing", ("unit", target.DisplayName)));
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

            MetaProgressManager.Instance.AddPendingGold(outcome.Gold, EconomySource.RoomEvent);
            report.Lines.Add(Loc.Format("event-text.report.gold", ("amount", outcome.Gold)));
        }

        private void ApplyXp(RoomEventOutcome outcome, Party party, RoomEventOutcomeReport report)
        {
            if (outcome.XpPerHero <= 0 || party == null)
            {
                return;
            }

            party.DistributeXp(outcome.XpPerHero * party.Heroes.Count, EconomySource.RoomEvent);
            report.Lines.Add(Loc.Format("event-text.report.xp", ("amount", outcome.XpPerHero)));
        }

        private void ApplyCharges(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (outcome.ChargesRestored <= 0 || !DungeonManager.HasInstance)
            {
                return;
            }

            int restored = DungeonManager.Instance.MagicState.RestoreEveryAbility(outcome.ChargesRestored);
            report.Lines.Add(restored > 0
                ? Loc.Format("event-text.report.charges", ("amount", restored))
                : Loc.Get("event-text.report.charges-full"));
        }

        private void ApplyCure(RoomEventOutcome outcome, LevelAfflictionTracker afflictions, RoomEventOutcomeReport report)
        {
            if (!outcome.CuresAfflictions || afflictions == null)
            {
                return;
            }

            report.Lines.Add(afflictions.CureLevelCurses() > 0
                ? Loc.Get("event-text.report.cured")
                : Loc.Get("event-text.report.nothing-to-cure"));
        }

        /// <summary>Every stat a share higher for the level, worked out once and held as flat amounts.</summary>
        private void ApplyAllStats(
            RoomEventOutcome outcome,
            Party party,
            Hero actingHero,
            LevelAfflictionTracker afflictions,
            RoomEventOutcomeReport report)
        {
            if (outcome.AllStatsPercent <= 0f || afflictions == null)
            {
                return;
            }

            foreach (var target in ResolveTargets(outcome.Targets, party, actingHero))
            {
                var hero = target as Hero;
                if (hero == null)
                {
                    continue;
                }

                foreach (var stat in AllStats)
                {
                    int gain = LevelAfflictionTracker.PercentLoss(hero.GetEffectiveStat(stat), outcome.AllStatsPercent);
                    if (gain > 0 && System.Enum.TryParse(stat.ToString(), out BuffType buff))
                    {
                        afflictions.Add(hero.HeroKey, buff, gain);
                    }
                }
                report.Lines.Add(Loc.Format("event-text.report.all-stats", ("hero", hero.DisplayName), ("percent", outcome.AllStatsPercent)));
            }
        }

        private void ApplyLevelMaterial(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (!outcome.PaysLevelMaterial || !InventoryManager.HasInstance || !DungeonManager.HasInstance)
            {
                return;
            }

            var level = DungeonManager.Instance.CurrentLevel;
            var options = level != null && level.MaterialTable != null
                ? level.MaterialTable.FindAll(d => d != null && d.Item != null && !RunFear.Current.Withholds(d.Item))
                : new List<LootDrop>();
            if (options.Count == 0)
            {
                report.Lines.Add(Loc.Get("event-text.report.found-nothing"));
                return;
            }

            var drop = options[Random.Range(0, options.Count)];
            int quantity = Random.Range(drop.MinQuantity, drop.MaxQuantity + 1);
            InventoryManager.Instance.AddItem(drop.Item, quantity, EconomySource.RoomEvent);
            report.Lines.Add(Loc.Format("event-text.report.found", ("item", drop.Item.DisplayName)));
        }

        private void ApplyPoolRecipe(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (!outcome.PaysPoolRecipe || !DungeonManager.HasInstance)
            {
                return;
            }

            var item = GrantOneOf(DungeonManager.Instance.OpenEventRecipes());
            report.Lines.Add(item != null
                ? Loc.Format("event-text.report.found", ("item", item.DisplayName))
                : Loc.Get("event-text.report.found-nothing"));
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
                // A recipe already known teaches nothing, so an event does not hand it over again.
                if (item == null || InventoryManager.Instance.AlreadyKnown(item))
                {
                    continue;
                }

                if (!LootRoller.ShouldDrop(item, DungeonManager.RunLevelIndex, Random.Range(0f, 1f)))
                {
                    continue;
                }

                InventoryManager.Instance.AddItem(item, 1, EconomySource.RoomEvent);
                report.Lines.Add(Loc.Format("event-text.report.found", ("item", item.DisplayName)));
                anyDropped = true;
            }

            if (!anyDropped)
            {
                report.Lines.Add(Loc.Get("event-text.report.found-nothing"));
            }
        }

        /// <summary>
        /// The one item an outcome always pays. Behind a fight it waits on the room
        /// (<see cref="Room.PendingEventLoot"/>); a mandatory side room hands it over when it is built.
        /// </summary>
        private void ApplyGuaranteedLoot(RoomEventOutcome outcome, Room room, RoomEventOutcomeReport report)
        {
            if (outcome.LootOneOf == null || outcome.LootOneOf.Count == 0 || room == null)
            {
                return;
            }
            if (outcome.SideRoom != null && outcome.SideRoom.IsSet && outcome.SideRoom.Mandatory)
            {
                return;
            }
            if (outcome.AwakenedEnemies != null && outcome.AwakenedEnemies.Count > 0)
            {
                room.PendingEventLoot = outcome.LootOneOf;
                return;
            }

            var item = GrantOneOf(outcome.LootOneOf);
            room.EventLootClaimed = true;
            if (item != null)
            {
                report.Lines.Add(Loc.Format("event-text.report.found", ("item", item.DisplayName)));
            }
        }

        /// <summary>Hands over one item of <paramref name="items"/>, never a recipe already known, or
        /// null when there is none to give.</summary>
        public static ItemSO GrantOneOf(List<ItemSO> items)
        {
            if (items == null || !InventoryManager.HasInstance)
            {
                return null;
            }
            var open = items.FindAll(i => i != null && !InventoryManager.Instance.AlreadyKnown(i));
            if (open.Count == 0)
            {
                return null;
            }
            var item = open[Random.Range(0, open.Count)];
            if (item != null)
            {
                InventoryManager.Instance.AddItem(item, 1, EconomySource.RoomEvent);
            }
            return item;
        }

        /// <summary>
        /// A share of the acting hero's stat, gone for the run. It is worked out once, from the stat as
        /// it stands, and held as a flat loss like any affliction.
        /// </summary>
        private void ApplyRunStatLoss(
            RoomEventOutcome outcome,
            Hero actingHero,
            LevelAfflictionTracker afflictions,
            RoomEventOutcomeReport report)
        {
            if (outcome.RunStatLoss == StatType.None || actingHero == null || afflictions == null
                || !System.Enum.TryParse(outcome.RunStatLoss.ToString(), out BuffType buff))
            {
                return;
            }

            int loss = LevelAfflictionTracker.PercentLoss(
                actingHero.GetEffectiveStat(outcome.RunStatLoss), outcome.RunStatLossPercent);
            if (loss <= 0)
            {
                return;
            }

            afflictions.Add(actingHero.HeroKey, buff, -loss, forRun: true);
            if (DungeonManager.HasInstance)
            {
                DungeonManager.Instance.SaveRunAfflictions();
            }
            report.Lines.Add(Loc.Format("event-text.affliction.run",
                ("hero", actingHero.DisplayName), ("stat", StatCatalog.DisplayName(outcome.RunStatLoss)), ("amount", loss)));
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

                if (InventoryManager.Instance.TryConsume(entry.ItemKey, ItemRemovalReason.Lost))
                {
                    var item = InventoryManager.Instance.GetItemSO(entry.ItemKey);
                    string name = item != null ? item.DisplayName : entry.ItemKey;
                    report.Lines.Add(Loc.Format("event-text.report.lost", ("item", name)));
                    return;
                }
            }

            report.Lines.Add(Loc.Get("event-text.report.lost-nothing"));
        }

        private void ApplyJoin(RoomEventOutcome outcome, RoomEventOutcomeReport report)
        {
            if (outcome.JoinsHero == null || !DungeonManager.HasInstance)
            {
                return;
            }

            if (DungeonManager.Instance.JoinParty(outcome.JoinsHero) != null)
            {
                report.Lines.Add(Loc.Format("event-text.report.joins", ("hero", outcome.JoinsHero.DisplayName)));
            }
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
                    ? Loc.Get("event-text.report.awakened.one")
                    : Loc.Format("event-text.report.awakened.many", ("count", spawned)));
            }
        }
    }
}
