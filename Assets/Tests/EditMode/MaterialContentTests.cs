using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Asset-integrity guards for raw materials — the taps side of <c>docs/plans/HUB.md</c> §7.
    ///
    /// <para>Every failure here is invisible in the inspector, which is why they are tests rather
    /// than a habit: a material with no source looks perfectly authored, a drop table with a null
    /// item row looks like an empty line, and a quantity range typed backwards silently pays out its
    /// minimum forever. All three get much more expensive once buildings and grid nodes are priced
    /// in materials, so the checks land with the drops rather than after them.</para>
    /// </summary>
    public class MaterialContentTests
    {
        private static List<T> LoadAll<T>() where T : UnityEngine.ScriptableObject
        {
            var results = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    results.Add(asset);
                }
            }
            results.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return results;
        }

        private static List<ItemSO> Materials()
        {
            return LoadAll<ItemSO>().Where(i => i.Category == ItemCategory.Material).ToList();
        }

        /// <summary>Every drop-table entry in the project, with a label saying where it came from.</summary>
        private static List<(string Source, LootDrop Drop)> EveryDrop()
        {
            var drops = new List<(string, LootDrop)>();
            foreach (var enemy in LoadAll<EnemySO>())
            {
                foreach (var drop in enemy.LootTable)
                {
                    drops.Add(($"{enemy.name}.LootTable", drop));
                }
            }
            foreach (var level in LoadAll<LevelDefinitionSO>())
            {
                foreach (var drop in level.MaterialTable)
                {
                    drops.Add(($"{level.name}.MaterialTable", drop));
                }
                foreach (var drop in level.GuaranteedMaterials)
                {
                    drops.Add(($"{level.name}.GuaranteedMaterials", drop));
                }
            }
            return drops;
        }

        /// <summary>
        /// A guaranteed drop has to actually be guaranteed. The guarantee comes from <i>where</i>
        /// the table is rolled — unconditionally, on level clear — rather than from a new meaning
        /// for <c>Chance</c>, so an entry authored below 1 is a promise the game silently does not
        /// keep. That is the worst possible failure for the one table a tutorial can point at.
        /// </summary>
        [Test]
        public void EveryGuaranteedDrop_IsActuallyGuaranteed()
        {
            var broken = new List<string>();
            foreach (var level in LoadAll<LevelDefinitionSO>())
            {
                foreach (var drop in level.GuaranteedMaterials)
                {
                    if (drop == null || drop.Item == null)
                    {
                        broken.Add($"{level.name}.GuaranteedMaterials has an entry with no item.");
                        continue;
                    }
                    if (!Mathf.Approximately(drop.Chance, 1f))
                    {
                        broken.Add($"{level.name}.GuaranteedMaterials/{drop.Item.name} is at "
                                   + $"Chance {drop.Chance}, so it is not guaranteed at all.");
                    }
                    if (drop.Item.Category != ItemCategory.Material)
                    {
                        broken.Add($"{level.name}.GuaranteedMaterials/{drop.Item.name} is not a "
                                   + "material.");
                    }
                }
            }
            CollectionAssert.IsEmpty(broken, string.Join(System.Environment.NewLine, broken));
        }

        /// <summary>
        /// Anything a hub lot asks for on a fresh save has to be <b>obtainable</b> on the campaign's
        /// opening run — otherwise the first thing the player is invited to build is one they cannot.
        ///
        /// <para>The trap this exists for is the <b>cacheless floor</b>: a `MaterialTable` never rolls
        /// on a level with `TreasureRooms: 0`, so a cache-only material can look perfectly authored on
        /// a floor that will never yield a single unit of it. That is exactly how the Sphere Hall came
        /// to cost timber the tutorial's first floor could not produce.</para>
        ///
        /// <para>Deliberately loose about enemy drops: it counts a material as obtainable if
        /// <i>any</i> enemy drops it, rather than walking each level's room pool to its spawn tables.
        /// Being precise there would test the level's room templates, not the hub's prices, and the
        /// failure it would add — "this material only drops from a monster that does not live here" —
        /// is a different check that belongs with the spawn tables.</para>
        /// </summary>
        [Test]
        public void EveryOpeningHubCost_IsObtainableOnTheOpeningRun()
        {
            var hub = UnityEngine.Resources.Load<Assets.Scripts.Hub.HubSO>(
                Assets.Scripts.Hub.HubSO.ResourcePath);
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var roots = CampaignOps.GetRootNodes(campaign);
            Assert.IsNotEmpty(roots, "No opening run to check against.");

            var obtainable = new List<string>();

            void Offer(LootDrop drop)
            {
                if (drop?.Item != null && !obtainable.Contains(drop.Item.Key))
                {
                    obtainable.Add(drop.Item.Key);
                }
            }

            foreach (var index in roots)
            {
                foreach (var entry in campaign.Nodes[index].Run.Levels)
                {
                    var level = entry?.LevelTemplate;
                    if (level == null)
                    {
                        continue;
                    }
                    foreach (var drop in level.GuaranteedMaterials)
                    {
                        Offer(drop);
                    }
                    // Only when the floor can actually roll a cache.
                    if (level.TreasureRooms > 0)
                    {
                        foreach (var drop in level.MaterialTable)
                        {
                            Offer(drop);
                        }
                    }
                }
            }

            foreach (var enemy in LoadAll<EnemySO>())
            {
                foreach (var drop in enemy.LootTable)
                {
                    Offer(drop);
                }
            }

            var unreachable = new List<string>();
            foreach (var building in Assets.Scripts.Hub.BuildingOps.InDrawOrder(hub))
            {
                bool offeredAtOnce = building.RequiredRunKeys == null
                                     || building.RequiredRunKeys.Count == 0;
                if (!offeredAtOnce || building.PlacedByDefault || building.PlacementCost == null)
                {
                    continue;
                }
                foreach (var line in building.PlacementCost)
                {
                    if (line?.Material != null && !obtainable.Contains(line.Material.Key))
                    {
                        unreachable.Add($"{building.SaveKey} is offered on a fresh save and needs "
                                        + $"{line.Material.Key}, which the opening run cannot yield "
                                        + "(no guarantee, no cache that rolls it, no enemy drops it).");
                    }
                }
            }
            CollectionAssert.IsEmpty(unreachable, string.Join(System.Environment.NewLine, unreachable));
        }

        [Test]
        public void EveryMaterial_HasAKeyAndADisplayName()
        {
            var broken = Materials()
                .Where(m => string.IsNullOrEmpty(m.Key) || string.IsNullOrEmpty(m.DisplayName))
                .Select(m => m.name)
                .ToList();

            CollectionAssert.IsEmpty(broken,
                $"Material asset(s) {string.Join(", ", broken)} are missing a Key or DisplayName. The key "
                + "is what the inventory save files a pile under, and the display name is the only thing "
                + "the hub can show.");
        }

        [Test]
        public void EveryMaterial_StacksHighEnoughToBeWorthGathering()
        {
            var shallow = Materials().Where(m => m.MaxStack < 99).Select(m => $"{m.name} ({m.MaxStack})").ToList();

            CollectionAssert.IsEmpty(shallow,
                $"Material(s) {string.Join(", ", shallow)} have a low MaxStack. Materials accumulate across "
                + "runs and are spent in bulk; a stack ceiling below 99 silently throws drops away.");
        }

        [Test]
        public void EveryMaterial_IsDroppedBySomething()
        {
            // The reachability check docs/plans/HUB.md asks for. A material nothing yields is content
            // the player can never hold, and once a building is priced in it, an unfinishable hub.
            var yielded = new HashSet<string>(
                EveryDrop()
                    .Where(entry => entry.Drop != null && entry.Drop.Item != null)
                    .Select(entry => entry.Drop.Item.Key));

            var orphans = Materials().Where(m => !yielded.Contains(m.Key)).Select(m => m.name).ToList();

            CollectionAssert.IsEmpty(orphans,
                $"Material(s) {string.Join(", ", orphans)} appear in no EnemySO.LootTable and no "
                + "LevelDefinitionSO.MaterialTable. Give them a source or delete them.");
        }

        [Test]
        public void EveryDropTableEntry_NamesAnItem()
        {
            var blanks = EveryDrop()
                .Where(entry => entry.Drop == null || entry.Drop.Item == null)
                .Select(entry => entry.Source)
                .Distinct()
                .ToList();

            CollectionAssert.IsEmpty(blanks,
                $"Drop table(s) {string.Join(", ", blanks)} hold an entry with no item. It rolls to nothing "
                + "and reads as an authored drop.");
        }

        [Test]
        public void EveryDropTableEntry_HasASaneQuantityRange()
        {
            var broken = EveryDrop()
                .Where(entry => entry.Drop != null && entry.Drop.Item != null)
                .Where(entry => entry.Drop.MinQuantity < 1 || entry.Drop.MaxQuantity < entry.Drop.MinQuantity)
                .Select(entry => $"{entry.Source} → {entry.Drop.Item.name} "
                               + $"({entry.Drop.MinQuantity}-{entry.Drop.MaxQuantity})")
                .ToList();

            CollectionAssert.IsEmpty(broken,
                $"Drop entr(ies) {string.Join(", ", broken)} have an impossible quantity range. A max below "
                + "the min is clamped at roll time, so the entry silently pays its minimum forever.");
        }

        [Test]
        public void NoDropTableEntry_AsksAStackFromANonStackingItem()
        {
            // Equipment can never be a pile - an inventory entry carries which hero has it equipped -
            // so a range on a sword is an authoring mistake that would otherwise never surface.
            var broken = EveryDrop()
                .Where(entry => entry.Drop != null && entry.Drop.Item != null)
                .Where(entry => !entry.Drop.Item.Stacks && entry.Drop.MaxQuantity > 1)
                .Select(entry => $"{entry.Source} → {entry.Drop.Item.name}")
                .ToList();

            CollectionAssert.IsEmpty(broken,
                $"Drop entr(ies) {string.Join(", ", broken)} ask for several of a non-stacking item. "
                + "Equipment always drops one at a time; the range is ignored.");
        }

        [Test]
        public void EveryMaterialDrop_SetsAnExplicitChance()
        {
            // Chance 0 falls back on rarity + run depth, which is the *gear* regime: it suppresses an
            // over-level item and would make a deep material nearly undroppable at the depth it is
            // authored for. Materials are gated by which monster and which floor, so they state their
            // own odds.
            var implicitChance = EveryDrop()
                .Where(entry => entry.Drop != null
                                && entry.Drop.Item != null
                                && entry.Drop.Item.Category == ItemCategory.Material
                                && !entry.Drop.HasExplicitChance)
                .Select(entry => $"{entry.Source} → {entry.Drop.Item.name}")
                .ToList();

            CollectionAssert.IsEmpty(implicitChance,
                $"Material drop(s) {string.Join(", ", implicitChance)} leave Chance at 0, which falls back "
                + "on the rarity + depth math meant for gear. Author the odds directly.");
        }

        [Test]
        public void EveryMaterial_IsInTheResourcesItemCatalog()
        {
            // The hub resolves item keys through the Resources catalog, not through scene wiring, so a
            // material missing from it drops into the bag and then displays as a bare key.
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            Assert.IsNotNull(catalog, "Assets/Resources/ItemCatalog.asset is missing.");

            var listed = new HashSet<string>(
                catalog.Items.Where(i => i != null).Select(i => i.Key));
            var missing = Materials().Where(m => !listed.Contains(m.Key)).Select(m => m.name).ToList();

            CollectionAssert.IsEmpty(missing,
                $"Material(s) {string.Join(", ", missing)} are not in the Resources ItemCatalog, so the hub "
                + "cannot resolve them.");
        }
    }
}
