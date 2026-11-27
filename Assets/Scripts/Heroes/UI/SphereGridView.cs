using System;
using System.Collections.Generic;
using System.Linq;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Heroes.UI
{
    /// <summary>
    /// The one graph renderer both sphere-grid surfaces share: the hub screen shows it read-only
    /// with an Activate flow, and the editor window wraps it with drag/connect/edit tooling — so
    /// the player sees exactly the shape the designer authored.
    ///
    /// <para>Pure view: it owns rendering, pan/zoom and hit events, and holds no game logic, no
    /// save access and no editing behaviour. Callers push graph shape via <see cref="SetGraph"/>,
    /// state classes via <see cref="SetNodeState"/> (from <c>SphereGridPresenter</c>), and compose
    /// interactions from the pointer events. Grid space is content-local pixels at zoom 1 (y-down),
    /// so the editor and the runtime render byte-identical layouts.</para>
    ///
    /// <para>Edges are drawn with Painter2D in content-local coordinates on the same element the
    /// nodes live in, so the pan/zoom transform applies to both identically — they cannot desync.
    /// Pan/zoom are style transforms (translate/scale), which move UITK's hit-testing with the
    /// render.</para>
    /// </summary>
    public sealed class SphereGridView : VisualElement
    {
        /// <summary>Everything the view needs to draw one node. State classes arrive separately.</summary>
        public struct NodeInfo
        {
            public string Key;
            public Vector2 Position;
            public string KindClass;
            public string Glyph;
            public bool IsStart;

            /// <summary>
            /// Optional art drawn in place of the glyph - the ability, summon or Ultra a node teaches
            /// (playtest 2026-10-19: those nodes read as empty). Null draws the glyph as before.
            /// </summary>
            public Sprite Icon;

            /// <summary>
            /// Optional small mark in the corner of an <see cref="Icon"/> - an upgrade node shows the
            /// art of what it upgrades with its "▲" or "+" here, so it still reads as an upgrade.
            /// </summary>
            public string Badge;

            /// <summary>
            /// Optional text drawn under the node. <b>Opt-in, and only the campaign map sets it</b>
            /// (the run's name - playtest 2026-09-28, finding 11): a sphere grid node is identified
            /// by its glyph and the detail panel, and a caption under every one of dozens of nodes
            /// would bury the graph. Null or empty draws nothing, so graphs that never set it render
            /// exactly as before.
            /// </summary>
            public string Caption;

            /// <summary>
            /// Keys of the nodes this one requires (<c>SphereGridNode.RequiredNodes</c>), drawn as a
            /// dashed gold link from each to this node - so a cross-branch condition can be seen, not
            /// only read in the detail panel. Null or empty draws nothing.
            /// </summary>
            public List<string> Requires;
        }

        public const float NodeRadius = 22f;

        /// <summary>One piece of scenery under the graph (the story map's trees and ruins): a sprite
        /// whose foot stands on <see cref="Position"/>, <see cref="Height"/> map units tall.</summary>
        public struct Decoration
        {
            public Sprite Sprite;
            public Vector2 Position;
            public float Height;

            /// <summary>Centred on <see cref="Position"/> instead of standing on it (a ground patch).</summary>
            public bool Centred;

            /// <summary>When set, the point of the sprite (0..1 across, 0..1 down) that sits on
            /// <see cref="Position"/> - a river cap's joining edge. Overrides <see cref="Centred"/>.</summary>
            public bool UsePivot;
            public Vector2 Pivot;
        }

        /// <summary>A river under the graph: a smooth curve through <see cref="Points"/>.</summary>
        public struct Stroke
        {
            public List<Vector2> Points;
            public float Width;

            /// <summary>Optional art laid along the curve: a horizontal strip of water and banks,
            /// seamless end to end. Null paints the plain river.</summary>
            public Texture2D Texture;
        }

        /// <summary>Everything <see cref="SetBackdrop"/> draws, bottom to top: the ground tile over
        /// <see cref="Area"/>, the ground patches, the rivers, then whatever stands (scenery and standing
        /// features, in the order given - further down the map last).</summary>
        public sealed class Backdrop
        {
            public Texture2D Ground;
            public float TileSize = 128f;
            public Rect Area;
            public readonly List<Decoration> Patches = new List<Decoration>();
            public readonly List<Stroke> Rivers = new List<Stroke>();

            /// <summary>Where rivers start and end (springs, lakes): drawn over the water, under what stands.</summary>
            public readonly List<Decoration> RiverCaps = new List<Decoration>();
            public readonly List<Decoration> Standing = new List<Decoration>();
        }

        /// <summary>Extra room <see cref="FrameAll"/> leaves when nodes carry captions: half a long name
        /// either side, and one caption's height below.</summary>
        private static readonly Vector2 CaptionFramePad = new Vector2(70f, 30f);
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 2.5f;

        // Edge stroke colors are code-side because Painter2D cannot read USS. They mirror the
        // --cd-* theme values in CardDungeon.uss; change them together.
        private static readonly Color EdgeActivated = new Color(205f / 255f, 110f / 255f, 255f / 255f); // --cd-accent
        private static readonly Color EdgeAvailable = new Color(104f / 255f, 47f / 255f, 165f / 255f);  // --cd-stone-hover
        private static readonly Color EdgeDim = new Color(91f / 255f, 42f / 255f, 145f / 255f);         // --cd-frame
        // Gold rather than --cd-parchment: parchment is a near-black ink in this theme, and a drag
        // line the designer cannot see is worse than no line at all.
        private static readonly Color GhostEdge = new Color(236f / 255f, 190f / 255f, 72f / 255f, 0.7f); // --cd-gold
        private static readonly Color RequirementMet = new Color(236f / 255f, 190f / 255f, 72f / 255f, 0.9f);  // --cd-gold
        private static readonly Color RequirementOpen = new Color(236f / 255f, 190f / 255f, 72f / 255f, 0.35f);
        private const float DashLength = 10f;
        private const float DashGap = 8f;

        private readonly VisualElement _content;

        // Under the content, panned and zoomed with it (SetBackdrop): a sibling rather than a child,
        // because the edges are painted on _content itself and its children would cover them.
        private readonly VisualElement _backdrop;
        private readonly VisualElement _stars;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, NodeInfo> _nodes = new Dictionary<string, NodeInfo>();

        // Scratch buffers for NodeInDirection, so walking a graph with the arrow keys allocates nothing.
        private readonly List<string> _navKeys = new List<string>();
        private readonly List<Vector2> _navPoints = new List<Vector2>();
        private readonly Dictionary<string, string> _stateClasses = new Dictionary<string, string>();
        private readonly List<(string A, string B)> _edges = new List<(string A, string B)>();

        private string _selectedKey;
        private string _hoveredKey;
        private string _ghostFromKey;
        private Vector2 _ghostTo;
        private bool _ghostVisible;

        private float _zoom = 1f;
        private Vector2 _pan = Vector2.zero;
        private bool _panning;
        private int _panPointerId = -1;
        private Vector3 _panStartPointer;
        private Vector2 _panStartValue;

        // Touch: every finger down on the view, in local space. Two of them pinch (zoom about their
        // midpoint, which also pans); the browser's own pinch is off in the WebGL template.
        private readonly Dictionary<int, Vector2> _touches = new Dictionary<int, Vector2>();
        private bool _pinching;
        private int _pinchA = -1;
        private int _pinchB = -1;
        private float _pinchStartDistance;
        private float _pinchStartZoom;
        private Vector2 _pinchGridAnchor;

        /// <summary>Fires when a node button is clicked. The runtime screen's only event.</summary>
        public event Action<string> NodeClicked;

        /// <summary>Raw pointer-down on a node — the editor composes drag and edge-connect from it.</summary>
        public event Action<string, PointerDownEvent> NodePointerDown;

        /// <summary>Pointer-down on empty canvas, in grid space — the editor places nodes with it.</summary>
        public event Action<Vector2, PointerDownEvent> BackgroundPointerDown;

        /// <summary>Bitmask of mouse buttons that pan (1 &lt;&lt; button index). Runtime: left+middle;
        /// the editor sets middle+right so left stays free for drag/connect.</summary>
        public int PanButtons { get; set; } = (1 << 0) | (1 << 2);

        public float Zoom
        {
            get { return _zoom; }
            set
            {
                _zoom = Mathf.Clamp(value, MinZoom, MaxZoom);
                ApplyTransform();
            }
        }

        /// <summary>
        /// Draws an arrowhead at the B end of every edge. Off by default: a sphere grid is undirected.
        /// The campaign map editor turns it on, because a campaign link reads "A opens B" and which end
        /// is which is the whole point of the line.
        /// </summary>
        public bool DrawArrows { get; set; }

        /// <summary>
        /// Draws every edge as a dotted trail instead of a line - the story map's roads between
        /// landmarks (2026-11-22). Off by default: the sphere grid keeps its lines.
        /// </summary>
        public bool TrailEdges { get; set; }

        /// <summary>
        /// The sphere grid's own look (2026-11-22, after FFX): every node a glass orb in an iron
        /// socket, coloured by its <c>sg-hue--*</c> class, owned orbs glowing, affordable ones
        /// pulsing, the lit paths glowing, all over a starry void with a star layer that drifts slower
        /// than the graph when it pans. The art is in USS (<c>Assets/UI/SphereGrid/</c>); this only
        /// adds the elements it hangs on. Set it before <see cref="SetGraph"/>. Off by default, so the
        /// story map keeps its landmarks.
        /// </summary>
        public bool OrbNodes
        {
            get { return _orbNodes; }
            set
            {
                if (_orbNodes == value)
                {
                    return;
                }
                _orbNodes = value;
                EnableInClassList("sg-viewport--orbs", value);
                _stars.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (value && _pulse == null)
                {
                    _pulse = schedule.Execute(() => ToggleInClassList("sg-pulse")).Every(PulseMs);
                }
                else if (!value && _pulse != null)
                {
                    _pulse.Pause();
                    _pulse = null;
                    RemoveFromClassList("sg-pulse");
                }
                _content.MarkDirtyRepaint();
            }
        }

        // How fast an affordable orb breathes (half a cycle), and how much slower than the graph the
        // star layer moves when it pans - the depth the void would not have as a flat picture.
        private const long PulseMs = 900;
        private const float StarParallax = 0.35f;
        private bool _orbNodes;
        private IVisualElementScheduledItem _pulse;

        public Vector2 Pan
        {
            get { return _pan; }
            set
            {
                _pan = value;
                ApplyTransform();
            }
        }

        public SphereGridView()
        {
            AddToClassList("sg-viewport");
            // Alias class so var(--cd-…) resolves when this sheet is loaded inside an editor
            // window's panel, where :root matching is not guaranteed.
            AddToClassList("sg-scope");
            focusable = false;

            // The drifting star layer (OrbNodes): fills the viewport and never scales, only its
            // tile offset follows the pan, so it reads as far behind the graph.
            _stars = new VisualElement { name = "sg-stars", pickingMode = PickingMode.Ignore };
            _stars.AddToClassList("sg-stars");
            _stars.style.display = DisplayStyle.None;
            Add(_stars);

            _backdrop = new VisualElement { name = "sg-backdrop", pickingMode = PickingMode.Ignore };
            _backdrop.AddToClassList("sg-content");
            Add(_backdrop);

            _content = new VisualElement { name = "sg-content", pickingMode = PickingMode.Ignore };
            _content.AddToClassList("sg-content");
            _content.generateVisualContent += DrawEdges;
            Add(_content);

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            // Only the pan's own pointer ends it: the pinch hands capture between fingers, and a
            // queued capture-out for another finger must not stop the pan that follows.
            RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId == _panPointerId)
                {
                    EndPan();
                }
            });

            // Trickle-down, so a finger that lands on a node button is still counted for a pinch.
            RegisterCallback<PointerDownEvent>(OnTouchDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnTouchMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnTouchUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnTouchCancel, TrickleDown.TrickleDown);
        }

        // --- population ------------------------------------------------------

        /// <summary>Replaces the whole graph. State classes reset to locked; push fresh ones after.</summary>
        public void SetGraph(IReadOnlyList<NodeInfo> nodes, IReadOnlyList<(string A, string B)> edges)
        {
            foreach (var button in _buttons.Values)
            {
                button.RemoveFromHierarchy();
            }
            _buttons.Clear();
            _nodes.Clear();
            _stateClasses.Clear();
            _edges.Clear();
            _selectedKey = null;
            _hoveredKey = null;
            _ghostVisible = false;

            if (edges != null)
            {
                _edges.AddRange(edges);
            }

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (string.IsNullOrEmpty(node.Key) || _buttons.ContainsKey(node.Key))
                    {
                        continue;
                    }

                    _nodes[node.Key] = node;
                    _buttons[node.Key] = MakeNodeButton(node);
                    _content.Add(_buttons[node.Key]);
                }
            }

            _content.MarkDirtyRepaint();
        }

        private Button MakeNodeButton(NodeInfo node)
        {
            var button = new Button { name = "sg-node-" + node.Key, focusable = false };
            button.RemoveFromClassList("unity-button");
            button.AddToClassList("sg-node");
            if (!string.IsNullOrEmpty(node.KindClass))
            {
                // Space-separated, so a node can carry more than one (the story map's
                // "cm-node--main cm-node--landmark").
                foreach (var kind in node.KindClass.Split(' '))
                {
                    if (kind.Length > 0)
                    {
                        button.AddToClassList(kind);
                    }
                }
            }
            if (node.IsStart)
            {
                button.AddToClassList("sg-node--start");
            }

            if (_orbNodes)
            {
                // Bottom to top: the start's sigil, the glow, the glass, the iron socket round it, the
                // shine, then the cursor ring; the glyph or icon goes over all of it below.
                if (node.IsStart)
                {
                    AddOrbLayer(button, "sg-node__sigil");
                }
                AddOrbLayer(button, "sg-node__halo");
                AddOrbLayer(button, "sg-node__orb");
                AddOrbLayer(button, "sg-node__socket");
                AddOrbLayer(button, "sg-node__gloss");
                AddOrbLayer(button, "sg-node__ring");
            }

            if (node.Icon != null)
            {
                var icon = new Image { sprite = node.Icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("sg-node__icon");
                button.Add(icon);
                if (!string.IsNullOrEmpty(node.Badge))
                {
                    var badge = new Label(node.Badge) { pickingMode = PickingMode.Ignore };
                    badge.AddToClassList("sg-node__badge");
                    button.Add(badge);
                }
            }
            else
            {
                var glyph = new Label(node.Glyph) { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList("sg-node__glyph");
                if (node.Glyph != null && node.Glyph.Length > 1)
                {
                    // A stat's short name ("STR") rather than a symbol: smaller, to fit the circle.
                    glyph.AddToClassList("sg-node__glyph--word");
                }
                button.Add(glyph);
            }

            if (!string.IsNullOrEmpty(node.Caption))
            {
                // A child of the button so it pans, zooms, dims and scales with its node; ignored by
                // picking so it never widens the node's click target.
                var caption = new Label(node.Caption) { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("sg-node__caption");
                button.Add(caption);
            }

            PositionButton(button, node.Position, node.IsStart);

            string captured = node.Key;
            button.clicked += () => NodeClicked?.Invoke(captured);
            // Trickle-down: the Button's own Clickable stops the press before later callbacks on the
            // same element hear it, which left the editors' drag dead (owner, 2026-11-22).
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                NodePointerDown?.Invoke(captured, evt);
            }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hoveredKey = captured;
                _content.MarkDirtyRepaint();
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_hoveredKey == captured)
                {
                    _hoveredKey = null;
                    _content.MarkDirtyRepaint();
                }
            });

            return button;
        }

        private static void AddOrbLayer(VisualElement button, string className)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("sg-orb-layer");
            layer.AddToClassList(className);
            button.Add(layer);
        }

        private static void PositionButton(Button button, Vector2 gridPosition, bool isStart)
        {
            float radius = isStart ? NodeRadius + 5f : NodeRadius;
            button.style.left = gridPosition.x - radius;
            button.style.top = gridPosition.y - radius;
        }

        /// <summary>
        /// The land under the graph, in grid space (the story map): see <see cref="Backdrop"/> for what
        /// is drawn and in which order. Opt-in; nothing is drawn until it is called, and null clears it.
        /// </summary>
        public void SetBackdrop(Backdrop backdrop)
        {
            _backdrop.Clear();
            _rivers.Clear();
            if (backdrop == null)
            {
                return;
            }
            if (backdrop.Ground != null)
            {
                var tiles = new VisualElement { pickingMode = PickingMode.Ignore };
                tiles.style.position = Position.Absolute;
                tiles.style.left = backdrop.Area.x;
                tiles.style.top = backdrop.Area.y;
                tiles.style.width = backdrop.Area.width;
                tiles.style.height = backdrop.Area.height;
                tiles.style.backgroundImage = new StyleBackground(backdrop.Ground);
                tiles.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                tiles.style.backgroundSize = new BackgroundSize(new Length(backdrop.TileSize), new Length(backdrop.TileSize));
                _backdrop.Add(tiles);
            }
            AddDecorations(backdrop.Patches);

            if (backdrop.Rivers.Count > 0)
            {
                _rivers.AddRange(backdrop.Rivers);
                var water = new VisualElement { pickingMode = PickingMode.Ignore };
                water.style.position = Position.Absolute;
                water.style.left = 0f;
                water.style.top = 0f;
                water.generateVisualContent += DrawRivers;
                _backdrop.Add(water);
            }

            AddDecorations(backdrop.RiverCaps);
            AddDecorations(backdrop.Standing);
        }

        private void AddDecorations(List<Decoration> decorations)
        {
            foreach (var decoration in decorations)
            {
                if (decoration.Sprite == null)
                {
                    continue;
                }
                var rect = decoration.Sprite.rect;
                float height = decoration.Height;
                float width = rect.height > 0f ? height * rect.width / rect.height : height;
                var image = new Image { sprite = decoration.Sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("sg-decoration");
                image.style.position = Position.Absolute;
                image.style.width = width;
                image.style.height = height;
                if (decoration.UsePivot)
                {
                    image.style.left = decoration.Position.x - width * decoration.Pivot.x;
                    image.style.top = decoration.Position.y - height * decoration.Pivot.y;
                }
                else
                {
                    image.style.left = decoration.Position.x - width * 0.5f;
                    image.style.top = decoration.Centred ? decoration.Position.y - height * 0.5f : decoration.Position.y - height;
                }
                _backdrop.Add(image);
            }
        }

        // River colours: a dark bank, the water, a lighter current down the middle.
        private static readonly Color RiverBank = new Color(0.16f, 0.2f, 0.14f);
        private static readonly Color RiverWater = new Color(0.22f, 0.36f, 0.45f);
        private static readonly Color RiverCurrent = new Color(0.36f, 0.52f, 0.6f);
        private readonly List<Stroke> _rivers = new List<Stroke>();

        private void DrawRivers(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            foreach (var river in _rivers)
            {
                var curve = SmoothPath(river.Points);
                if (curve.Count < 2)
                {
                    continue;
                }
                if (river.Texture != null)
                {
                    DrawTexturedRiver(context, curve, river.Width, river.Texture);
                    continue;
                }
                StrokePath(painter, curve, river.Width + 5f, RiverBank);
                StrokePath(painter, curve, river.Width, RiverWater);
                StrokePath(painter, curve, Mathf.Max(1.5f, river.Width * 0.3f), RiverCurrent);
            }
        }

        // A textured river's band is this many times its water width (the strip's banks around it),
        // and the strip is laid in sections of this many art pixels, each run along the curve in steps.
        public const float RiverBandPerWater = 2.3f;
        private const int RiverSectionPixels = 30;
        private const int RiverStepsPerSection = 6;

        /// <summary>
        /// The river strip laid along <paramref name="curve"/>: a band <paramref name="width"/> x
        /// <see cref="RiverBandPerWater"/> wide, its art running down the curve section by section, the
        /// strip's sections in order so its whole length plays before it repeats. Each edge is set
        /// square to the river's direction at that point, so the band bends with it.
        /// </summary>
        private static void DrawTexturedRiver(MeshGenerationContext context, List<Vector2> curve, float width, Texture2D texture)
        {
            float band = width * RiverBandPerWater;
            int sections = Mathf.Max(1, texture.width / RiverSectionPixels);
            float sectionLength = band * RiverSectionPixels / Mathf.Max(1f, texture.height);
            var samples = Resample(curve, sectionLength / RiverStepsPerSection);
            if (samples.Count < 2)
            {
                return;
            }

            // One mesh per section, so each maps its own slice of the strip.
            for (int start = 0, section = 0; start < samples.Count - 1; start += RiverStepsPerSection, section++)
            {
                int end = Mathf.Min(samples.Count - 1, start + RiverStepsPerSection);
                int count = end - start + 1;
                var mesh = context.Allocate(count * 2, (count - 1) * 6, texture);
                var region = mesh.uvRegion;
                float u0 = (section % sections) / (float)sections;
                for (int i = 0; i < count; i++)
                {
                    int k = start + i;
                    var tangent = samples[Mathf.Min(samples.Count - 1, k + 1)] - samples[Mathf.Max(0, k - 1)];
                    var normal = new Vector2(-tangent.y, tangent.x).normalized * (band * 0.5f);
                    float u = u0 + (i / (float)RiverStepsPerSection) / sections;
                    var uvTop = new Vector2(region.x + u * region.width, region.y + region.height);
                    var uvBottom = new Vector2(region.x + u * region.width, region.y);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(samples[k].x - normal.x, samples[k].y - normal.y, Vertex.nearZ), tint = Color.white, uv = uvTop });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(samples[k].x + normal.x, samples[k].y + normal.y, Vertex.nearZ), tint = Color.white, uv = uvBottom });
                }
                for (int i = 0; i < count - 1; i++)
                {
                    ushort a = (ushort)(i * 2);
                    mesh.SetNextIndex(a);
                    mesh.SetNextIndex((ushort)(a + 2));
                    mesh.SetNextIndex((ushort)(a + 1));
                    mesh.SetNextIndex((ushort)(a + 1));
                    mesh.SetNextIndex((ushort)(a + 2));
                    mesh.SetNextIndex((ushort)(a + 3));
                }
            }
        }

        /// <summary>Points every <paramref name="step"/> units along the path, the first and last kept.</summary>
        private static List<Vector2> Resample(List<Vector2> path, float step)
        {
            var points = new List<Vector2> { path[0] };
            float carried = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                var from = path[i - 1];
                var segment = path[i] - from;
                float length = segment.magnitude;
                if (length <= 0f)
                {
                    continue;
                }
                float at = step - carried;
                while (at <= length)
                {
                    points.Add(from + segment * (at / length));
                    at += step;
                }
                carried = length - (at - step);
            }
            if ((points[points.Count - 1] - path[path.Count - 1]).sqrMagnitude > 1f)
            {
                points.Add(path[path.Count - 1]);
            }
            return points;
        }

        private static void StrokePath(Painter2D painter, List<Vector2> path, float width, Color color)
        {
            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(path[0]);
            for (int i = 1; i < path.Count; i++)
            {
                painter.LineTo(path[i]);
            }
            painter.Stroke();
        }

        /// <summary>A Catmull-Rom curve through the points, so a river bends instead of kinking.</summary>
        public static List<Vector2> SmoothPath(IReadOnlyList<Vector2> points, int steps = 8)
        {
            var path = new List<Vector2>();
            if (points == null || points.Count == 0)
            {
                return path;
            }
            if (points.Count < 3)
            {
                path.AddRange(points);
                return path;
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    path.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            path.Add(points[points.Count - 1]);
            return path;
        }

        // --- state -----------------------------------------------------------

        private static readonly string[] SphereGridStateClasses =
        {
            "sg-node--activated", "sg-node--available", "sg-node--adjacent", "sg-node--locked", "sg-node--gated"
        };

        /// <summary>
        /// The mutually exclusive state classes <see cref="SetNodeState"/> swaps between - it removes
        /// every name in this set before adding the one asked for. Settable because this widget draws
        /// more than one kind of graph: the campaign map reuses it with its own <c>cm-node--*</c>
        /// vocabulary, and the two must not clear each other's classes.
        /// </summary>
        public string[] StateClassNames { get; set; } = SphereGridStateClasses;

        /// <summary>
        /// The state class an edge treats as "both ends done" (drawn in the accent colour), and the
        /// one it treats as "reachable from here" (drawn brighter than dim). Settable for the same
        /// reason as <see cref="StateClassNames"/> - edge tint has to be decided in code because
        /// Painter2D cannot read USS.
        /// </summary>
        public string EdgeStrongStateClass { get; set; } = "sg-node--activated";

        /// <inheritdoc cref="EdgeStrongStateClass"/>
        public string EdgeOpenStateClass { get; set; } = "sg-node--available";

        /// <summary>Swaps the node's state class (one of <see cref="StateClassNames"/>).</summary>
        public void SetNodeState(string key, string stateClass)
        {
            if (!_buttons.TryGetValue(key, out var button))
            {
                return;
            }

            foreach (var name in StateClassNames)
            {
                button.RemoveFromClassList(name);
            }
            if (!string.IsNullOrEmpty(stateClass))
            {
                button.AddToClassList(stateClass);
            }
            _stateClasses[key] = stateClass;
            _content.MarkDirtyRepaint();
        }

        /// <summary>Marks one node selected (null clears the selection).</summary>
        public void SetSelected(string key)
        {
            if (_selectedKey != null && _buttons.TryGetValue(_selectedKey, out var previous))
            {
                previous.RemoveFromClassList("sg-node--selected");
            }
            _selectedKey = key;
            if (key != null && _buttons.TryGetValue(key, out var current))
            {
                current.AddToClassList("sg-node--selected");
            }
            // Requirement links follow the focused node, so a new selection redraws them.
            _content.MarkDirtyRepaint();
        }

        /// <summary>Adds/removes an arbitrary class on one node (editor: connect-mode source).</summary>
        public void SetNodeFlag(string key, string className, bool present)
        {
            if (_buttons.TryGetValue(key, out var button))
            {
                if (present)
                {
                    button.AddToClassList(className);
                }
                else
                {
                    button.RemoveFromClassList(className);
                }
            }
        }

        /// <summary>Live drag support: repositions one node and redraws its edges.</summary>
        public void SetNodePosition(string key, Vector2 gridPosition)
        {
            if (!_nodes.TryGetValue(key, out var node) || !_buttons.TryGetValue(key, out var button))
            {
                return;
            }

            node.Position = gridPosition;
            _nodes[key] = node;
            PositionButton(button, gridPosition, node.IsStart);
            _content.MarkDirtyRepaint();
        }

        /// <summary>Connect-mode preview: a line from a node to a grid-space point (null hides it).</summary>
        public void SetGhostEdge(string fromKey, Vector2? gridPosition)
        {
            _ghostVisible = gridPosition.HasValue && !string.IsNullOrEmpty(fromKey) && _nodes.ContainsKey(fromKey);
            _ghostFromKey = fromKey;
            _ghostTo = gridPosition ?? Vector2.zero;
            _content.MarkDirtyRepaint();
        }

        // --- coordinates & camera ----------------------------------------------

        /// <summary>Panel-space position → grid space (inverse of the pan/zoom transform).</summary>
        public Vector2 ToGridSpace(Vector2 panelPosition)
        {
            var local = this.WorldToLocal(panelPosition);
            return (local - _pan) / _zoom;
        }

        // ------------------------------------------------------------------
        //  Keyboard navigation
        // ------------------------------------------------------------------

        /// <summary>
        /// The node lying in <paramref name="direction"/> from <paramref name="fromKey"/>, or null
        /// when nothing does. Grid space shares UI Toolkit's downward y, so "up" is <c>(0,-1)</c>.
        ///
        /// <para>It lives on the widget rather than in the two screens that use it because the widget
        /// is what holds the node set - a screen asking this question would otherwise have to keep a
        /// parallel copy of the graph's geometry in sync with the one being drawn.</para>
        /// </summary>
        public string NodeInDirection(string fromKey, Vector2 direction)
        {
            if (_nodes.Count == 0)
            {
                return null;
            }

            // Follow the graph's own edges first: a node connected to this one that lies that way is
            // what the player means. Picking from every node let the cursor leap across a gap to an
            // unconnected node on another branch (revisit playtest finding 11). Only when no
            // neighbour lies that way does the pick fall back to the whole graph.
            if (!string.IsNullOrEmpty(fromKey) && _nodes.TryGetValue(fromKey, out var origin))
            {
                _navKeys.Clear();
                _navPoints.Clear();
                foreach (var edge in _edges)
                {
                    string other = edge.A == fromKey ? edge.B : edge.B == fromKey ? edge.A : null;
                    if (other != null && _nodes.TryGetValue(other, out var neighbour) && !_navKeys.Contains(other))
                    {
                        _navKeys.Add(other);
                        _navPoints.Add(neighbour.Position);
                    }
                }
                int viaEdge = DirectionalNav.PickInDirection(_navPoints, origin.Position, direction);
                if (viaEdge >= 0)
                {
                    return _navKeys[viaEdge];
                }
            }

            _navKeys.Clear();
            _navPoints.Clear();
            int from = -1;
            foreach (var pair in _nodes)
            {
                if (pair.Key == fromKey)
                {
                    from = _navKeys.Count;
                }
                _navKeys.Add(pair.Key);
                _navPoints.Add(pair.Value.Position);
            }

            // With nothing selected yet, any arrow simply lands on a node rather than doing nothing.
            int target = from >= 0
                ? DirectionalNav.PickInDirection(_navPoints, from, direction)
                : 0;
            if (target >= 0)
            {
                return _navKeys[target];
            }

            // Last resort: the connected node that lies most nearly the way pressed, anywhere within
            // 90 degrees. Both cones can miss on a sparse branch, and the cursor then sat on a node
            // near the start and would not move (playtest 2026-10-30).
            if (from >= 0)
            {
                var originPoint = _navPoints[from];
                _navKeys.Clear();
                _navPoints.Clear();
                foreach (var edge in _edges)
                {
                    string other = edge.A == fromKey ? edge.B : edge.B == fromKey ? edge.A : null;
                    if (other != null && _nodes.TryGetValue(other, out var neighbour) && !_navKeys.Contains(other))
                    {
                        _navKeys.Add(other);
                        _navPoints.Add(neighbour.Position);
                    }
                }
                int loose = DirectionalNav.PickInHalfPlane(_navPoints, originPoint, direction);
                if (loose >= 0)
                {
                    return _navKeys[loose];
                }
            }
            return null;
        }

        /// <summary>
        /// Pans the least amount needed to bring a node inside the viewport, leaving zoom alone
        /// (fitting the whole graph is <see cref="FrameAll"/>'s job). Keyboard navigation needs this:
        /// the cursor can walk off the visible area, and a player who is not using the mouse has no
        /// way to drag the graph back.
        /// </summary>
        public void EnsureNodeVisible(string key)
        {
            if (string.IsNullOrEmpty(key) || !_nodes.TryGetValue(key, out var node))
            {
                return;
            }

            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }

            float margin = NodeRadius * 2f;
            var local = node.Position * _zoom + _pan;
            var pan = _pan;
            pan.x += Mathf.Max(0f, margin - local.x) - Mathf.Max(0f, local.x - (width - margin));
            pan.y += Mathf.Max(0f, margin - local.y) - Mathf.Max(0f, local.y - (height - margin));
            Pan = pan;
        }

        /// <summary>Moves the view by <paramref name="delta"/> panel pixels (the story map's WASD).</summary>
        public void PanBy(Vector2 delta)
        {
            Pan = _pan + delta;
        }

        /// <summary>
        /// Zooms by <paramref name="factor"/> about the viewport's centre, the keyboard's counterpart of
        /// the wheel, which zooms about the cursor (the story map's + and -).
        /// </summary>
        public void ZoomByAtCentre(float factor)
        {
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }
            float newZoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            _pan = ZoomedPan(_pan, _zoom, newZoom, new Vector2(width * 0.5f, height * 0.5f));
            _zoom = newZoom;
            ApplyTransform();
        }

        /// <summary>
        /// The pan that keeps the graph point under <paramref name="anchor"/> (panel-local) where it is
        /// when the zoom goes from <paramref name="oldZoom"/> to <paramref name="newZoom"/>. Pure.
        /// </summary>
        public static Vector2 ZoomedPan(Vector2 pan, float oldZoom, float newZoom, Vector2 anchor)
        {
            if (oldZoom <= 0f)
            {
                return pan;
            }
            return anchor - (anchor - pan) * (newZoom / oldZoom);
        }

        /// <summary>
        /// Centres one node at <paramref name="zoom"/>. The tutorial uses it: a whole grid framed to
        /// fit shrinks every node to a speck, which is no way to point at one of them. Waits for
        /// layout the way <see cref="FrameAll"/> does.
        /// </summary>
        public void FrameNode(string key, float zoom = 1f)
        {
            if (string.IsNullOrEmpty(key) || !_nodes.TryGetValue(key, out var node))
            {
                return;
            }

            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                RegisterCallbackOnce<GeometryChangedEvent>(_ => FrameNode(key, zoom));
                return;
            }

            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _pan = new Vector2(width * 0.5f, height * 0.5f) - node.Position * _zoom;
            ApplyTransform();
        }

        /// <summary>Fits every node inside the viewport (call after Show/SetGraph, once laid out).</summary>
        public void FrameAll()
        {
            Frame(_nodes.Keys.ToList(), 0.05f, MaxZoom);
        }

        /// <summary>
        /// Fits just <paramref name="keys"/> - the sphere grid frames the player's frontier, since a
        /// whole grid fitted to the viewport shrinks every node to a speck. The zoom is held between
        /// <paramref name="minZoom"/> and <paramref name="maxZoom"/>, so a sprawling frontier stays
        /// legible (the rest is a drag away) and a tiny one does not fill the screen.
        /// </summary>
        public void FrameNodes(IEnumerable<string> keys, float minZoom, float maxZoom)
        {
            var list = keys != null ? keys.Where(k => k != null && _nodes.ContainsKey(k)).ToList() : new List<string>();
            if (list.Count == 0)
            {
                FrameAll();
                return;
            }
            Frame(list, minZoom, maxZoom);
        }

        private void Frame(List<string> keys, float minZoom, float maxZoom)
        {
            if (keys.Count == 0)
            {
                _pan = Vector2.zero;
                Zoom = 1f;
                return;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in keys.Select(k => _nodes[k]))
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
            min -= new Vector2(NodeRadius * 2f, NodeRadius * 2f);
            max += new Vector2(NodeRadius * 2f, NodeRadius * 2f);

            // Captions hang below their nodes and are wider than them, so a graph that has any needs
            // the extra room or the lowest and outermost names are cut off. Only graphs with captions
            // (the campaign map) pay for it; the sphere grid frames exactly as before.
            if (keys.Any(k => !string.IsNullOrEmpty(_nodes[k].Caption)))
            {
                min.x -= CaptionFramePad.x;
                max.x += CaptionFramePad.x;
                max.y += CaptionFramePad.y;
            }

            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                // Not laid out yet — try again once geometry exists.
                RegisterCallbackOnce<GeometryChangedEvent>(_ => Frame(keys, minZoom, maxZoom));
                return;
            }

            var size = max - min;
            float zoom = 1f;
            if (size.x > 0f && size.y > 0f)
            {
                zoom = Mathf.Min(width / size.x, height / size.y);
            }
            // Framing may zoom out past the wheel's floor — a big grid must fit whole; the floor
            // only stops the *user* zooming into oblivion.
            _zoom = Mathf.Clamp(zoom, minZoom, maxZoom);

            var center = (min + max) * 0.5f;
            _pan = new Vector2(width * 0.5f, height * 0.5f) - center * _zoom;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector2(_zoom, _zoom));
            _backdrop.style.translate = _content.style.translate;
            _backdrop.style.scale = _content.style.scale;
            if (_orbNodes)
            {
                // A repeating background wraps any offset, so the stars never run out.
                _stars.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(_pan.x * StarParallax));
                _stars.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, new Length(_pan.y * StarParallax));
            }
            _content.MarkDirtyRepaint();
        }

        // --- input ---------------------------------------------------------------

        private void OnWheel(WheelEvent evt)
        {
            // Zoom toward the cursor: keep the grid point under the mouse stationary.
            var mouseLocal = this.WorldToLocal(evt.mousePosition);
            float oldZoom = _zoom;
            float newZoom = Mathf.Clamp(oldZoom * (evt.delta.y < 0f ? 1.1f : 1f / 1.1f), MinZoom, MaxZoom);
            if (!Mathf.Approximately(oldZoom, newZoom))
            {
                _pan = mouseLocal - (mouseLocal - _pan) * (newZoom / oldZoom);
                _zoom = newZoom;
                ApplyTransform();
            }
            // Never let the wheel scroll an ancestor while the cursor is over the graph.
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            bool overNode = evt.target is VisualElement element && IsNodeElement(element);
            if (!overNode)
            {
                BackgroundPointerDown?.Invoke(ToGridSpace(evt.position), evt);
            }

            // Left-button presses on a node never pan: in the runtime they are clicks, in the
            // editor they are drags/connects (which is why the editor sets PanButtons to
            // middle+right, keeping left entirely free).
            bool panButton = (PanButtons & (1 << evt.button)) != 0;
            if (!_panning && panButton && !(overNode && evt.button == 0))
            {
                _panning = true;
                _panPointerId = evt.pointerId;
                _panStartPointer = evt.position;
                _panStartValue = _pan;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private static bool IsNodeElement(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains("sg-node"))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_panning && evt.pointerId == _panPointerId)
            {
                var delta = (Vector2)(evt.position - _panStartPointer);
                _pan = _panStartValue + delta;
                ApplyTransform();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_panning && evt.pointerId == _panPointerId)
            {
                this.ReleasePointer(evt.pointerId);
                EndPan();
            }
        }

        private void EndPan()
        {
            _panning = false;
            _panPointerId = -1;
        }

        // --- touch: pinch to zoom -------------------------------------------------------

        private void OnTouchDown(PointerDownEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch)
            {
                return;
            }
            _touches[evt.pointerId] = this.WorldToLocal(evt.position);
            if (!_pinching && _touches.Count >= 2)
            {
                BeginPinch();
                evt.StopPropagation();
            }
        }

        private void OnTouchMove(PointerMoveEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch || !_touches.ContainsKey(evt.pointerId))
            {
                return;
            }
            _touches[evt.pointerId] = this.WorldToLocal(evt.position);
            if (!_pinching)
            {
                return;
            }

            var a = _touches[_pinchA];
            var b = _touches[_pinchB];
            float distance = Vector2.Distance(a, b);
            if (_pinchStartDistance > 1f)
            {
                _zoom = Mathf.Clamp(_pinchStartZoom * distance / _pinchStartDistance, MinZoom, MaxZoom);
            }
            // Keep the grid point that was between the fingers between them.
            _pan = (a + b) * 0.5f - _pinchGridAnchor * _zoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnTouchUp(PointerUpEvent evt)
        {
            if (evt.pointerType == UnityEngine.UIElements.PointerType.touch)
            {
                EndTouch(evt.pointerId);
            }
        }

        private void OnTouchCancel(PointerCancelEvent evt)
        {
            if (evt.pointerType == UnityEngine.UIElements.PointerType.touch)
            {
                EndTouch(evt.pointerId);
            }
        }

        private void BeginPinch()
        {
            // A one-finger pan in progress gives way to the pinch.
            if (_panning)
            {
                int panId = _panPointerId;
                EndPan();
                if (this.HasPointerCapture(panId))
                {
                    this.ReleasePointer(panId);
                }
            }

            using (var keys = _touches.Keys.GetEnumerator())
            {
                keys.MoveNext();
                _pinchA = keys.Current;
                keys.MoveNext();
                _pinchB = keys.Current;
            }

            // Capturing both fingers takes them from any node button, so a pinch never clicks a node.
            this.CapturePointer(_pinchA);
            this.CapturePointer(_pinchB);

            var a = _touches[_pinchA];
            var b = _touches[_pinchB];
            _pinchStartDistance = Vector2.Distance(a, b);
            _pinchStartZoom = _zoom;
            _pinchGridAnchor = ((a + b) * 0.5f - _pan) / Mathf.Max(0.01f, _zoom);
            _pinching = true;
        }

        private void EndTouch(int pointerId)
        {
            _touches.Remove(pointerId);
            if (!_pinching || (pointerId != _pinchA && pointerId != _pinchB))
            {
                return;
            }

            _pinching = false;
            int remaining = pointerId == _pinchA ? _pinchB : _pinchA;
            if (this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }
            _pinchA = -1;
            _pinchB = -1;

            // The finger still down carries on as a one-finger pan from where the pinch left off.
            // It keeps its capture, so no capture-out arrives to end that pan.
            if (_touches.TryGetValue(remaining, out var position))
            {
                _panning = true;
                _panPointerId = remaining;
                _panStartPointer = this.LocalToWorld(position);
                _panStartValue = _pan;
            }
        }

        // --- edges ------------------------------------------------------------------

        private void DrawEdges(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineWidth = 3f / Mathf.Max(0.01f, _zoom);
            painter.lineCap = LineCap.Round;

            foreach (var edge in _edges)
            {
                if (!_nodes.TryGetValue(edge.A, out var a) || !_nodes.TryGetValue(edge.B, out var b))
                {
                    continue;
                }

                if (TrailEdges)
                {
                    DrawTrail(painter, a.Position, b.Position, TrailColorFor(edge.A, edge.B));
                    if (DrawArrows)
                    {
                        painter.fillColor = TrailColorFor(edge.A, edge.B);
                        painter.strokeColor = TrailColorFor(edge.A, edge.B);
                        DrawArrowhead(painter, a.Position, b.Position, b.IsStart ? NodeRadius + 5f : NodeRadius);
                    }
                    continue;
                }

                if (_orbNodes)
                {
                    DrawOrbEdge(painter, edge.A, edge.B, a.Position, b.Position);
                }
                else
                {
                    painter.strokeColor = EdgeColorFor(edge.A, edge.B);
                    painter.BeginPath();
                    painter.MoveTo(a.Position);
                    painter.LineTo(b.Position);
                    painter.Stroke();
                }

                if (DrawArrows)
                {
                    DrawArrowhead(painter, a.Position, b.Position, b.IsStart ? NodeRadius + 5f : NodeRadius);
                }
            }

            DrawRequirementLinks(painter);

            if (_ghostVisible && _nodes.TryGetValue(_ghostFromKey, out var from))
            {
                painter.strokeColor = GhostEdge;
                painter.BeginPath();
                painter.MoveTo(from.Position);
                painter.LineTo(_ghostTo);
                painter.Stroke();
            }
        }

        /// <summary>
        /// A dashed line from a required node to the node that needs it, with an arrowhead on the
        /// gated end: bright once the requirement is bought, faint until then. Drawn only for the node
        /// under the pointer or the selected one (either end of the link), because a cross-branch
        /// requirement drawn all the time is a long line across the whole grid.
        /// </summary>
        private void DrawRequirementLinks(Painter2D painter)
        {
            foreach (var node in _nodes.Values)
            {
                if (node.Requires == null)
                {
                    continue;
                }
                foreach (var key in node.Requires)
                {
                    if (!_nodes.TryGetValue(key, out var required) || !IsFocused(node.Key, key))
                    {
                        continue;
                    }
                    bool met = _stateClasses.TryGetValue(key, out var state) && state == EdgeStrongStateClass;
                    painter.strokeColor = met ? RequirementMet : RequirementOpen;
                    DrawDashed(painter, required.Position, node.Position);
                    DrawArrowhead(painter, required.Position, node.Position, node.IsStart ? NodeRadius + 5f : NodeRadius);
                }
            }
        }

        private bool IsFocused(string gatedKey, string requiredKey)
        {
            return gatedKey == _selectedKey || gatedKey == _hoveredKey
                || requiredKey == _selectedKey || requiredKey == _hoveredKey;
        }

        // A trail's footprints: a dot every TrailStep units, a dark rim under each so it reads on any
        // ground, stopping short of the landmarks at both ends.
        private const float TrailStep = 11f;
        private const float TrailDot = 2.6f;
        private const float TrailEndGap = 30f;
        private static readonly Color TrailRim = new Color(0.08f, 0.05f, 0.03f, 0.75f);
        private static readonly Color TrailWalked = new Color(0.98f, 0.86f, 0.55f);
        private static readonly Color TrailOpen = new Color(0.86f, 0.78f, 0.62f);
        private static readonly Color TrailDim = new Color(0.55f, 0.5f, 0.45f, 0.6f);

        private Color TrailColorFor(string a, string b)
        {
            string stateA = _stateClasses.TryGetValue(a, out var sa) ? sa : null;
            string stateB = _stateClasses.TryGetValue(b, out var sb) ? sb : null;
            if (stateA == EdgeStrongStateClass && stateB == EdgeStrongStateClass)
            {
                return TrailWalked;
            }
            if (stateA == EdgeStrongStateClass || stateA == EdgeOpenStateClass || stateB == EdgeOpenStateClass)
            {
                return TrailOpen;
            }
            return TrailDim;
        }

        private static void DrawTrail(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            var line = to - from;
            float length = line.magnitude;
            if (length <= TrailEndGap * 2f)
            {
                return;
            }
            var direction = line / length;
            float usable = length - TrailEndGap * 2f;
            int dots = Mathf.Max(1, Mathf.RoundToInt(usable / TrailStep));
            for (int i = 0; i <= dots; i++)
            {
                var at = from + direction * (TrailEndGap + usable * i / dots);
                painter.fillColor = TrailRim;
                painter.BeginPath();
                painter.Arc(at, TrailDot + 1.2f, 0f, 360f);
                painter.Fill();
                painter.fillColor = color;
                painter.BeginPath();
                painter.Arc(at, TrailDot, 0f, 360f);
                painter.Fill();
            }
        }

        private static void DrawDashed(Painter2D painter, Vector2 from, Vector2 to)
        {
            var line = to - from;
            float length = line.magnitude;
            if (length < 1f)
            {
                return;
            }
            var direction = line / length;
            for (float at = 0f; at < length; at += DashLength + DashGap)
            {
                painter.BeginPath();
                painter.MoveTo(from + direction * at);
                painter.LineTo(from + direction * Mathf.Min(length, at + DashLength));
                painter.Stroke();
            }
        }

        /// <summary>A filled arrowhead on the from-to line, its tip on the target node's rim.</summary>
        private void DrawArrowhead(Painter2D painter, Vector2 from, Vector2 to, float targetRadius)
        {
            var line = to - from;
            if (line.sqrMagnitude < 1f)
            {
                return;
            }

            var direction = line.normalized;
            var normal = new Vector2(-direction.y, direction.x);
            var tip = to - direction * (targetRadius + 2f);
            const float length = 14f;
            const float halfWidth = 7f;

            painter.fillColor = painter.strokeColor;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(tip - direction * length + normal * halfWidth);
            painter.LineTo(tip - direction * length - normal * halfWidth);
            painter.ClosePath();
            painter.Fill();
        }

        // OrbNodes paths: a walked path is a beam of light with a wide soft glow under a bright core;
        // one that leads somewhere buyable is a steady violet line; the rest a faint thread, so the
        // owned part of the grid lights up like FFX's activated spheres. Widths are screen pixels.
        private static readonly Color BeamGlow = new Color(205f / 255f, 110f / 255f, 1f, 0.22f);
        private static readonly Color BeamCore = new Color(240f / 255f, 214f / 255f, 1f);
        private static readonly Color PathOpen = new Color(150f / 255f, 92f / 255f, 214f / 255f, 0.95f);
        private static readonly Color PathDim = new Color(86f / 255f, 58f / 255f, 130f / 255f, 0.7f);

        private void DrawOrbEdge(Painter2D painter, string keyA, string keyB, Vector2 from, Vector2 to)
        {
            float pixel = 1f / Mathf.Max(0.01f, _zoom);
            string stateA = _stateClasses.TryGetValue(keyA, out var sa) ? sa : null;
            string stateB = _stateClasses.TryGetValue(keyB, out var sb) ? sb : null;
            if (stateA == EdgeStrongStateClass && stateB == EdgeStrongStateClass)
            {
                StrokeLine(painter, from, to, 11f * pixel, BeamGlow);
                StrokeLine(painter, from, to, 6f * pixel, BeamGlow);
                StrokeLine(painter, from, to, 2.5f * pixel, BeamCore);
            }
            else if (stateA == EdgeOpenStateClass || stateB == EdgeOpenStateClass
                     || stateA == EdgeStrongStateClass || stateB == EdgeStrongStateClass)
            {
                StrokeLine(painter, from, to, 2.5f * pixel, PathOpen);
            }
            else
            {
                StrokeLine(painter, from, to, 2f * pixel, PathDim);
            }
            // Leave the painter as the arrows and requirement links expect it.
            painter.lineWidth = 3f * pixel;
        }

        private static void StrokeLine(Painter2D painter, Vector2 from, Vector2 to, float width, Color color)
        {
            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private Color EdgeColorFor(string a, string b)
        {
            string stateA = _stateClasses.TryGetValue(a, out var sa) ? sa : null;
            string stateB = _stateClasses.TryGetValue(b, out var sb) ? sb : null;

            if (stateA == EdgeStrongStateClass && stateB == EdgeStrongStateClass)
            {
                return EdgeActivated;
            }
            if (stateA == EdgeOpenStateClass || stateB == EdgeOpenStateClass)
            {
                return EdgeAvailable;
            }
            return EdgeDim;
        }
    }
}
