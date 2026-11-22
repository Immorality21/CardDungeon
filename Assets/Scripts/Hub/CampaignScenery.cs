using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>One piece of scenery on the story map: which sprite (an index into the woods kinds,
    /// then the open-ground kinds after them), where its foot stands, and how much bigger or smaller it is.</summary>
    public struct SceneryPiece
    {
        public int Kind;
        public Vector2 Position;
        public float Scale;
    }

    /// <summary>
    /// Where the story map's scenery goes (owner, 2026-11-22: an illustrated world map that has to
    /// keep working as the campaign grows past the 20% that exists). Pure, so it is tested without a
    /// screen.
    ///
    /// <para>Nothing is painted for the current layout. The ground is a tile that repeats under the
    /// whole map, and the trees, rocks and ruins are scattered here one grid cell at a time from a
    /// hash of the cell and a seed. That is what lets the map grow: a new level placed anywhere gets
    /// scenery around it, and adding one never reshuffles what is already standing, because each
    /// cell decides for itself. Only the clearings move: nothing stands on a visible level or its
    /// caption, or on a visible trail, so a secret branch opening clears its own path.</para>
    /// </summary>
    public static class CampaignScenery
    {
        /// <summary>One candidate spot per cell this size, in map units.</summary>
        public const float CellSize = 56f;

        /// <summary>How far the ground and scenery reach past the outermost level.</summary>
        public const float Margin = 420f;

        // The clearing around a level: its landmark above and its name below.
        private const float ClearHalfWidth = 78f;
        private const float ClearAbove = 70f;
        private const float ClearBelow = 64f;

        private const float TrailClearance = 20f;

        // Woods and open moor: a cell's chance is the density scaled by a slow noise over the map, so
        // trees gather instead of spreading evenly like static. Measured in cells.
        private const float WoodsScale = 5f;

        /// <summary>The ground's extent: every level of the campaign, visible or not, plus
        /// <see cref="Margin"/>. Built from all of them so a branch opening does not move the edge.</summary>
        public static Rect Area(IEnumerable<Vector2> allLevelPositions)
        {
            bool any = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            if (allLevelPositions != null)
            {
                foreach (var p in allLevelPositions)
                {
                    if (!any)
                    {
                        min = max = p;
                        any = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
            }
            return Rect.MinMaxRect(min.x - Margin, min.y - Margin, max.x + Margin, max.y + Margin);
        }

        // How much sparser the open moor is than the heart of a wood, and how open a cell must be
        // to hold a rock or a ruin rather than nothing.
        private const float OpenShare = 0.2f;
        private const float OpenBelow = 0.15f;

        /// <summary>Woods only: <see cref="Scatter(Rect, IReadOnlyList{Vector2}, IReadOnlyList{ValueTuple{Vector2, Vector2}}, int, int, float, int)"/> with no open-ground kinds.</summary>
        public static List<SceneryPiece> Scatter(Rect area, IReadOnlyList<Vector2> levels,
            IReadOnlyList<(Vector2 A, Vector2 B)> trails, int kinds, float density, int seed)
        {
            return Scatter(area, levels, trails, kinds, 0, density, seed);
        }

        /// <summary>
        /// The scenery for <paramref name="area"/>, lowest last so it draws in front. In a wood
        /// (<see cref="Woods"/>) a cell holds a tree-kind piece (<c>0 .. woodKinds-1</c>) when its hash
        /// falls under the density scaled by how wooded it is; on open moor it may hold a rare
        /// open-ground piece instead (<c>woodKinds ..</c>): a rock, a ruin. Each piece is jittered inside
        /// its cell and left out if it would stand on a level or a trail.
        /// </summary>
        public static List<SceneryPiece> Scatter(Rect area, IReadOnlyList<Vector2> levels,
            IReadOnlyList<(Vector2 A, Vector2 B)> trails, int woodKinds, int openKinds, float density, int seed,
            IReadOnlyList<(Vector2 Center, float Radius)> clear = null)
        {
            var pieces = new List<SceneryPiece>();
            if (woodKinds + openKinds <= 0 || density <= 0f)
            {
                return pieces;
            }

            int x0 = Mathf.FloorToInt(area.xMin / CellSize);
            int x1 = Mathf.CeilToInt(area.xMax / CellSize);
            int y0 = Mathf.FloorToInt(area.yMin / CellSize);
            int y1 = Mathf.CeilToInt(area.yMax / CellSize);
            for (int cy = y0; cy < y1; cy++)
            {
                for (int cx = x0; cx < x1; cx++)
                {
                    uint h = Hash(cx, cy, seed);
                    float woods = Woods(cx, cy, seed);
                    int kind;
                    // Twice the density at the heart of a wood; on the open moor, now and then a rock.
                    if (woodKinds > 0 && Unit(h) < density * 2f * woods)
                    {
                        kind = (int)((h >> 20) % (uint)woodKinds);
                    }
                    else if (openKinds > 0 && woods < OpenBelow && Unit(h) < density * OpenShare)
                    {
                        kind = woodKinds + (int)((h >> 20) % (uint)openKinds);
                    }
                    else
                    {
                        continue;
                    }
                    uint h2 = Hash(cx, cy, seed ^ 0x5bd1e995);
                    var position = new Vector2(
                        (cx + 0.15f + 0.7f * Unit(h2)) * CellSize,
                        (cy + 0.15f + 0.7f * Unit(h2 >> 16 | h2 << 16)) * CellSize);
                    if (!area.Contains(position) || Blocked(position, levels, trails, clear))
                    {
                        continue;
                    }
                    pieces.Add(new SceneryPiece
                    {
                        Kind = kind,
                        Position = position,
                        Scale = 0.85f + 0.3f * Unit(h2 * 2654435761u)
                    });
                }
            }
            // Further down the map is nearer the viewer: draw it last.
            pieces.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));
            return pieces;
        }

        /// <summary>Whether a piece standing at <paramref name="p"/> would cover a level, its name or a trail.</summary>
        public static bool Blocked(Vector2 p, IReadOnlyList<Vector2> levels, IReadOnlyList<(Vector2 A, Vector2 B)> trails,
            IReadOnlyList<(Vector2 Center, float Radius)> clear = null)
        {
            if (clear != null)
            {
                foreach (var circle in clear)
                {
                    if ((p - circle.Center).sqrMagnitude < circle.Radius * circle.Radius)
                    {
                        return true;
                    }
                }
            }
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    var d = p - level;
                    if (Mathf.Abs(d.x) < ClearHalfWidth && d.y > -ClearAbove && d.y < ClearBelow)
                    {
                        return true;
                    }
                }
            }
            if (trails != null)
            {
                foreach (var trail in trails)
                {
                    if (DistanceToSegment(p, trail.A, trail.B) < TrailClearance)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>How wooded the map is around a cell, 0 (open moor) to 1: smoothed value noise on a
        /// grid <see cref="WoodsScale"/> cells wide, so it depends only on where the cell is.</summary>
        public static float Woods(int cx, int cy, int seed)
        {
            float fx = cx / WoodsScale;
            float fy = cy / WoodsScale;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - x0);
            float ty = Smooth(fy - y0);
            int woodsSeed = seed ^ 0x2c1b3c6d;
            float a = Unit(Hash(x0, y0, woodsSeed));
            float b = Unit(Hash(x0 + 1, y0, woodsSeed));
            float c = Unit(Hash(x0, y0 + 1, woodsSeed));
            float d = Unit(Hash(x0 + 1, y0 + 1, woodsSeed));
            float v = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
            // Sharpen it, so woods have edges and the moor between them is mostly bare.
            return Mathf.Clamp01((v - 0.35f) / 0.4f);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // An integer hash of a cell and the seed: the same cell always answers the same, whatever
        // else is on the map.
        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Unit(uint h)
        {
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
