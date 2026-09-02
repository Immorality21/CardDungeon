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
