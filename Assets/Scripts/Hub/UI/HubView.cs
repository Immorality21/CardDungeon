using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// The painted town: a backdrop with a sprite per building layered over it, and a clickable box
    /// per building layered over that — all inside an authored pixel rect that is letterboxed as a
    /// single unit into whatever space the screen gives it.
    ///
    /// <para><b>Why this is not <c>SphereGridView</c>.</b> Two of that widget's three jobs — edges and
    /// pan/zoom — are wrong for a fixed town, and the third is not its own: <c>DirectionalNav</c> is a
    /// standalone static, and the hub does not even need it (see the Keyboard note below).
    /// <c>docs/plans/HUB.md</c> §7 machinery 3 decided this before either existed.</para>
    ///
    /// <para><b>Three layers, and the middle one is the point.</b> Sprites live in their own layer
    /// beneath the buttons, positioned by <see cref="LotInfo.DrawRect"/>, so a silhouette can overlap
    /// its neighbours and spill outside the box you click — which is what makes a town look painted
    /// rather than tiled. The buttons sit on <see cref="LotInfo.HitRect"/> and go transparent whenever
    /// there is art behind them. Keeping the two rectangles apart is what lets the art overlap while
    /// UI Toolkit's stubbornly rectangular hit-testing stays unambiguous.</para>
    ///
    /// <para>Two more constraints the shape here exists to satisfy:</para>
    /// <list type="bullet">
    /// <item><b>The town scales as one unit.</b> Everything is absolutely positioned inside one
    /// fixed-size <see cref="_canvas"/> that is uniformly scaled and centred, so the art and the
    /// hitboxes can never drift apart — the same trap <c>cd-window--fixed</c> exists to avoid.</item>
    /// <item><b>UI Toolkit has no z-index.</b> Siblings paint in the order they are added, so lots
    /// arrive in <see cref="BuildingOps.InDrawOrder"/> and nothing re-sorts them afterwards.</item>
    /// </list>
    /// </summary>
    public sealed class HubView : VisualElement
    {
        /// <summary>One lot's render input. Built by <see cref="HubPresenter.BuildViewModel"/>.</summary>
        public struct LotInfo
        {
            public string Key;

            /// <summary>Where it can be clicked. Never overlaps another lot's.</summary>
            public Rect HitRect;

            /// <summary>Where the sprite paints. Free to overlap anything.</summary>
            public Rect DrawRect;

            public string Label;
            public string Glyph;
            public Sprite Sprite;
            public string Tooltip;
        }

        private readonly VisualElement _canvas;
        private readonly VisualElement _backdrop;
        private readonly VisualElement _spriteLayer;
        private readonly VisualElement _lotLayer;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, VisualElement> _sprites = new Dictionary<string, VisualElement>();
        private readonly List<string> _order = new List<string>();

        private Vector2 _referenceSize = new Vector2(1280f, 720f);

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
            Stretch(_backdrop);
            _canvas.Add(_backdrop);

            // Sprites below, buttons above: the art may overlap freely, while input stays on the tidy
            // rectangles HubContentTests keeps apart.
            _spriteLayer = new VisualElement { name = "hub-sprites", pickingMode = PickingMode.Ignore };
            Stretch(_spriteLayer);
            _canvas.Add(_spriteLayer);

            _lotLayer = new VisualElement { name = "hub-lots", pickingMode = PickingMode.Ignore };
            Stretch(_lotLayer);
            _canvas.Add(_lotLayer);

            // The canvas is a fixed pixel rect; the viewport's size is only known after layout, so the
            // letterbox is recomputed whenever it changes.
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
        }

        private static void Stretch(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }

        // --- population -----------------------------------------------------------

        /// <summary>Replaces the whole town. Lots are added in the order given, which <b>is</b> the
        /// paint order — pass <see cref="HubPresenter.BuildViewModel"/>'s output unmodified.</summary>
        public void SetTown(Vector2 referenceSize, Sprite backdrop, IReadOnlyList<LotInfo> lots)
        {
            _spriteLayer.Clear();
            _lotLayer.Clear();
            _buttons.Clear();
            _sprites.Clear();
            _order.Clear();

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

                    var art = MakeSprite(lot);
                    _sprites[lot.Key] = art;
                    _spriteLayer.Add(art);

                    var button = MakeLot(lot);
                    _buttons[lot.Key] = button;
                    _lotLayer.Add(button);

                    _order.Add(lot.Key);
                }
            }

            Relayout();
        }

        private static VisualElement MakeSprite(LotInfo lot)
        {
            var art = new VisualElement { name = "hub-art-" + lot.Key, pickingMode = PickingMode.Ignore };
            art.AddToClassList("hub-art");
            art.style.position = Position.Absolute;
            art.style.left = lot.DrawRect.x;
            art.style.top = lot.DrawRect.y;
            art.style.width = lot.DrawRect.width;
            art.style.height = lot.DrawRect.height;
            ApplySprite(art, lot.Sprite);
            return art;
        }

        private static void ApplySprite(VisualElement art, Sprite sprite)
        {
            art.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : new StyleBackground((Texture2D)null);
            art.EnableInClassList("hub-art--empty", sprite == null);
        }

        private Button MakeLot(LotInfo lot)
        {
            // focusable = false for the same reason every hub button is: the screen's shared cursor
            // drives selection, and UITK focus would fight it.
            var button = new Button { name = "hub-lot-" + lot.Key, focusable = false };
            button.RemoveFromClassList("unity-button");
            button.AddToClassList("hub-lot");
            button.style.position = Position.Absolute;
            button.style.left = lot.HitRect.x;
            button.style.top = lot.HitRect.y;
            button.style.width = lot.HitRect.width;
            button.style.height = lot.HitRect.height;
            button.tooltip = lot.Tooltip ?? "";

            var glyph = new Label(lot.Glyph) { name = "hub-glyph-" + lot.Key, pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("hub-lot__glyph");
            button.Add(glyph);

            var label = new Label(lot.Label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("hub-lot__label");
            button.Add(label);

            var note = new Label(string.Empty) { name = "hub-lot-note-" + lot.Key, pickingMode = PickingMode.Ignore };
            note.AddToClassList("hub-lot__note");
            button.Add(note);

            ApplyArtMode(button, glyph, lot.Sprite != null);

            var captured = lot.Key;
            button.clicked += () => LotClicked?.Invoke(captured);
            return button;
        }

        /// <summary>With art behind it the button is a hitbox, not a slab: no fill, no border, no
        /// glyph — the sprite is doing the identifying.</summary>
        private static void ApplyArtMode(Button button, Label glyph, bool hasArt)
        {
            button.EnableInClassList("hub-lot--art", hasArt);
            if (glyph != null)
            {
                glyph.style.display = hasArt ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        // --- state ----------------------------------------------------------------

        /// <summary>Swaps a lot's state class on both its button and its sprite. Every other state
        /// class is removed first, so callers never have to know which one was on it.</summary>
        public void SetLotState(string key, string stateClass)
        {
            SwapStateClass(_buttons.TryGetValue(key, out var button) ? button : null, stateClass);
            SwapStateClass(_sprites.TryGetValue(key, out var art) ? art : null, stateClass);
        }

        private static void SwapStateClass(VisualElement element, string stateClass)
        {
            if (element == null)
            {
                return;
            }
            foreach (var candidate in HubPresenter.StateClasses)
            {
                element.RemoveFromClassList(candidate);
            }
            if (!string.IsNullOrEmpty(stateClass))
            {
                element.AddToClassList(stateClass);
            }
        }

        /// <summary>
        /// Swaps the sprite a lot paints. <paramref name="phaseIn"/> replays the build animation — a
        /// USS transition on opacity and scale, which is all "the new building appears" needs to be,
        /// and the reason a build has to be <i>confirmed in the hub</i> rather than applied on load.
        /// </summary>
        public void SetLotSprite(string key, Sprite sprite, bool phaseIn = false)
        {
            if (!_sprites.TryGetValue(key, out var art))
            {
                return;
            }

            ApplySprite(art, sprite);
            if (_buttons.TryGetValue(key, out var button))
            {
                ApplyArtMode(button, button.Q<Label>("hub-glyph-" + key), sprite != null);
            }

            if (!phaseIn)
            {
                return;
            }

            // Set the *starting* state, let a frame lay it out, then drop it — the sprite transitions
            // from there back to its resting opacity and scale. Doing it the other way round (adding a
            // class that sets the end state) animates nothing, because the end state is the default
            // and a USS transition only runs on a change.
            art.AddToClassList("hub-art--phasing");
            art.schedule.Execute(() => art.RemoveFromClassList("hub-art--phasing")).ExecuteLater(16);
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
        // measures worldBound centres, which carry the canvas transform, so the arrows follow the town
        // as drawn. The road and the menu button navigate as part of the same screen for free.
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
