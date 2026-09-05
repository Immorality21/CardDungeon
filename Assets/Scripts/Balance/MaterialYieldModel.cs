using System.Collections.Generic;
using Assets.Scripts.Items;

namespace Assets.Scripts.Balance
{
    /// <summary>Expected units of one material a floor (or a run) hands over.</summary>
    public class MaterialYield
    {
        public ItemSO Material;
        public string Key = "";
        public string Name = "";

        /// <summary>Units expected from kills, at this floor's modelled traversal.</summary>
        public float FromKills;

        /// <summary>Units expected from the floor's caches (<c>LevelDefinitionSO.MaterialTable</c>).</summary>
        public float FromCaches;

        public float Total => FromKills + FromCaches;
    }

    /// <summary>
    /// What a floor <b>yields in raw materials</b> - the tap side of <c>docs/plans/HUB.md</c> §7.
    ///
    /// <para>This exists because phase 1 of that plan is explicitly "drop rates measurable before
    /// anything depends on them". Buildings, upgrades and material-priced grid nodes are all drains
    /// on this number, and a drain can only be priced against a tap that has been measured. Every
    /// term is read off the same objects the game rolls against, so the model cannot drift from the
    /// drop tables: enemy weights come from the run curve's own room encounters, chances and
    /// quantities from <see cref="LootRoller.ExpectedQuantity"/>.</para>
    ///
    /// <para><b>Not modelled:</b> the player's route. A floor's kills are counted at the traversal
    /// the run curve already assumed - a beeline opens fewer rooms than a full clear - so this is a
    /// yield for the <i>modelled</i> player, not a ceiling for a completionist.</para>
    /// </summary>
    public static class MaterialYieldModel
    {
        /// <summary>
        /// Expected material yield of one modelled level, keyed by material. Rooms contribute their
        /// enemies' tables weighted by how often the room turns up; caches contribute the level's own
        /// <c>MaterialTable</c> once per authored treasure room.
        /// </summary>
        public static List<MaterialYield> ForLevel(LevelCurve level)
        {
            var byKey = new Dictionary<string, MaterialYield>();
            if (level == null)
            {
                return new List<MaterialYield>();
            }

            foreach (var room in level.Rooms)
            {
                if (room == null || room.Occurrences <= 0f)
                {
                    continue;
                }

                foreach (var member in room.Expected.Members)
                {
                    if (member == null || member.Definition == null || member.Definition.LootTable == null)
                    {
                        continue;
                    }

                    float kills = room.Occurrences * member.Weight;
                    foreach (var drop in member.Definition.LootTable)
                    {
                        Accumulate(byKey, drop, level.Index, kills, fromKills: true);
                    }
                }
            }

            if (level.Template != null && level.TreasureRooms > 0)
            {
                foreach (var drop in level.Template.MaterialTable)
                {
                    Accumulate(byKey, drop, level.Index, level.TreasureRooms, fromKills: false);
                }
            }

            return Sorted(byKey);
        }

        /// <summary>Expected material yield of a whole run - every level's, summed.</summary>
        public static List<MaterialYield> ForRun(RunCurve run)
        {
            var byKey = new Dictionary<string, MaterialYield>();
            if (run == null)
            {
                return new List<MaterialYield>();
            }

            foreach (var level in run.Levels)
            {
                foreach (var yield in ForLevel(level))
                {
                    var entry = GetOrCreate(byKey, yield.Material);
                    if (entry == null)
                    {
                        continue;
                    }
                    entry.FromKills += yield.FromKills;
                    entry.FromCaches += yield.FromCaches;
                }
            }

            return Sorted(byKey);
        }

        /// <summary>
        /// Materials in <paramref name="catalog"/> that nothing in <paramref name="runs"/> yields -
        /// authored content the player can never hold. This is the reachability check
        /// <c>docs/plans/HUB.md</c> asks for, wired in from day one rather than after buildings start
        /// depending on materials.
        /// </summary>
        public static List<ItemSO> Unobtainable(IList<ItemSO> catalog, IList<RunCurve> runs)
        {
            var missing = new List<ItemSO>();
            if (catalog == null)
            {
                return missing;
            }

            var yielded = new HashSet<string>();
            if (runs != null)
            {
                foreach (var run in runs)
                {
                    foreach (var yield in ForRun(run))
                    {
                        if (yield.Total > 0f)
                        {
                            yielded.Add(yield.Key);
                        }
                    }
                }
            }

            foreach (var item in catalog)
            {
                if (item != null
                    && item.Category == ItemCategory.Material
                    && !string.IsNullOrEmpty(item.Key)
                    && !yielded.Contains(item.Key))
                {
                    missing.Add(item);
                }
            }
            return missing;
        }

        /// <summary>
        /// Levels that author a <c>MaterialTable</c> but hold no cache to yield it from - dead
        /// content, and silently so: the table looks authored in the inspector and never rolls.
        /// </summary>
        public static List<LevelCurve> LevelsWithUnreachableMaterialTable(RunCurve run)
        {
            var dead = new List<LevelCurve>();
            if (run == null)
            {
                return dead;
            }

            foreach (var level in run.Levels)
            {
                if (level != null
                    && level.TreasureRooms <= 0
                    && level.Template != null
                    && level.Template.MaterialTable != null
                    && level.Template.MaterialTable.Count > 0)
                {
                    dead.Add(level);
                }
            }
            return dead;
        }

        private static void Accumulate(
            Dictionary<string, MaterialYield> byKey, LootDrop drop, int runLevelIndex, float rolls, bool fromKills)
        {
            if (drop == null || drop.Item == null
                || drop.Item.Category != ItemCategory.Material || rolls <= 0f)
            {
                return;
            }

            var entry = GetOrCreate(byKey, drop.Item);
            float units = rolls * LootRoller.ExpectedQuantity(drop, runLevelIndex);
            if (fromKills)
            {
                entry.FromKills += units;
            }
            else
            {
                entry.FromCaches += units;
            }
        }

        private static MaterialYield GetOrCreate(Dictionary<string, MaterialYield> byKey, ItemSO material)
        {
            if (material == null)
            {
                return null;
            }

            string key = string.IsNullOrEmpty(material.Key) ? material.name : material.Key;
            if (!byKey.TryGetValue(key, out var entry))
            {
                entry = new MaterialYield
                {
                    Material = material,
                    Key = key,
                    Name = string.IsNullOrEmpty(material.DisplayName) ? material.name : material.DisplayName
                };
                byKey[key] = entry;
            }
            return entry;
        }

        private static List<MaterialYield> Sorted(Dictionary<string, MaterialYield> byKey)
        {
            var list = new List<MaterialYield>(byKey.Values);
            list.Sort((a, b) =>
            {
                int byTotal = b.Total.CompareTo(a.Total);
                return byTotal != 0 ? byTotal : string.CompareOrdinal(a.Name, b.Name);
            });
            return list;
        }
    }
}
