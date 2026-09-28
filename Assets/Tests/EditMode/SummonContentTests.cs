using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Guards the hand-populated summon content: the Resources catalog, and every grid node that
    /// names a summon. A summon missing from the catalog cannot be resolved in a dungeon, so its node
    /// would teach nothing — silently, which is the failure every catalog test in this project exists
    /// to catch.
    /// </summary>
    public class SummonContentTests
    {
        private static List<SummonSO> AllSummonAssets()
        {
            return AssetDatabase.FindAssets("t:SummonSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<SummonSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .ToList();
        }

        private static List<SphereGridSO> AllGrids()
        {
            return AssetDatabase.FindAssets("t:SphereGridSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<SphereGridSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .ToList();
        }

        [Test]
        public void Catalog_ExistsInResources_AndHoldsEverySummonOnce()
        {
            var catalog = SummonCatalogSO.Load();
            Assert.IsNotNull(catalog, "Assets/Resources/SummonCatalog.asset is missing.");
            Assert.That(catalog.Summons.All(s => s != null), "The catalog holds a null entry.");

            var keys = catalog.Summons.Select(s => s.Key).ToList();
            Assert.AreEqual(keys.Count, keys.Distinct().Count(), "Two summons share a key.");
            Assert.That(keys.All(k => !string.IsNullOrEmpty(k)), "A summon has no key.");

            var missing = AllSummonAssets().Where(s => !catalog.Summons.Contains(s)).Select(s => s.name).ToList();
            Assert.IsEmpty(missing, "Summon assets not in Resources/SummonCatalog: " + string.Join(", ", missing));
        }

        [Test]
        public void EverySummon_IsComplete()
        {
            foreach (var summon in AllSummonAssets())
            {
                Assert.IsNotNull(summon.Sprite, $"{summon.name} has no creature sprite - the summoning moment would show nothing.");
                Assert.IsNotEmpty(summon.Effects, $"{summon.name} does nothing.");
                Assert.GreaterOrEqual(summon.BaseCharges, 1, $"{summon.name} has no charges.");
            }
        }

        [Test]
        public void EverySummonNode_NamesASummonInTheCatalog()
        {
            var catalog = SummonCatalogSO.Load();
            foreach (var grid in AllGrids())
            {
                foreach (var node in grid.Nodes.Where(n => n != null && IsSummonKind(n.Kind)))
                {
                    Assert.IsNotNull(catalog.Find(node.GrantedSummonKey),
                        $"{grid.name}/{node.Key} names summon '{node.GrantedSummonKey}', which is not in the catalog.");
                }
            }
        }

        [Test]
        public void EveryUpgradeNode_UpgradesASummonItsOwnGridTeaches()
        {
            foreach (var grid in AllGrids())
            {
                var taught = grid.Nodes.Where(n => n != null && n.Kind == SphereNodeKind.Summon)
                    .Select(n => n.GrantedSummonKey).ToList();
                foreach (var node in grid.Nodes.Where(n => n != null && IsUpgradeKind(n.Kind)))
                {
                    Assert.Contains(node.GrantedSummonKey, taught,
                        $"{grid.name}/{node.Key} upgrades '{node.GrantedSummonKey}', which this grid never teaches - it would do nothing.");
                    Assert.Greater(node.SummonAmount, 0, $"{grid.name}/{node.Key} adds nothing.");
                }
            }
        }

        [Test]
        public void EveryCatalogSummon_IsTaughtBySomeGrid()
        {
            var taught = AllGrids().SelectMany(g => g.Nodes)
                .Where(n => n != null && n.Kind == SphereNodeKind.Summon)
                .Select(n => n.GrantedSummonKey).ToList();
            var orphans = SummonCatalogSO.Load().Summons.Where(s => !taught.Contains(s.Key)).Select(s => s.Key).ToList();
            Assert.IsEmpty(orphans, "Summons no grid teaches (unreachable): " + string.Join(", ", orphans));
        }

        private static bool IsSummonKind(SphereNodeKind kind)
        {
            return kind == SphereNodeKind.Summon || IsUpgradeKind(kind);
        }

        private static bool IsUpgradeKind(SphereNodeKind kind)
        {
            return kind == SphereNodeKind.SummonPower || kind == SphereNodeKind.SummonDuration
                   || kind == SphereNodeKind.SummonCharge;
        }
    }
}
