using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A grid's edges are drawn as straight lines, so a node lying on a line it is not part of reads
    /// as a step on that path. Every grid's trunk ability slot sat exactly on the line from the hub
    /// node to the middle branch's first ability: it looked like a gate to that branch, but the
    /// branch could be bought without it (playtest 3 finding 4). These pin that no node sits on a
    /// line it does not end, and that no two lines cross.
    /// </summary>
    public class SphereGridLayoutTests
    {
        // The node is drawn 44px wide (.sg-node), so a line nearer than its radius plus a margin
        // visibly runs under it.
        private const float MinClearance = 35f;

        [Test]
        public void EveryGrid_NoNode_SitsOnAnEdgeItIsNotPartOf()
        {
            foreach (var grid in LoadGrids())
            {
                var nodes = new List<SphereGridView.NodeInfo>();
                var edges = new List<(string A, string B)>();
                SphereGridPresenter.BuildViewModel(grid, nodes, edges);
                var positions = PositionsOf(nodes);

                foreach (var edge in edges)
                {
                    if (!positions.TryGetValue(edge.A, out var a) || !positions.TryGetValue(edge.B, out var b))
                    {
                        continue;
                    }
                    foreach (var node in nodes)
                    {
                        if (node.Key == edge.A || node.Key == edge.B)
                        {
                            continue;
                        }
                        float distance = DistanceToSegmentInterior(node.Position, a, b);
                        Assert.IsFalse(distance < MinClearance,
                            $"{grid.name}: node '{node.Key}' is {distance:0}px from the line {edge.A} - {edge.B}, " +
                            "so it reads as a step on that path. Move it clear of the line.");
                    }
                }
            }
        }

        [Test]
        public void EveryGrid_NoTwoEdges_Cross()
        {
            foreach (var grid in LoadGrids())
            {
                var nodes = new List<SphereGridView.NodeInfo>();
                var edges = new List<(string A, string B)>();
                SphereGridPresenter.BuildViewModel(grid, nodes, edges);
                var positions = PositionsOf(nodes);

                for (int i = 0; i < edges.Count; i++)
                {
                    for (int j = i + 1; j < edges.Count; j++)
                    {
                        var e1 = edges[i];
                        var e2 = edges[j];
                        if (e1.A == e2.A || e1.A == e2.B || e1.B == e2.A || e1.B == e2.B)
                        {
                            continue;
                        }
                        if (!positions.ContainsKey(e1.A) || !positions.ContainsKey(e1.B)
                            || !positions.ContainsKey(e2.A) || !positions.ContainsKey(e2.B))
                        {
                            continue;
                        }
                        Assert.IsFalse(
                            Cross(positions[e1.A], positions[e1.B], positions[e2.A], positions[e2.B]),
                            $"{grid.name}: the lines {e1.A} - {e1.B} and {e2.A} - {e2.B} cross.");
                    }
                }
            }
        }

        private static List<SphereGridSO> LoadGrids()
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
            Assert.IsNotEmpty(grids);
            return grids;
        }

        private static Dictionary<string, Vector2> PositionsOf(List<SphereGridView.NodeInfo> nodes)
        {
            var positions = new Dictionary<string, Vector2>();
            foreach (var node in nodes)
            {
                positions[node.Key] = node.Position;
            }
            return positions;
        }

        /// <summary>Distance from <paramref name="p"/> to the segment, or infinity when its nearest
        /// point is an end - a node beside an end is a neighbour of that node, not on the line.</summary>
        private static float DistanceToSegmentInterior(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0f)
            {
                return float.PositiveInfinity;
            }
            float t = Vector2.Dot(p - a, ab) / lengthSquared;
            if (t <= 0f || t >= 1f)
            {
                return float.PositiveInfinity;
            }
            return Vector2.Distance(p, a + t * ab);
        }

        private static bool Cross(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            return Side(p1, p2, p3) * Side(p1, p2, p4) < 0f && Side(p3, p4, p1) * Side(p3, p4, p2) < 0f;
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}
