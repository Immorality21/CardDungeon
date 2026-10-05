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
                Assert.GreaterOrEqual(summon.BaseCharges, 1, $"{summon.name} has no charges.");
                if (summon.Kind == SummonKind.SpecialAttack)
                {
                    Assert.IsNotEmpty(summon.Effects, $"{summon.name} does nothing.");
                    continue;
                }

                // A squad is its troops: each is a summon asset of its own and checked as a unit here.
                if (summon.IsSquad)
                {
                    Assert.That(summon.SquadTiers.All(t => t != null), $"{summon.name} has an empty troop tier.");
                    Assert.That(summon.SquadTiers.All(t => !t.IsSquad), $"{summon.name} nests a squad in a squad.");
                    Assert.LessOrEqual(summon.SquadSize, summon.MaxSquadSize, $"{summon.name} starts above its own cap.");
                    Assert.LessOrEqual(summon.MaxSquadSize, 4, $"{summon.name} can field more troops than the hero side has slots.");
                    Assert.GreaterOrEqual(summon.TurnsActive, 1, $"{summon.name} never stays.");
                    continue;
                }

                // A party replacement is a unit: it needs a body, a stay, and something to do.
                Assert.Greater(summon.StatPercents[Assets.Scripts.UnitStats.StatType.MaxHealth], 0,
                    $"{summon.name} brings no health - it would fall to the first blow, or arrive dead.");
                Assert.Greater(summon.StatPercents[Assets.Scripts.UnitStats.StatType.Strength], 0,
                    $"{summon.name} has no Strength, so its Attack does nothing.");
                Assert.GreaterOrEqual(summon.TurnsActive, 1, $"{summon.name} never stays.");
                Assert.IsNotNull(summon.Signature, $"{summon.name} has no Signature.");
                Assert.That(summon.Actions.All(a => a != null), $"{summon.name} has an empty action slot.");
                Assert.That(summon.AnimationFrames.All(f => f != null), $"{summon.name} has an empty animation frame.");
            }
        }

        [Test]
        public void SummonAbilities_AreNeverAHerosMagic()
        {
            // A summon's abilities cast through the Boar's path - no Forge bonus, no tags, no combos.
            // On a grid they would be learnable and carried; in the catalog they would be upgradeable
            // in the Forge and resolvable into a hero's slot. Both would quietly break that rule.
            var abilityKeys = SummonOps.AbilityKeys(AllSummonAssets());
            Assert.IsNotEmpty(abilityKeys, "No summon has abilities, so this guards nothing.");

            var onGrids = AllGrids().SelectMany(g => g.Nodes)
                .Where(n => n != null && n.Kind == SphereNodeKind.MagicKnown && abilityKeys.Contains(n.GrantedMagicKey))
                .Select(n => n.Key).ToList();
            Assert.IsEmpty(onGrids, "Grid nodes teach a summon's own ability: " + string.Join(", ", onGrids));

            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/MagicCatalog.prefab");
            var catalog = prefab != null ? prefab.GetComponent<MagicCatalog>() : null;
            Assert.IsNotNull(catalog, "No MagicCatalog prefab.");
            var inCatalog = catalog.AllMagic.Where(m => m != null && abilityKeys.Contains(m.Key)).Select(m => m.Key).ToList();
            Assert.IsEmpty(inCatalog, "The hero magic catalog lists a summon's own ability: " + string.Join(", ", inCatalog));
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
            // A Sacrifice Ultra's creature is reached through the Ultra (the Cultist's Horror).
            foreach (var guid in AssetDatabase.FindAssets("t:UltraSO"))
            {
                var ultra = AssetDatabase.LoadAssetAtPath<UltraSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (ultra != null && ultra.Creature != null)
                {
                    taught.Add(ultra.Creature.Key);
                }
            }
            // A squad's troops are reached through the squad (the Demon Army's Imps and Succubi).
            var catalog = SummonCatalogSO.Load();
            foreach (var key in taught.ToList())
            {
                var squad = catalog.Find(key);
                if (squad != null && squad.IsSquad)
                {
                    taught.AddRange(squad.SquadTiers.Where(t => t != null).Select(t => t.Key));
                }
            }
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
                   || kind == SphereNodeKind.SummonCharge || kind == SphereNodeKind.SummonSize
                   || kind == SphereNodeKind.SummonPromote;
        }
    }
}
