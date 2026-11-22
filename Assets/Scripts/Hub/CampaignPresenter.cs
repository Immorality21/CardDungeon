using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes.UI;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Turns campaign state into things a graph view can draw: a USS class per node, a glyph, a
    /// position, and the edge list. Pure and static, exactly like <c>SphereGridPresenter</c> - the map
    /// screen makes no progression decisions of its own, it renders what this returns.
    ///
    /// <para>The renderer is <see cref="SphereGridView"/>, reused rather than reimplemented: it is
    /// already a generic pan/zoom node-graph widget whose only vocabulary is keys, positions and class
    /// names. The campaign supplies its own <c>cm-node--*</c> classes so the two graphs cannot clear
    /// each other's state.</para>
    /// </summary>
    public static class CampaignPresenter
    {
        /// <summary>State classes in the order <see cref="SphereGridView.StateClassNames"/> expects.</summary>
        public static readonly string[] StateClasses =
        {
            "cm-node--completed", "cm-node--available", "cm-node--active", "cm-node--locked"
        };

        public const string CompletedClass = "cm-node--completed";
        public const string AvailableClass = "cm-node--available";
        public const string ActiveClass = "cm-node--active";
        public const string LockedClass = "cm-node--locked";

        /// <summary>Horizontal gap between campaign tiers when positions are laid out automatically.</summary>
        private const float TierSpacing = 190f;

        /// <summary>Vertical gap between the branches sharing a tier.</summary>
        private const float BranchSpacing = 110f;

        public static string StateClass(CampaignNodeStatus status)
        {
            switch (status)
            {
                case CampaignNodeStatus.Completed:
                    return CompletedClass;
                case CampaignNodeStatus.Available:
                    return AvailableClass;
                case CampaignNodeStatus.InProgress:
                    return ActiveClass;
                default:
                    return LockedClass;
            }
        }

        /// <summary>A one-word status word for the detail panel.</summary>
        public static string StatusLabel(CampaignNodeState state)
        {
            if (state == null)
            {
                return string.Empty;
            }
            switch (state.Status)
            {
                case CampaignNodeStatus.Completed:
                    return state.CanStart ? Loc.Get("hub.campaign.status.cleared-rerun") : Loc.Get("hub.campaign.status.cleared");
                case CampaignNodeStatus.Available:
                    return Loc.Get("hub.campaign.status.open");
                case CampaignNodeStatus.InProgress:
                    return Loc.Get("hub.campaign.status.in-progress");
                case CampaignNodeStatus.Locked:
                    return Loc.Get("hub.campaign.status.locked");
                default:
                    return string.Empty;
            }
        }

        /// <summary>The glyph drawn inside a node: a tick for cleared, a marker for the run underway.</summary>
        public static string Glyph(CampaignNodeState state)
        {
            if (state == null)
            {
                return "?";
            }
            switch (state.Status)
            {
                case CampaignNodeStatus.Completed:
                    return "✓";
                case CampaignNodeStatus.InProgress:
                    return "▶";
                case CampaignNodeStatus.Locked:
                    return "🔒";
                default:
                    return state.Node != null && state.Node.Optional ? "?" : "★";
            }
        }

        /// <summary>Kind class, so optional side content can be drawn differently from the main line.</summary>
        public const string LandmarkClass = "cm-node--landmark";

        /// <summary>The node's kind classes: main or optional, plus <see cref="LandmarkClass"/> when it
        /// has a landmark to draw (<c>CampaignNodeEntry.MapIcon</c>), space-separated.</summary>
        public static string KindClass(CampaignNodeEntry node)
        {
            string kind = node != null && node.Optional ? "cm-node--optional" : "cm-node--main";
            return node != null && node.MapIcon != null ? kind + " " + LandmarkClass : kind;
        }

        /// <summary>
        /// Fills <paramref name="nodes"/> and <paramref name="edges"/> for the visible part of the
        /// campaign. Hidden (secret, still locked) nodes are omitted entirely - and so is any edge
        /// touching one, or the map would leak the existence of the secret it is hiding.
        /// </summary>
        public static void BuildViewModel(
            CampaignSO campaign,
            IReadOnlyList<CampaignNodeState> states,
            List<SphereGridView.NodeInfo> nodes,
            List<(string A, string B)> edges)
        {
            nodes.Clear();
            edges.Clear();
            if (campaign == null || states == null)
            {
                return;
            }

            var positions = ResolvePositions(campaign);
            var visible = new HashSet<string>();

            foreach (var state in states)
            {
                if (state?.Node?.Run == null || !ShownOnMap(campaign, state))
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(state.Node.Run);
                if (!visible.Add(key))
                {
                    continue;
                }
                nodes.Add(new SphereGridView.NodeInfo
                {
                    Key = key,
                    Position = positions.TryGetValue(key, out var p) ? p : Vector2.zero,
                    KindClass = KindClass(state.Node),
                    Glyph = Glyph(state),
                    // A level with a landmark draws it, and its state mark rides in the corner.
                    Icon = state.Node.MapIcon,
                    Badge = state.Node.MapIcon != null ? Glyph(state) : null,
                    // Every visible run is named on the map itself, not only in the side panel
                    // after it is clicked. Secret runs are not visible at all, so a name here never
                    // spoils one.
                    Caption = CampaignOps.DisplayNameOf(state.Node.Run),
                    IsStart = state.Node.Requires == null || state.Node.Requires.Count == 0
                });
            }

            foreach (var state in states)
            {
                var node = state?.Node;
                if (node?.Run == null || !ShownOnMap(campaign, state) || node.Requires == null)
                {
                    continue;
                }
                string to = CampaignOps.RunKeyOf(node.Run);
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite == null)
                    {
                        continue;
                    }
                    string from = CampaignOps.RunKeyOf(prerequisite);
                    if (visible.Contains(from) && visible.Contains(to))
                    {
                        edges.Add((from, to));
                    }
                }
            }
        }

        /// <summary>
        /// Where each node sits on the map. Authored <see cref="CampaignNodeEntry.MapPosition"/> wins;
        /// a campaign that has authored none gets a tidy generated layout instead of every node piled
        /// on the origin - tier (longest prerequisite chain) left to right, branches stacked within a
        /// tier. Authoring positions is therefore a polish step, not a prerequisite for playing.
        /// </summary>
        /// <summary>
        /// Whether a level is drawn on the story map: visible by the campaign's rules (a secret one
        /// only once it opens) and, unless <c>CampaignSO.ShowLockedLevels</c>, open, underway or cleared
        /// - a level the player cannot play yet is not on the map (owner, 2026-11-22).
        /// </summary>
        public static bool ShownOnMap(CampaignSO campaign, CampaignNodeState state)
        {
            if (state == null || !state.IsVisible)
            {
                return false;
            }
            return (campaign != null && campaign.ShowLockedLevels) || state.Status != CampaignNodeStatus.Locked;
        }

        // Scenery is drawn this many times its sprite's pixel height, in map units.
        private const float SceneryPixelScale = 1.25f;

        /// <summary>Where the story map's ground reaches: every level (visible or not), every placed
        /// feature and every river point, plus a margin (<see cref="CampaignScenery.Area"/>), so a branch
        /// opening does not move the map's edge and nothing placed runs off the land.</summary>
        public static Rect SceneryArea(CampaignSO campaign)
        {
            if (campaign == null)
            {
                return new Rect();
            }
            var points = new List<Vector2>(ResolvePositions(campaign).Values);
            if (campaign.MapFeatures != null)
            {
                foreach (var feature in campaign.MapFeatures)
                {
                    if (feature != null)
                    {
                        points.Add(feature.Position);
                    }
                }
            }
            if (campaign.MapRivers != null)
            {
                foreach (var river in campaign.MapRivers)
                {
                    if (river?.Points != null)
                    {
                        points.AddRange(river.Points);
                    }
                }
            }
            return CampaignScenery.Area(points);
        }

        /// <summary>
        /// The whole land under the story map for the levels on show: the ground, the hand-placed
        /// features and rivers (<c>CampaignSO.MapFeatures</c> / <c>MapRivers</c>) and the scattered
        /// scenery, which keeps off every level, trail, feature and river. Standing pieces come
        /// sorted, further down the map last.
        /// </summary>
        public static SphereGridView.Backdrop BuildBackdrop(CampaignSO campaign,
            IReadOnlyList<SphereGridView.NodeInfo> nodes, IReadOnlyList<(string A, string B)> edges)
        {
            var backdrop = new SphereGridView.Backdrop();
            if (campaign == null)
            {
                return backdrop;
            }
            backdrop.Ground = campaign.MapGround;
            backdrop.TileSize = campaign.MapGroundTileSize;
            backdrop.Area = SceneryArea(campaign);

            var clear = new List<(Vector2 Center, float Radius)>();
            if (campaign.MapFeatures != null)
            {
                foreach (var feature in campaign.MapFeatures)
                {
                    if (feature?.Sprite == null)
                    {
                        continue;
                    }
                    var rect = feature.Sprite.rect;
                    float height = rect.height * SceneryPixelScale * feature.Scale;
                    float width = rect.height > 0f ? height * rect.width / rect.height : height;
                    var decoration = new SphereGridView.Decoration
                    {
                        Sprite = feature.Sprite,
                        Position = feature.Position,
                        Height = height,
                        Centred = feature.Ground
                    };
                    var centre = feature.Ground ? feature.Position : feature.Position - new Vector2(0f, height * 0.5f);
                    clear.Add((centre, Mathf.Max(width, height) * 0.45f));
                    (feature.Ground ? backdrop.Patches : backdrop.Standing).Add(decoration);
                }
            }
            if (campaign.MapRivers != null)
            {
                foreach (var river in campaign.MapRivers)
                {
                    if (river?.Points == null || river.Points.Count < 2)
                    {
                        continue;
                    }
                    var points = new List<Vector2>(river.Points);
                    AddRiverCaps(campaign, river, points, backdrop.RiverCaps, clear);
                    backdrop.Rivers.Add(new SphereGridView.Stroke
                    {
                        Points = points,
                        Width = river.Width,
                        Texture = campaign.MapRiverStrip
                    });
                    // The banks clear the trees: a circle every few units along the curve.
                    foreach (var point in SphereGridView.SmoothPath(river.Points, 4))
                    {
                        clear.Add((point, river.Width * (campaign.MapRiverStrip != null ? 1.15f : 0.5f) + 12f));
                    }
                }
            }

            backdrop.Standing.AddRange(BuildScenery(campaign, nodes, edges, clear));
            backdrop.Standing.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));
            return backdrop;
        }

        // How far a river runs straight out of (or into) its cap before it may bend, in map units.
        private const float CapStraight = 24f;

        /// <summary>
        /// A river's source and mouth (<c>CampaignMapRiver.Source</c> / <c>Mouth</c>): each cap is drawn
        /// at the river strip's own pixel scale with its joining edge on the river's end point, and the
        /// river is extended under the cap's stub and held straight for a moment as it leaves, so the
        /// strip and the cap's stub of the same strip line up.
        /// </summary>
        private static void AddRiverCaps(CampaignSO campaign, CampaignMapRiver river, List<Vector2> points,
            List<SphereGridView.Decoration> caps, List<(Vector2 Center, float Radius)> clear)
        {
            float stripPixels = campaign.MapRiverStrip != null ? campaign.MapRiverStrip.height : 32f;
            float unitsPerPixel = river.Width * SphereGridView.RiverBandPerWater / Mathf.Max(1f, stripPixels);

            var source = campaign.FindRiverCap(river.Source);
            if (source != null && !source.Mouth)
            {
                var flow = CampaignMapRiverCap.Direction(source.Flow);
                var start = points[0];
                float under = CapUnder(source, unitsPerPixel);
                points.Insert(1, start + flow * CapStraight);
                points.Insert(0, start - flow * under);
                caps.Add(CapDecoration(source, start, flow, unitsPerPixel, clear));
            }

            var mouth = campaign.FindRiverCap(river.Mouth);
            if (mouth != null && mouth.Mouth)
            {
                var flow = CampaignMapRiverCap.Direction(mouth.Flow);
                var end = points[points.Count - 1];
                float under = CapUnder(mouth, unitsPerPixel);
                points.Insert(points.Count - 1, end - flow * CapStraight);
                points.Add(end + flow * under);
                caps.Add(CapDecoration(mouth, end, flow, unitsPerPixel, clear));
            }
        }

        // How far the river runs on under a cap: a quarter of the cap, where its own stub of the strip lies.
        private static float CapUnder(CampaignMapRiverCap cap, float unitsPerPixel)
        {
            return cap.Sprite.rect.width * 0.25f * unitsPerPixel;
        }

        private static SphereGridView.Decoration CapDecoration(CampaignMapRiverCap cap, Vector2 at, Vector2 flow,
            float unitsPerPixel, List<(Vector2 Center, float Radius)> clear)
        {
            var rect = cap.Sprite.rect;
            float height = rect.height * unitsPerPixel;
            float width = rect.width * unitsPerPixel;
            // The joining edge's middle: a source's on the side the water leaves by, a mouth's on the
            // side it comes in from.
            var side = cap.Mouth ? -flow : flow;
            var pivot = new Vector2(0.5f + side.x * 0.5f, 0.5f + side.y * 0.5f);
            var centre = at + new Vector2((0.5f - pivot.x) * width, (0.5f - pivot.y) * height);
            clear.Add((centre, Mathf.Max(width, height) * 0.5f));
            return new SphereGridView.Decoration
            {
                Sprite = cap.Sprite,
                Position = at,
                Height = height,
                UsePivot = true,
                Pivot = pivot
            };
        }

        /// <summary>
        /// The scenery around the levels on show: <see cref="CampaignScenery.Scatter"/> over
        /// <see cref="SceneryArea"/>, kept off every visible level and trail and off
        /// <paramref name="clear"/>, as decorations the view can draw. Empty when the campaign has no
        /// scenery sprites.
        /// </summary>
        public static List<SphereGridView.Decoration> BuildScenery(CampaignSO campaign,
            IReadOnlyList<SphereGridView.NodeInfo> nodes, IReadOnlyList<(string A, string B)> edges,
            IReadOnlyList<(Vector2 Center, float Radius)> clear = null)
        {
            var decorations = new List<SphereGridView.Decoration>();
            var kinds = new List<Sprite>();
            if (campaign != null && campaign.MapScenery != null)
            {
                kinds.AddRange(campaign.MapScenery);
            }
            int woodKinds = kinds.Count;
            if (campaign != null && campaign.MapSceneryOpen != null)
            {
                kinds.AddRange(campaign.MapSceneryOpen);
            }
            if (campaign == null || kinds.Count == 0 || nodes == null)
            {
                return decorations;
            }
            var levels = new List<Vector2>();
            var at = new Dictionary<string, Vector2>();
            foreach (var node in nodes)
            {
                levels.Add(node.Position);
                at[node.Key] = node.Position;
            }
            var trails = new List<(Vector2 A, Vector2 B)>();
            if (edges != null)
            {
                foreach (var edge in edges)
                {
                    if (at.TryGetValue(edge.A, out var a) && at.TryGetValue(edge.B, out var b))
                    {
                        trails.Add((a, b));
                    }
                }
            }
            var pieces = CampaignScenery.Scatter(SceneryArea(campaign), levels, trails,
                woodKinds, kinds.Count - woodKinds, campaign.MapSceneryDensity, campaign.MapScenerySeed, clear);
            foreach (var piece in pieces)
            {
                var sprite = kinds[piece.Kind];
                if (sprite == null)
                {
                    continue;
                }
                decorations.Add(new SphereGridView.Decoration
                {
                    Sprite = sprite,
                    Position = piece.Position,
                    Height = sprite.rect.height * SceneryPixelScale * piece.Scale
                });
            }
            return decorations;
        }

        public static Dictionary<string, Vector2> ResolvePositions(CampaignSO campaign)
        {
            var positions = new Dictionary<string, Vector2>();
            if (campaign == null)
            {
                return positions;
            }

            bool anyAuthored = false;
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null && node.MapPosition != Vector2.zero)
                {
                    anyAuthored = true;
                    break;
                }
            }

            if (anyAuthored)
            {
                foreach (var node in campaign.Nodes)
                {
                    if (node?.Run != null)
                    {
                        positions[CampaignOps.RunKeyOf(node.Run)] = node.MapPosition;
                    }
                }
                return positions;
            }

            var tiers = CampaignOps.ComputeTiers(campaign);
            var usedPerTier = new Dictionary<int, int>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run == null)
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(node.Run);
                int tier = tiers.TryGetValue(key, out var t) ? t : 0;
                usedPerTier.TryGetValue(tier, out int row);
                usedPerTier[tier] = row + 1;
                positions[key] = new Vector2(tier * TierSpacing, row * BranchSpacing);
            }
            return positions;
        }

    }
}
