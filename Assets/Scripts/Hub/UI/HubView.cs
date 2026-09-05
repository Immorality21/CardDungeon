using System;
using System.Collections.Generic;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// The painted town: a backdrop with one clickable lot per building, laid out in an authored
    /// pixel rect that is letterboxed as a single unit into whatever space the screen gives it.
    ///
    /// <para><b>Why this is not <c>SphereGridView</c>.</b> Two of that widget's three jobs — edges and
    /// pan/zoom — are wrong for a fixed town, and the third is not its own:
    /// <see cref="DirectionalNav.PickInDirection"/> is a standalone static this class calls directly.
    /// <c>docs/plans/HUB.md</c> §7 machinery 3 decided this before either existed.</para>
    ///
    /// <para>Three constraints the shape here exists to satisfy:</para>
    /// <list type="bullet">
    /// <item><b>The town scales as one unit.</b> Everything is absolutely positioned inside one
    /// fixed-size <see cref="_canvas"/> that is uniformly scaled and centred, so the art and the
    /// hitboxes can never drift apart — the same trap <c>cd-window--fixed</c> exists to avoid.</item>
    /// <item><b>UI Toolkit has no z-index.</b> Siblings paint in the order they are added, so lots
    /// are added in <see cref="BuildingOps.InDrawOrder"/> and nothing re-sorts them afterwards.</item>
    /// <item><b>Hit-testing is rectangular.</b> A lot's button is its authored rect, whatever the
    /// sprite looks like; overlapping rects steal each other's clicks, which is why
    /// <c>HubContentTests</c> refuses to let two overlap.</item>
    /// </list>
    /// </summary>
    public sealed class HubView : VisualElement
    {
        /// <summary>One lot's render input. Built by <see cref="HubPresenter.BuildViewModel"/>.</summary>
        public struct LotInfo
        {
            public string Key;
            public Rect Rect;
            public string Label;
            public string Glyph;
            public Sprite Sprite;
            public string Tooltip;
        }

        private readonly VisualElement _canvas;
        private readonly VisualElement _backdrop;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly List<string> _order = new List<string>();

        private Vector2 _referenceSize = new Vector2(1280f, 720f);
        private string _selectedKey;

        /// <summary>Raised when a lot is clicked or activated with the keyboard.</summary>
        public event Action<string> LotClicked;

        public HubView()
        {
            AddToClassList("hub-view__viewport");
            pickingMode = PickingMode.Position;

            _canvas = new VisualElement { name = "hub-canvas" };
            _canvas.AddToClassList("hub-canvas");
            _canvas.style.position = Position.Absolute;
            Add(_canvas);

            _backdrop = new VisualElement { name = "hub-backdrop", pickingMode = PickingMode.Ignore };
            _backdrop.AddToClassList("hub-backdrop");
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.left = 0;
            _backdrop.style.top = 0;
            _backdrop.style.right = 0;
            _backdrop.style.bottom = 0;
            _canvas.Add(_backdrop);

            // The canvas is a fixed pixel rect; the viewport's size is only known after layout, so
            // the letterbox is recomputed whenever it changes.
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
        }

        // --- population -----------------------------------------------------------

        /// <summary>Replaces the whole town. Lots are added in the order given, which <b>is</b> the
        /// paint order — pass <see cref="HubPresenter.BuildViewModel"/>'s output unmodified.</summary>
        public void SetTown(Vector2 referenceSize, Sprite backdrop, IReadOnlyList<LotInfo> lots)
        {
            foreach (var button in _buttons.Values)
            {
                button.RemoveFromHierarchy();
            }
            _buttons.Clear();
            _order.Clear();
            _selectedKey = null;

            _referenceSize = new Vector2(Mathf.Max(1f, referenceSize.x), Mathf.Max(1f, referenceSize.y));
            _backdrop.style.backgroundImage = backdrop != null
                ? new StyleBackground(backdrop)
                : new StyleBackground((Texture2D)null);

            if (lots != null)
            {
                foreach (var lot in lots)
                {
                    if (string.IsNullOrEmpty(lot.Key) || _buttons.ContainsKey(lot.Key))
                    {
                        continue;
                    }
                    var button = MakeLot(lot);
                    _buttons[lot.Key] = button;
                    _order.Add(lot.Key);
                    _canvas.Add(button);
                }
            }

            Relayout();
        }

        private Button MakeLot(LotInfo lot)
        {
            // focusable = false for the same reason every hub button is: the screen's own cursor
            // drives selection, and UITK focus would fight it.
            var button = new Button { name = "hub-lot-" + lot.Key, focusable = false };
            button.RemoveFromClassList("unity-button");
            button.AddToClassList("hub-lot");
            button.style.position = Position.Absolute;
            button.style.left = lot.Rect.x;
            button.style.top = lot.Rect.y;
            button.style.width = lot.Rect.width;
            button.style.height = lot.Rect.height;
            button.tooltip = lot.Tooltip ?? "";

            if (lot.Sprite != null)
            {
                button.style.backgroundImage = new StyleBackground(lot.Sprite);
                button.AddToClassList("hub-lot--art");
            }

            var glyph = new Label(lot.Glyph) { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("hub-lot__glyph");
            button.Add(glyph);

            var label = new Label(lot.Label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("hub-lot__label");
            button.Add(label);

            var note = new Label(string.Empty) { name = "hub-lot-note-" + lot.Key, pickingMode = PickingMode.Ignore };
            note.AddToClassList("hub-lot__note");
            button.Add(note);

            var captured = lot.Key;
            button.clicked += () => LotClicked?.Invoke(captured);
            return button;
        }

        // --- state ----------------------------------------------------------------

        /// <summary>Swaps a lot's state class. Every other state class is removed first, so callers
        /// never have to know which one was on it.</summary>
        public void SetLotState(string key, string stateClass)
        {
            if (!_buttons.TryGetValue(key, out var button))
            {
                return;
            }
            foreach (var candidate in HubPresenter.StateClasses)
            {
                button.RemoveFromClassList(candidate);
            }
            if (!string.IsNullOrEmpty(stateClass))
            {
                button.AddToClassList(stateClass);
            }
        }

        /// <summary>Sets the small line under a lot's name (its level, or what it is waiting for).</summary>
        public void SetLotNote(string key, string note)
        {
            if (!_buttons.TryGetValue(key, out var button))
            {
                return;
            }
            var label = button.Q<Label>("hub-lot-note-" + key);
            if (label != null)
            {
                label.text = note ?? "";
            }
        }

        /// <summary>The lot keys currently drawn, in paint order.</summary>
        public IReadOnlyList<string> Lots => _order;

        // --- keyboard --------------------------------------------------------------
        //
        // There is none here, deliberately. A lot is a real Button in the visible subtree, so the
        // hub's shared KeyboardNavigator already finds it and moves between lots spatially - it
        // measures worldBound centres, which carry the canvas transform, so the arrows follow the
        // town as drawn. The road and the menu button navigate as part of the same screen for free.
        //
        // This is why hub-view is *included* in HubManager.NavigatesCurrentView() while the campaign
        // map, sphere grid, bestiary and inventory are excluded: those four build their own cursors
        // because they scroll or pan content the shared navigator cannot see. A fixed town does not.

        // --- layout ----------------------------------------------------------------

        /// <summary>
        /// Letterboxes the authored rect into the viewport: one uniform scale, centred. Scaling the
        /// canvas rather than each lot is what keeps the art and the hitboxes locked together — UITK
        /// applies the transform to hit-testing too, so a click lands where the pixel is.
        /// </summary>
        private void Relayout()
        {
            float availableWidth = resolvedStyle.width;
            float availableHeight = resolvedStyle.height;
            if (float.IsNaN(availableWidth) || availableWidth <= 0f
                || float.IsNaN(availableHeight) || availableHeight <= 0f)
            {
                return;
            }

            _canvas.style.width = _referenceSize.x;
            _canvas.style.height = _referenceSize.y;

            float scale = Mathf.Min(availableWidth / _referenceSize.x, availableHeight / _referenceSize.y);
            _canvas.style.left = (availableWidth - _referenceSize.x * scale) * 0.5f;
            _canvas.style.top = (availableHeight - _referenceSize.y * scale) * 0.5f;
            _canvas.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _canvas.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }
    }
}
