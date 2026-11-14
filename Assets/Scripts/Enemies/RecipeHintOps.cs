using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Enemies
{
    /// <summary>How a recipe can come into the bags.</summary>
    public enum RecipeSourceKind
    {
        /// <summary>An enemy's drop table (or its salvage, for a machine taken apart).</summary>
        EnemyDrop,
        /// <summary>A floor's clear (<see cref="LevelDefinitionSO.RecipeTable"/>).</summary>
        FloorClear,
        /// <summary>A room event's pay-out on a floor (<see cref="LevelDefinitionSO.EventRecipes"/>, or an
        /// event outcome's loot or side-room chest).</summary>
        RoomEvent,
        /// <summary>A quest's reward, collected at the Tavern.</summary>
        Quest
    }

    /// <summary>One place a recipe can be found: who drops it, or which floor of which level pays it.</summary>
    public class RecipeSource
    {
        public RecipeSourceKind Kind;
        public EnemySO Enemy;
        public RunDefinitionSO Run;
        public RunLevelEntry Floor;
    }

    /// <summary>
    /// The Hall of Knowledge's hint on a recipe not yet found (owner, 2026-10-29; built 2026-11-14):
    /// once the Hall is upgraded, a "???" recipe row says <b>where</b> it comes from. A level granting
    /// <i>information</i>, which is the phase-6 rule for building levels (<c>docs/plans/HUB.md</c> §7).
    ///
    /// <para>Pure. <see cref="Find"/> reads the authored data (enemy drop tables, every floor's recipe
    /// table and event recipes, over the campaign's runs); <see cref="Lines"/> words it through what the
    /// player already knows, so the hint never names an enemy they have not met or a level they have
    /// not reached: those read "a foe not yet met" and "a level not yet reached". The hint points the
    /// way and leaves the rest to discovery (the mystery-over-hand-holding rule).</para>
    /// </summary>
    public static class RecipeHintOps
    {
        /// <summary>The level of the Hall (<c>HubService.Bestiary</c>) at which the hint shows.</summary>
        public const int HintLevel = 2;

        /// <summary>
        /// Every place <paramref name="recipe"/> can be found. Enemy drops come first, one source per
        /// enemy and level it fights in (or one with no level for an enemy no floor spawns); then floor
        /// clears and room events, one per floor. In campaign order, duplicates dropped.
        /// </summary>
        public static List<RecipeSource> Find(ItemSO recipe, IEnumerable<RunDefinitionSO> runs, IEnumerable<EnemySO> enemies,
            IEnumerable<Hub.Quests.QuestSO> quests = null)
        {
            var sources = new List<RecipeSource>();
            if (recipe == null)
            {
                return sources;
            }
            var runList = runs != null ? new List<RunDefinitionSO>(runs) : new List<RunDefinitionSO>();

            if (enemies != null)
            {
                var seen = new List<EnemySO>();
                foreach (var enemy in enemies)
                {
                    if (enemy == null || seen.Contains(enemy) || !Drops(enemy, recipe))
                    {
                        continue;
                    }
                    seen.Add(enemy);
                    bool placed = false;
                    foreach (var run in runList)
                    {
                        if (RunHasEnemy(run, enemy))
                        {
                            sources.Add(new RecipeSource { Kind = RecipeSourceKind.EnemyDrop, Enemy = enemy, Run = run });
                            placed = true;
                        }
                    }
                    if (!placed)
                    {
                        sources.Add(new RecipeSource { Kind = RecipeSourceKind.EnemyDrop, Enemy = enemy });
                    }
                }
            }

            foreach (var run in runList)
            {
                if (run == null || run.Levels == null)
                {
                    continue;
                }
                foreach (var floor in run.Levels)
                {
                    var template = floor != null ? floor.LevelTemplate : null;
                    if (template == null)
                    {
                        continue;
                    }
                    if (TableHas(template.RecipeTable, recipe))
                    {
                        sources.Add(new RecipeSource { Kind = RecipeSourceKind.FloorClear, Run = run, Floor = floor });
                    }
                    // A floor authored without room events never pays an event's recipe.
                    if (floor.AllowRoomEvents
                        && ((template.EventRecipes != null && template.EventRecipes.Contains(recipe)) || EventsPay(template, recipe)))
                    {
                        sources.Add(new RecipeSource { Kind = RecipeSourceKind.RoomEvent, Run = run, Floor = floor });
                    }
                }
            }
            if (quests != null)
            {
                foreach (var quest in quests)
                {
                    if (quest != null && quest.RewardItem == recipe)
                    {
                        sources.Add(new RecipeSource { Kind = RecipeSourceKind.Quest });
                        break;
                    }
                }
            }
            return sources;
        }

        /// <summary>Whether a room event listed on this floor can hand the recipe over: an outcome's loot,
        /// or the chest in the side room an outcome opens.</summary>
        private static bool EventsPay(LevelDefinitionSO template, ItemSO recipe)
        {
            if (template.RoomEvents == null)
            {
                return false;
            }
            foreach (var listed in template.RoomEvents)
            {
                if (listed?.Event?.Options == null)
                {
                    continue;
                }
                foreach (var option in listed.Event.Options)
                {
                    foreach (var pool in new[] { option?.Success, option?.Failure })
                    {
                        if (pool == null)
                        {
                            continue;
                        }
                        foreach (var outcome in pool)
                        {
                            if (outcome == null)
                            {
                                continue;
                            }
                            if ((outcome.LootTable != null && outcome.LootTable.Contains(recipe))
                                || (outcome.SideRoom?.ChestLoot != null && outcome.SideRoom.ChestLoot.Contains(recipe)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The hint as lines for the page, worded through what the player knows:
        /// <paramref name="enemyMet"/> (a Bestiary entry exists) and <paramref name="runReached"/> (the
        /// level is open or cleared). One line per enemy, naming at most two of its levels; unmet
        /// enemies with the same levels fold into one line (unmet ones in different levels do not). At most
        /// <paramref name="maxLines"/>, the rest summed up in a last "and elsewhere" line.
        /// </summary>
        public static List<string> Lines(List<RecipeSource> sources, Func<EnemySO, bool> enemyMet, Func<RunDefinitionSO, bool> runReached, int maxLines = 4)
        {
            var lines = new List<string>();
            if (sources == null)
            {
                return lines;
            }

            // One line per enemy, naming at most two of the levels it fights in: the Cinder Imp turns
            // up nearly everywhere, and a line per level pushed every other source off the page.
            var enemies = new List<EnemySO>();
            foreach (var source in sources)
            {
                if (source.Kind == RecipeSourceKind.EnemyDrop && source.Enemy != null && !enemies.Contains(source.Enemy))
                {
                    enemies.Add(source.Enemy);
                }
            }
            foreach (var enemy in enemies)
            {
                var levels = new List<string>();
                foreach (var source in sources)
                {
                    if (source.Kind == RecipeSourceKind.EnemyDrop && source.Enemy == enemy && source.Run != null)
                    {
                        string level = LevelName(source.Run, runReached);
                        if (!levels.Contains(level))
                        {
                            levels.Add(level);
                        }
                    }
                }
                // Levels the player knows read first; "a level not yet reached" goes last, once.
                string unknown = Loc.Get("bestiary.recipe.hint.unknown-level");
                if (levels.Remove(unknown))
                {
                    levels.Add(unknown);
                }
                AddOnce(lines, EnemyLine(enemy, levels, enemyMet));
            }

            foreach (var source in sources)
            {
                if (source.Kind != RecipeSourceKind.EnemyDrop)
                {
                    AddOnce(lines, LineFor(source, runReached));
                }
            }

            if (lines.Count > maxLines)
            {
                lines.RemoveRange(maxLines - 1, lines.Count - (maxLines - 1));
                lines.Add(Loc.Get("bestiary.recipe.hint.elsewhere"));
            }
            return lines;
        }

        private static void AddOnce(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private static string LevelName(RunDefinitionSO run, Func<RunDefinitionSO, bool> runReached)
        {
            return runReached != null && runReached(run)
                ? CampaignOps.DisplayNameOf(run)
                : Loc.Get("bestiary.recipe.hint.unknown-level");
        }

        private static string EnemyLine(EnemySO enemy, List<string> levels, Func<EnemySO, bool> enemyMet)
        {
            bool met = enemyMet != null && enemyMet(enemy);
            string who = met ? EnemyName(enemy) : Loc.Get("bestiary.recipe.hint.unknown-foe");
            if (levels.Count == 0)
            {
                return Loc.Format("bestiary.recipe.hint.dropped-by", ("enemy", who));
            }
            string separator = Loc.Get("bestiary.recipe.hint.separator");
            string where = levels.Count <= 2
                ? string.Join(separator, levels)
                : Loc.Format("bestiary.recipe.hint.levels-more", ("levels", string.Join(separator, levels.GetRange(0, 2))));
            return Loc.Format("bestiary.recipe.hint.dropped-by-in", ("enemy", who), ("level", where));
        }

        private static string LineFor(RecipeSource source, Func<RunDefinitionSO, bool> runReached)
        {
            string level = source.Run == null ? Loc.Get("bestiary.recipe.hint.unknown-level") : LevelName(source.Run, runReached);
            switch (source.Kind)
            {
                case RecipeSourceKind.FloorClear:
                    return Loc.Format("bestiary.recipe.hint.floor-clear", ("floor", FloorName(source, runReached)), ("level", level));
                case RecipeSourceKind.RoomEvent:
                    return Loc.Format("bestiary.recipe.hint.room-event", ("floor", FloorName(source, runReached)), ("level", level));
                case RecipeSourceKind.Quest:
                    return Loc.Get("bestiary.recipe.hint.quest");
            }
            return null;
        }

        private static string FloorName(RecipeSource source, Func<RunDefinitionSO, bool> runReached)
        {
            bool reached = runReached != null && source.Run != null && runReached(source.Run);
            string name = source.Floor != null ? source.Floor.LevelName : null;
            if (!reached || string.IsNullOrEmpty(name))
            {
                int index = source.Run != null && source.Run.Levels != null ? source.Run.Levels.IndexOf(source.Floor) : -1;
                return index >= 0
                    ? Loc.Format("bestiary.recipe.hint.floor-number", ("number", index + 1))
                    : Loc.Get("bestiary.recipe.hint.a-floor");
            }
            return name;
        }

        private static string EnemyName(EnemySO enemy)
        {
            return enemy == null ? string.Empty : string.IsNullOrEmpty(enemy.DisplayName) ? enemy.name : enemy.DisplayName;
        }

        /// <summary>
        /// Whether the hint may name a level: the story map shows it open, in progress or cleared. A
        /// locked or hidden one (a secret level whose hero the player does not own) is "a level not
        /// yet reached". <paramref name="ownedHeroKeys"/> is the real roster, as the map passes it.
        /// </summary>
        public static bool IsReached(CampaignNodeEntry node, ICollection<string> completedRunKeys, ICollection<string> ownedHeroKeys)
        {
            if (node == null)
            {
                return false;
            }
            var status = CampaignOps.GetState(node, completedRunKeys, null, ownedHeroKeys).Status;
            return status == CampaignNodeStatus.Available
                || status == CampaignNodeStatus.InProgress
                || status == CampaignNodeStatus.Completed;
        }

        /// <summary>Whether this enemy can drop the recipe, killed or taken apart.</summary>
        public static bool Drops(EnemySO enemy, ItemSO recipe)
        {
            return enemy != null && (TableHas(enemy.LootTable, recipe) || TableHas(enemy.SalvageTable, recipe));
        }

        private static bool TableHas(List<LootDrop> table, ItemSO item)
        {
            if (table == null)
            {
                return false;
            }
            foreach (var drop in table)
            {
                if (drop != null && drop.Item == item)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether any floor of the run can put this enemy in front of the party: its rooms'
        /// spawn tables (or a hand-laid layout's), its boss and the boss's adds.</summary>
        public static bool RunHasEnemy(RunDefinitionSO run, EnemySO enemy)
        {
            if (run == null || run.Levels == null || enemy == null)
            {
                return false;
            }
            foreach (var floor in run.Levels)
            {
                if (floor != null && FloorHasEnemy(floor, enemy))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool FloorHasEnemy(RunLevelEntry floor, EnemySO enemy)
        {
            if (floor.BossEnemy == enemy)
            {
                return true;
            }
            foreach (var add in floor.EnumerateBossAdds())
            {
                if (add == enemy)
                {
                    return true;
                }
            }

            var rooms = new List<Rooms.RoomSO>();
            if (floor.ManualLayout != null && floor.ManualLayout.Rooms != null)
            {
                foreach (var room in floor.ManualLayout.Rooms)
                {
                    if (room == null)
                    {
                        continue;
                    }
                    if (room.RoomTemplate != null)
                    {
                        rooms.Add(room.RoomTemplate);
                    }
                    if (SpawnsHave(room.EnemySpawnOverride, enemy))
                    {
                        return true;
                    }
                }
            }
            else if (floor.LevelTemplate != null && floor.LevelTemplate.RoomPool != null)
            {
                rooms.AddRange(floor.LevelTemplate.RoomPool);
            }

            foreach (var room in rooms)
            {
                if (room != null && SpawnsHave(room.EnemySpawnTable, enemy))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SpawnsHave(List<EnemySpawnEntry> table, EnemySO enemy)
        {
            if (table == null)
            {
                return false;
            }
            foreach (var entry in table)
            {
                if (entry != null && entry.Enemy == enemy)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
