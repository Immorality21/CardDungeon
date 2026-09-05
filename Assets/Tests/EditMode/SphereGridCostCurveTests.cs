using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Node price is a function of how far into the grid a node sits
    /// (<see cref="SphereGridOps.CostForDepth"/>). Without a guard that is only true until someone
    /// hand-edits one node in the inspector, and a grid with a node off the curve fills at a
    /// different rate than every published figure says it does — which is exactly the class of
    /// silent drift <c>docs/BALANCING.md</c> §5s was written to stop.
    /// </summary>
    public class SphereGridCostCurveTests
    {
        private static List<SphereGridSO> LoadEveryGrid()
        {
            var grids = new List<SphereGridSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:SphereGridSO"))
            {
                var grid = AssetDatabase.LoadAssetAtPath<SphereGridSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (grid != null)
                {
                    grids.Add(grid);
                }
            }
            return grids;
        }

        [Test]
        public void CostForDepth_RisesWithDepth()
        {
            for (int d = 1; d < 40; d++)
            {
                Assert.Greater(SphereGridOps.CostForDepth(d), SphereGridOps.CostForDepth(d - 1),
                    $"Depth {d} must cost more than depth {d - 1}, or the far end of a grid is not "
                    + "the expensive end and there is nothing left to pace progression with.");
            }
        }

        [Test]
        public void CostForDepth_StartsCheap()
        {
            // A new hero has to be able to buy something on their first run.
            Assert.LessOrEqual(SphereGridOps.CostForDepth(0), 20);
        }

        [Test]
        public void EveryGridNode_IsPricedOnTheCurve()
        {
            var offCurve = new List<string>();
            foreach (var grid in LoadEveryGrid())
            {
                var depths = SphereGridOps.DepthsFrom(grid);
                foreach (var node in grid.Nodes)
                {
                    if (node == null || !depths.TryGetValue(node.Key, out int depth))
                    {
                        continue;
                    }
                    // A default unlock is handed over rather than sold, so the curve has nothing to
                    // say about it - but it must be priced at 0, or every published "grid costs N
                    // XP" figure counts a node no one is ever charged for.
                    if (SphereGridOps.IsDefaultUnlocked(node))
                    {
                        if (node.XpCost != 0)
                        {
                            offCurve.Add($"{grid.name}/{node.Key} is unlocked by default but costs "
                                         + $"{node.XpCost} XP; a default unlock must be priced at 0.");
                        }
                        continue;
                    }
                    int expected = SphereGridOps.CostForDepth(depth);
                    if (node.XpCost != expected)
                    {
                        offCurve.Add($"{grid.name}/{node.Key} at depth {depth} costs {node.XpCost}, "
                                     + $"curve says {expected}");
                    }
                }
            }
            CollectionAssert.IsEmpty(offCurve, string.Join("\n", offCurve));
        }

        /// <summary>
        /// A node XP cannot reach is content nobody will ever see. Edges are undirected, so this
        /// only fails if a node was authored with no edges at all — which the grid editor window
        /// makes easy to do by accident.
        /// </summary>
        [Test]
        public void EveryGridNode_IsReachableFromTheStart()
        {
            var unreachable = new List<string>();
            foreach (var grid in LoadEveryGrid())
            {
                var depths = SphereGridOps.DepthsFrom(grid);
                foreach (var node in grid.Nodes)
                {
                    if (node != null && !depths.ContainsKey(node.Key))
                    {
                        unreachable.Add(grid.name + "/" + node.Key);
                    }
                }
            }
            CollectionAssert.IsEmpty(unreachable,
                "Unreachable node(s): " + string.Join(", ", unreachable));
        }

        /// <summary>
        /// The signatures a hero is meant to arrive holding. <c>UnlockedByDefault</c> is a bool in
        /// hand-edited YAML, so this pins both halves of the wiring at once: that the flag survived
        /// serialization, and that the spell behind it actually resolves off an <i>empty</i> save —
        /// which is the only state a brand-new hero is ever in.
        /// </summary>
        [Test]
        public void DefaultUnlockedSpells_AreKnownBeforeAnyXpIsSpent()
        {
            var armed = new List<string>();
            foreach (var grid in LoadEveryGrid())
            {
                var known = SphereGridOps.KnownMagicForNodes(grid, new List<string>());
                foreach (var node in grid.Nodes)
                {
                    if (!SphereGridOps.IsDefaultUnlocked(node)
                        || node.Kind != SphereNodeKind.MagicKnown
                        || string.IsNullOrEmpty(node.GrantedMagicKey))
                    {
                        continue;
                    }

                    armed.Add(grid.name + "/" + node.GrantedMagicKey);
                    Assert.IsTrue(known.Exists(pair => pair.Key == node.GrantedMagicKey),
                        $"{grid.name} marks {node.Key} unlocked by default but a fresh save does "
                        + "not know " + node.GrantedMagicKey + ".");
                }
            }

            CollectionAssert.IsNotEmpty(armed,
                "No grid hands a hero a spell for free, so no hero starts able to cast anything and "
                + "the default-unlock flag is dead content.");
        }

        /// <summary>
        /// A material price is authored by dragging an <c>ItemSO</c> into a list, which makes an
        /// empty line, a zero amount or a non-material item all one slip away — and every one of
        /// them fails <i>open</i>: <c>MaterialCost.IsValid</c> drops the line, so the node quietly
        /// becomes free rather than refusing to be bought.
        /// </summary>
        [Test]
        public void EveryMaterialPrice_NamesARealMaterial()
        {
            var broken = new List<string>();
            foreach (var grid in LoadEveryGrid())
            {
                foreach (var node in grid.Nodes)
                {
                    if (node?.MaterialCosts == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < node.MaterialCosts.Count; i++)
                    {
                        var line = node.MaterialCosts[i];
                        if (line == null || !line.IsValid)
                        {
                            broken.Add($"{grid.name}/{node.Key} material line {i} is not a valid "
                                       + "price (no item, a non-material item, or amount 0).");
                        }
                    }
                }
            }
            CollectionAssert.IsEmpty(broken, string.Join("\n", broken));
        }

        /// <summary>
        /// A node key is a save identifier, so two nodes sharing one means a save records the wrong
        /// purchase. Same contract as <c>HeroSO.Key</c> and <c>EnemySO.Key</c>.
        /// </summary>
        [Test]
        public void EveryGridNode_HasAUniqueNonEmptyKey()
        {
            foreach (var grid in LoadEveryGrid())
            {
                var keys = grid.Nodes.Where(n => n != null).Select(n => n.Key).ToList();
                CollectionAssert.IsEmpty(keys.Where(string.IsNullOrEmpty).ToList(),
                    grid.name + " has a node with no Key.");
                Assert.AreEqual(keys.Count, keys.Distinct().Count(),
                    grid.name + " has duplicate node keys.");
            }
        }
    }
}
