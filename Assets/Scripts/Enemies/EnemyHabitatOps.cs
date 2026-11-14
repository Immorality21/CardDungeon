using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Enemies
{
    /// <summary>The floors of one level where an enemy can be met.</summary>
    public class EnemyHabitat
    {
        public RunDefinitionSO Run;
        public List<RunLevelEntry> Floors = new List<RunLevelEntry>();
    }

    /// <summary>
    /// Where each monster is found, for the Hall of Knowledge's monster page (backlog 16a, playtest 4
    /// finding 13: it helps with the tavern's hunting quests). Pure: <see cref="Find"/> reads the
    /// campaign's floors through <see cref="RecipeHintOps.FloorHasEnemy"/> (spawn tables, bosses and
    /// their adds, what room events wake), <see cref="Lines"/> words it through what the player knows -
    /// a level the story map does not show yet is never named, only counted.
    /// </summary>
    public static class EnemyHabitatOps
    {
        /// <summary>Every level, in campaign order, with the floors of it the enemy can be met on.</summary>
        public static List<EnemyHabitat> Find(EnemySO enemy, IEnumerable<RunDefinitionSO> runs)
        {
            var found = new List<EnemyHabitat>();
            if (enemy == null || runs == null)
            {
                return found;
            }
            foreach (var run in runs)
            {
                if (run?.Levels == null)
                {
                    continue;
                }
                EnemyHabitat habitat = null;
                foreach (var floor in run.Levels)
                {
                    if (!RecipeHintOps.FloorHasEnemy(floor, enemy))
                    {
                        continue;
                    }
                    if (habitat == null)
                    {
                        habitat = new EnemyHabitat { Run = run };
                        found.Add(habitat);
                    }
                    habitat.Floors.Add(floor);
                }
            }
            return found;
        }

        /// <summary>
        /// One line per level the player has reached - the level's name, then its floors ("The Mire
        /// Throne (boss)") - and one closing line counting the levels not reached yet. Empty when the
        /// enemy is met nowhere in the campaign's halls (a body only something else calls in).
        /// </summary>
        public static List<BestiaryLine> Lines(EnemySO enemy, List<EnemyHabitat> habitats, Func<RunDefinitionSO, bool> runReached)
        {
            var lines = new List<BestiaryLine>();
            if (habitats == null)
            {
                return lines;
            }
            int unreached = 0;
            foreach (var habitat in habitats)
            {
                if (runReached == null || !runReached(habitat.Run))
                {
                    unreached++;
                    continue;
                }
                var floors = new List<string>();
                foreach (var floor in habitat.Floors)
                {
                    string name = FloorName(habitat.Run, floor);
                    floors.Add(floor.BossEnemy == enemy
                        ? Loc.Format("bestiary.habitat.boss-floor", ("floor", name))
                        : name);
                }
                lines.Add(new BestiaryLine(CampaignOps.DisplayNameOf(habitat.Run),
                    string.Join(Loc.Get("bestiary.habitat.separator"), floors), BestiaryTone.Neutral));
            }
            if (unreached > 0)
            {
                lines.Add(new BestiaryLine(unreached == 1
                    ? Loc.Get("bestiary.habitat.unreached-one")
                    : Loc.Format("bestiary.habitat.unreached-many", ("count", unreached)), "", BestiaryTone.Unknown));
            }
            return lines;
        }

        private static string FloorName(RunDefinitionSO run, RunLevelEntry floor)
        {
            if (!string.IsNullOrEmpty(floor.LevelName))
            {
                return floor.LevelName;
            }
            int index = run.Levels.IndexOf(floor);
            return Loc.Format("bestiary.recipe.hint.floor-number", ("number", index + 1));
        }
    }
}
