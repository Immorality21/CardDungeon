using System.Collections.Generic;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Rooms.UI
{
    /// <summary>
    /// Draws the floor the player has walked: room boxes to scale, the doors between them, and one
    /// glyph per room saying what is still in it.
    ///
    /// <para>Pure view. It owns no knowledge rules - <see cref="DungeonMapOps"/> decides what may be
    /// drawn - and no game state; callers push a model through <see cref="SetModel"/>.</para>
    ///
    /// <para>Unlike the sphere grid, which lays an authored graph out in its own coordinate space and
    /// needs pan/zoom to walk it, a dungeon already <i>has</i> real 2D coordinates: every room is a
    /// rectangle at a grid position. So this draws the floor <b>to scale and fit to the panel</b>
    /// instead - the shape of the floor is the information, and a map you have to pan is a map you
    /// cannot read at a glance. That is the one deliberate divergence from the plan's "reuse
    /// SphereGridView" note; the Painter2D idiom is the same.</para>
    ///
    /// <para>Dungeon grid space is y-up and UI space is y-down, so the vertical axis is flipped here
    /// once, in <see cref="ToPixels"/>, and nowhere else.</para>
    /// </summary>
    public sealed class DungeonMapView : VisualElement
    {
        // Painter2D cannot read USS, so these mirror the --cd-* theme tokens in CardDungeon.uss.
        // Change them together.
        // Deliberately lighter than --cd-stone: the panel's own ground is a 35%-black wash over
        // --cd-parchment, and stone against that was too close to an unfilled outline to tell apart
        // at a glance - which is the one thing the map has to do.
        private static readonly Color ExploredFill = new Color(58f / 255f, 30f / 255f, 94f / 255f);
        private static readonly Color CurrentFill = new Color(104f / 255f, 47f / 255f, 165f / 255f);       // --cd-stone-hover
        private static readonly Color ExploredBorder = new Color(205f / 255f, 110f / 255f, 255f / 255f);   // --cd-accent
        private static readonly Color CurrentBorder = new Color(236f / 255f, 190f / 255f, 72f / 255f);     // --cd-gold
        private static readonly Color KnownBorder = new Color(91f / 255f, 42f / 255f, 145f / 255f);        // --cd-frame
        // The keyboard cursor, drawn as a ring *inside* the room's own border so the border can go
        // on saying what the room is. Parchment-light, matching .sg-node--selected on the sphere
        // grid - gold is already spoken for by the party's own room.
        private static readonly Color CursorRing = new Color(245f / 255f, 240f / 255f, 255f / 255f);
        private static readonly Color WalkedDoor = new Color(205f / 255f, 110f / 255f, 255f / 255f);
        private static readonly Color UnwalkedDoor = new Color(91f / 255f, 42f / 255f, 145f / 255f);

        private const float Padding = 12f;
        private const float MinScale = 3f;
        private const float MaxScale = 30f;

        private readonly List<Label> _glyphs = new List<Label>();
        private readonly Dictionary<int, Button> _hitBoxes = new Dictionary<int, Button>();

        // Scratch buffers for RoomInDirection, so walking the map with the arrows allocates nothing.
        private readonly List<int> _navIndices = new List<int>();
        private readonly List<Vector2> _navPoints = new List<Vector2>();

        private DungeonMapModel _model;
        private float _scale = 1f;
        private Vector2 _origin;
        private int _selected = -1;

        /// <summary>Raised with a room index when the player picks a room to travel to.</summary>
        public event System.Action<int> RoomChosen;

        public DungeonMapView()
        {
            AddToClassList("dm-viewport");
            // Ignore, not None: the element itself is not a click target, but the per-room hit boxes
            // parented to it still are.
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            // The fit depends on the panel's size, which UITK only knows after layout - and again
            // after every resize.
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        /// <summary>The room the keyboard cursor is on, or -1.</summary>
        public int SelectedRoom => _selected;

        /// <summary>Replaces what is drawn. Null or empty clears the map.</summary>
        public void SetModel(DungeonMapModel model)
        {
            _model = model;
            _selected = -1;
            Layout();
        }

        /// <summary>
        /// Puts the keyboard cursor on a room, or clears it with -1. Only a travellable room can hold
        /// it - a cursor sitting on somewhere the player cannot go would make Enter do nothing.
        /// </summary>
        public void SelectRoom(int index)
        {
            if (index >= 0 && !CanTravelTo(index))
            {
                return;
            }

            _selected = index;
            foreach (var pair in _hitBoxes)
            {
                pair.Value.EnableInClassList("dm-room--selected", pair.Key == index);
            }
            MarkDirtyRepaint();
        }

        /// <summary>The first travellable room, for the cursor's opening position.</summary>
        public int FirstTravellableRoom()
        {
            if (_model == null)
            {
                return -1;
            }

            foreach (var room in _model.Rooms)
            {
                if (room.CanTravel)
                {
                    return room.Index;
                }
            }
            return -1;
        }

        /// <summary>
        /// The travellable room that lies <paramref name="direction"/> of the cursor, using the same
        /// <see cref="DirectionalNav"/> maths as the menu buttons, the sphere grid and the doors of a
        /// room - so "up" means the same thing on every screen in the game. Measured from the party's
        /// own room when the cursor is not yet placed.
        /// </summary>
        public int RoomInDirection(Vector2 direction)
        {
            if (_model == null)
            {
                return -1;
            }

            _navIndices.Clear();
            _navPoints.Clear();
            int from = -1;
            foreach (var room in _model.Rooms)
            {
                if (!room.CanTravel)
                {
                    continue;
                }
                if (room.Index == _selected)
                {
                    from = _navIndices.Count;
                }
                _navIndices.Add(room.Index);
                _navPoints.Add(PixelCentre(room.Bounds));
            }

            if (_navIndices.Count == 0)
            {
                return -1;
            }

            int picked;
            if (from >= 0)
            {
                picked = DirectionalNav.PickInDirection(_navPoints, from, direction);
            }
            else
            {
                var origin = CurrentRoomCentre();
                picked = DirectionalNav.PickInDirection(_navPoints, origin, direction);
            }

            return picked >= 0 && picked < _navIndices.Count ? _navIndices[picked] : -1;
        }

        private Vector2 CurrentRoomCentre()
        {
            foreach (var room in _model.Rooms)
            {
                if (room.IsCurrent)
                {
                    return PixelCentre(room.Bounds);
                }
            }
            return contentRect.center;
        }

        private bool CanTravelTo(int index)
        {
            if (_model == null)
            {
                return false;
            }

            foreach (var room in _model.Rooms)
            {
                if (room.Index == index)
                {
                    return room.CanTravel;
                }
            }
            return false;
        }

        /// <summary>Recomputes the fit, repositions the glyphs and repaints.</summary>
        private void Layout()
        {
            var size = contentRect.size;
            if (_model == null || _model.Rooms.Count == 0 || size.x <= 0f || size.y <= 0f)
            {
                ClearGlyphs();
                ClearHitBoxes();
                MarkDirtyRepaint();
                return;
            }

            var extent = _model.Extent;
            float width = Mathf.Max(1, extent.width);
            float height = Mathf.Max(1, extent.height);
            float usableX = Mathf.Max(1f, size.x - Padding * 2f);
            float usableY = Mathf.Max(1f, size.y - Padding * 2f);

            _scale = Mathf.Clamp(Mathf.Min(usableX / width, usableY / height), MinScale, MaxScale);

            // Centre whatever the scale ended up showing, so a floor with one explored room sits in
            // the middle of the panel rather than in a corner.
            _origin = new Vector2(
                (size.x - width * _scale) * 0.5f,
                (size.y - height * _scale) * 0.5f);

            BuildGlyphs();
            BuildHitBoxes();
            MarkDirtyRepaint();
        }

        /// <summary>Grid-space point to panel pixels. The single place the y flip happens.</summary>
        private Vector2 ToPixels(float gridX, float gridY)
        {
            var extent = _model.Extent;
            return new Vector2(
                _origin.x + (gridX - extent.xMin) * _scale,
                _origin.y + (extent.yMax - gridY) * _scale);
        }

        private Rect PixelRect(RectInt bounds)
        {
            // yMax maps to the top edge once flipped.
            var topLeft = ToPixels(bounds.xMin, bounds.yMax);
            return new Rect(topLeft.x, topLeft.y, bounds.width * _scale, bounds.height * _scale);
        }

        private Vector2 PixelCentre(RectInt bounds)
        {
            return ToPixels(bounds.xMin + bounds.width * 0.5f, bounds.yMin + bounds.height * 0.5f);
        }

        /// <summary>
        /// Where the segment from <paramref name="from"/> (a point inside <paramref name="rect"/>)
        /// toward <paramref name="to"/> leaves the rectangle. A slab test: per axis the boundary in
        /// the direction of travel is the larger of the two crossings, and the first axis to be
        /// crossed is the smaller of those.
        /// </summary>
        private static Vector2 EdgePoint(Rect rect, Vector2 from, Vector2 to)
        {
            var direction = to - from;
            float t = 1f;

            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                t = Mathf.Min(t, Mathf.Max(
                    (rect.xMin - from.x) / direction.x,
                    (rect.xMax - from.x) / direction.x));
            }
            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                t = Mathf.Min(t, Mathf.Max(
                    (rect.yMin - from.y) / direction.y,
                    (rect.yMax - from.y) / direction.y));
            }

            return from + direction * Mathf.Clamp01(t);
        }

        // --- glyphs -------------------------------------------------------------------
        //
        // Painter2D draws no text, so each glyph is a Label child - the same "rows built from data"
        // pattern the turn-order list and the party window already use. Structure stays authored in
        // UXML; only these data marks are made in code.

        private void ClearGlyphs()
        {
            foreach (var label in _glyphs)
            {
                label.RemoveFromHierarchy();
            }
            _glyphs.Clear();
        }

        private void BuildGlyphs()
        {
            ClearGlyphs();

            foreach (var room in _model.Rooms)
            {
                var rect = PixelRect(room.Bounds);

                string glyph = DungeonMapOps.GlyphFor(room.Marker);
                if (!string.IsNullOrEmpty(glyph))
                {
                    AddGlyph(glyph, rect, MarkerClass(room.Marker), centred: true);
                }

                if (room.ShowsExit)
                {
                    AddGlyph(DungeonMapOps.ExitGlyph, rect, "dm-glyph--exit", centred: false);
                }
            }
        }

        // --- hit boxes ----------------------------------------------------------------
        //
        // One transparent Button per travellable room, laid over the painted box. Buttons rather
        // than a hit test on the view, for the same reason SphereGridView uses them for nodes: hover
        // and the click route come for free, and USS owns the feedback. They carry no border of their
        // own - the painted rectangle already draws the room's state, and a second frame on top read
        // as a doubled outline - so a hover is a wash of light and the cursor a wash of gold.
        //
        // Only travellable rooms get one: an unreachable room with a dead button still invites the
        // click, and the map is meant to answer "can I get there" before the player tries.

        private void ClearHitBoxes()
        {
            foreach (var pair in _hitBoxes)
            {
                pair.Value.RemoveFromHierarchy();
            }
            _hitBoxes.Clear();
        }

        private void BuildHitBoxes()
        {
            ClearHitBoxes();

            foreach (var room in _model.Rooms)
            {
                if (!room.CanTravel)
                {
                    continue;
                }

                var rect = PixelRect(room.Bounds);
                int index = room.Index;

                var button = new Button(() => RoomChosen?.Invoke(index));
                button.AddToClassList("dm-room");
                button.text = string.Empty;
                // Focusable buttons here would fight the panel root for focus, which is how the
                // combat screens lose the keyboard after one arrow - see the Rooms guide.
                button.focusable = false;
                button.style.left = rect.x;
                button.style.top = rect.y;
                button.style.width = rect.width;
                button.style.height = rect.height;

                Add(button);
                _hitBoxes[index] = button;
            }

            // A rebuild drops the cursor's highlight with the old buttons; put it back.
            if (_selected >= 0 && _hitBoxes.TryGetValue(_selected, out var selected))
            {
                selected.AddToClassList("dm-room--selected");
            }
            else
            {
                _selected = -1;
            }
        }

        private void AddGlyph(string text, Rect rect, string stateClass, bool centred)
        {
            var label = new Label(text);
            label.AddToClassList("dm-glyph");
            if (!string.IsNullOrEmpty(stateClass))
            {
                label.AddToClassList(stateClass);
            }
            label.pickingMode = PickingMode.Ignore;

            if (centred)
            {
                label.style.left = rect.x;
                label.style.top = rect.y;
                label.style.width = rect.width;
                label.style.height = rect.height;
            }
            else
            {
                // Top-right corner: the exit tick never covers the room's own marker.
                float corner = Mathf.Min(16f, Mathf.Max(9f, rect.height * 0.45f));
                label.style.left = rect.xMax - corner - 1f;
                label.style.top = rect.y + 1f;
                label.style.width = corner;
                label.style.height = corner;
                label.style.fontSize = Mathf.Max(8f, corner - 2f);
            }

            Add(label);
            _glyphs.Add(label);
        }

        private static string MarkerClass(MapMarker marker)
        {
            switch (marker)
            {
                case MapMarker.Enemies:
                    return "dm-glyph--enemies";
                case MapMarker.Captive:
                    return "dm-glyph--captive";
                case MapMarker.Event:
                    return "dm-glyph--event";
                case MapMarker.Cache:
                    return "dm-glyph--cache";
                case MapMarker.Refuge:
                    return "dm-glyph--refuge";
                default:
                    return null;
            }
        }

        // --- painting -----------------------------------------------------------------

        private void Draw(MeshGenerationContext context)
        {
            if (_model == null || _model.Rooms.Count == 0)
            {
                return;
            }

            var painter = context.painter2D;
            var rects = new Dictionary<int, Rect>(_model.Rooms.Count);
            foreach (var room in _model.Rooms)
            {
                rects[room.Index] = PixelRect(room.Bounds);
            }

            // Doors first, under the boxes. Each one is trimmed to the two rooms' edges rather than
            // drawn centre to centre: an unexplored room is an unfilled outline, so an untrimmed
            // line shows straight through it and reads as something reaching into a room the player
            // has never been in.
            painter.lineCap = LineCap.Round;
            painter.lineWidth = Mathf.Max(2f, _scale * 0.35f);
            foreach (var link in _model.Links)
            {
                if (!rects.TryGetValue(link.A, out var rectA) || !rects.TryGetValue(link.B, out var rectB))
                {
                    continue;
                }

                var from = rectA.center;
                var to = rectB.center;
                var start = EdgePoint(rectA, from, to);
                var end = EdgePoint(rectB, to, from);

                // Rooms that touch leave nothing to draw, and the trim would cross itself.
                if (Vector2.Dot(end - start, to - from) <= 0f)
                {
                    continue;
                }

                painter.strokeColor = link.BothExplored ? WalkedDoor : UnwalkedDoor;
                painter.BeginPath();
                painter.MoveTo(start);
                painter.LineTo(end);
                painter.Stroke();
            }

            foreach (var room in _model.Rooms)
            {
                var rect = PixelRect(room.Bounds);
                bool explored = room.State == MapRoomState.Explored;

                painter.lineWidth = room.IsCurrent ? 3f : 2f;
                painter.strokeColor = room.IsCurrent
                    ? CurrentBorder
                    : explored ? ExploredBorder : KnownBorder;
                painter.fillColor = room.IsCurrent ? CurrentFill : ExploredFill;

                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();

                // An unvisited room is an outline and nothing else: no fill, so the eye reads it as
                // "there is a room there" rather than as somewhere already dealt with.
                if (explored)
                {
                    painter.Fill();
                }
                painter.Stroke();

                if (room.Index == _selected)
                {
                    var inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
                    painter.lineWidth = 2f;
                    painter.strokeColor = CursorRing;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(inner.xMin, inner.yMin));
                    painter.LineTo(new Vector2(inner.xMax, inner.yMin));
                    painter.LineTo(new Vector2(inner.xMax, inner.yMax));
                    painter.LineTo(new Vector2(inner.xMin, inner.yMax));
                    painter.ClosePath();
                    painter.Stroke();
                }
            }
        }
    }
}
