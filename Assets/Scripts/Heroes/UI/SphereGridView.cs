using System;
using System.Collections.Generic;
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
        }

        public const float NodeRadius = 22f;
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 2.5f;

        // Edge stroke colors are code-side because Painter2D cannot read USS. They mirror the
        // --cd-* theme values in CardDungeon.uss; change them together.
        private static readonly Color EdgeActivated = new Color(128f / 255f, 204f / 255f, 1f);        // --cd-accent
        private static readonly Color EdgeAvailable = new Color(135f / 255f, 128f / 255f, 165f / 255f); // --cd-stone-hover
        private static readonly Color EdgeDim = new Color(58f / 255f, 42f / 255f, 22f / 255f);          // --cd-frame
        private static readonly Color GhostEdge = new Color(216f / 255f, 198f / 255f, 154f / 255f, 0.7f); // --cd-parchment

        private readonly VisualElement _content;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, NodeInfo> _nodes = new Dictionary<string, NodeInfo>();
        private readonly Dictionary<string, string> _stateClasses = new Dictionary<string, string>();
        private readonly List<(string A, string B)> _edges = new List<(string A, string B)>();

        private string _selectedKey;
        private string _ghostFromKey;
        private Vector2 _ghostTo;
        private bool _ghostVisible;

        private float _zoom = 1f;
        private Vector2 _pan = Vector2.zero;
        private bool _panning;
        private int _panPointerId = -1;
        private Vector3 _panStartPointer;
        private Vector2 _panStartValue;

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

            _content = new VisualElement { name = "sg-content", pickingMode = PickingMode.Ignore };
            _content.AddToClassList("sg-content");
            _content.generateVisualContent += DrawEdges;
            Add(_content);

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndPan());
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
                button.AddToClassList(node.KindClass);
            }
            if (node.IsStart)
            {
                button.AddToClassList("sg-node--start");
            }

            var glyph = new Label(node.Glyph) { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("sg-node__glyph");
            button.Add(glyph);

            PositionButton(button, node.Position, node.IsStart);

            string captured = node.Key;
            button.clicked += () => NodeClicked?.Invoke(captured);
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                NodePointerDown?.Invoke(captured, evt);
            });

            return button;
        }

        private static void PositionButton(Button button, Vector2 gridPosition, bool isStart)
        {
            float radius = isStart ? NodeRadius + 5f : NodeRadius;
            button.style.left = gridPosition.x - radius;
            button.style.top = gridPosition.y - radius;
        }

        // --- state -----------------------------------------------------------

        private static readonly string[] SphereGridStateClasses =
        {
            "sg-node--activated", "sg-node--available", "sg-node--adjacent", "sg-node--locked"
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

        /// <summary>Fits every node inside the viewport (call after Show/SetGraph, once laid out).</summary>
        public void FrameAll()
        {
            if (_nodes.Count == 0)
            {
                _pan = Vector2.zero;
                Zoom = 1f;
                return;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in _nodes.Values)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
            min -= new Vector2(NodeRadius * 2f, NodeRadius * 2f);
            max += new Vector2(NodeRadius * 2f, NodeRadius * 2f);

            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                // Not laid out yet — try again once geometry exists.
                RegisterCallbackOnce<GeometryChangedEvent>(_ => FrameAll());
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
            _zoom = Mathf.Clamp(zoom, 0.05f, MaxZoom);

            var center = (min + max) * 0.5f;
            _pan = new Vector2(width * 0.5f, height * 0.5f) - center * _zoom;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector2(_zoom, _zoom));
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

                painter.strokeColor = EdgeColorFor(edge.A, edge.B);
                painter.BeginPath();
                painter.MoveTo(a.Position);
                painter.LineTo(b.Position);
                painter.Stroke();
            }

            if (_ghostVisible && _nodes.TryGetValue(_ghostFromKey, out var from))
            {
                painter.strokeColor = GhostEdge;
                painter.BeginPath();
                painter.MoveTo(from.Position);
                painter.LineTo(_ghostTo);
                painter.Stroke();
            }
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
