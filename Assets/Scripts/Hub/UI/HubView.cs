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

            /// <summary>An idle loop to play in place of <see cref="Sprite"/>, or null (see <see cref="BuildingSO.IdleFrames"/>).</summary>
            public Sprite[] Frames;

            public float FramesPerSecond;

            /// <summary>The lot's living lights, or null (see <see cref="BuildingSO.Lights"/>).</summary>
            public List<HubAmbientLight> Lights;
        }

        private readonly VisualElement _canvas;

        /// <summary>Design-space height a caption needs below its lot: one line, the name and its badge.</summary>
        private const float CaptionRoom = 44f;
        private readonly VisualElement _backdrop;
        private readonly AmbienceLayer _backFxLayer;
        private readonly VisualElement _spriteLayer;
        private readonly AmbienceLayer _fxLayer;
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

            // The backdrop's own life (stars, the glowing pools on the ground) paints under the
            // buildings; a building's fire, sparks and smoke paint over them.
            _backFxLayer = new AmbienceLayer { name = "hub-back-fx" };
            _canvas.Add(_backFxLayer);

            // Sprites below, buttons above: the art may overlap freely, while input stays on the tidy
            // rectangles HubContentTests keeps apart.
            _spriteLayer = new VisualElement { name = "hub-sprites", pickingMode = PickingMode.Ignore };
            Stretch(_spriteLayer);
            _canvas.Add(_spriteLayer);

            _fxLayer = new AmbienceLayer { name = "hub-fx" };
            _canvas.Add(_fxLayer);

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
            _fxLayer.ClearAll();
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
                    SetLotAmbience(lot.Key, lot.Frames, lot.FramesPerSecond, lot.Lights, lot.DrawRect.position);

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
            var button = new LotButton { name = "hub-lot-" + lot.Key, focusable = false };
            button.DrawRectLocal = new Rect(lot.DrawRect.position - lot.HitRect.position, lot.DrawRect.size);
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

            // Name and note hang BELOW the lot, not on it: inside the hitbox they sat on the building's
            // own art - the Storehouse's name over its door, a requirement over the Merchant's fence -
            // and wrapped to the lot's width mid-phrase (playtest 2026-09-28, finding 13). A child of
            // the button, so it moves with the lot; ignored by picking, so the hitbox stays the art.
            var caption = new VisualElement { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("hub-lot__caption");
            // A lot at the foot of the town (the campfire) has no room below it inside the frame, so
            // its caption goes above instead.
            caption.EnableInClassList("hub-lot__caption--above",
                lot.HitRect.yMax + CaptionRoom > _referenceSize.y);
            button.Add(caption);

            var label = new Label(lot.Label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("hub-lot__label");
            caption.Add(label);

            // A hammer or a padlock beside the name, never a line of costs: the details are one click
            // away on the lot panel (the owner's call, 2026-09-28).
            var badge = new VisualElement { name = "hub-lot-badge-" + lot.Key, pickingMode = PickingMode.Ignore };
            badge.AddToClassList("hub-lot__badge");
            badge.AddToClassList("cd-hidden");
            caption.Add(badge);

            // The tutorial's pointer, hidden until SetLotGuided turns it on. Authored with the lot
            // rather than added later, the same way the badge is.
            var pointer = new Label("▼") { name = "hub-lot-pointer-" + lot.Key, pickingMode = PickingMode.Ignore };
            pointer.AddToClassList("hub-lot__pointer");
            pointer.AddToClassList("cd-hidden");
            button.Add(pointer);

            // The highlight that follows the building's shape: a one-texel ring around the sprite,
            // shown on hover and under the cursor (playtest 4 finding 47). Ignored by picking; it also
            // widens the button's bounding box over the art, which is what lets ContainsPoint be
            // asked about a point on the art outside the hit rect.
            var outline = new VisualElement { name = "hub-lot-outline-" + lot.Key, pickingMode = PickingMode.Ignore };
            outline.AddToClassList("hub-lot__outline");
            outline.style.position = Position.Absolute;
            button.Add(outline);
            button.Outline = outline;

            ApplyArtMode(button, glyph, lot.Sprite != null);
            ApplyShape(button, lot.Sprite);

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

        // --- shape-true hover and click (playtest 4 finding 47) ---------------------

        /// <summary>
        /// A lot's button that answers hover and clicks only over its building's solid pixels (with a
        /// texel of slack), not over its whole rectangle. UI Toolkit asks <see cref="ContainsPoint"/>
        /// while picking, front lot first, so where two buildings overlap the one drawn in front wins,
        /// and a click through a transparent corner reaches the lot behind. With no readable sprite
        /// (no art, or a texture imported without Read/Write) it falls back to the hit rectangle.
        /// </summary>
        public sealed class LotButton : Button
        {
            /// <summary>The lot's draw rect in this button's own space (DrawRect less HitRect's corner).</summary>
            public Rect DrawRectLocal;

            /// <summary>Where the sprite actually paints inside <see cref="DrawRectLocal"/> (scale-to-fit).</summary>
            public Rect ShapeRect;

            public SpriteAlphaMask Mask;
            public VisualElement Outline;

            public override bool ContainsPoint(Vector2 localPoint)
            {
                if (Mask == null || ShapeRect.width <= 0f || ShapeRect.height <= 0f)
                {
                    return base.ContainsPoint(localPoint);
                }
                var normalized = new Vector2(
                    (localPoint.x - ShapeRect.x) / ShapeRect.width,
                    (localPoint.y - ShapeRect.y) / ShapeRect.height);
                return Mask.Hits(normalized);
            }
        }

        private static readonly Dictionary<Sprite, SpriteAlphaMask> MaskCache = new Dictionary<Sprite, SpriteAlphaMask>();
        private static readonly Dictionary<Sprite, Texture2D> OutlineCache = new Dictionary<Sprite, Texture2D>();
        private static bool _warnedUnreadable;

        /// <summary>Points the lot's hit test and outline at <paramref name="sprite"/>'s shape, or back
        /// to its rectangle when there is no usable sprite.</summary>
        private static void ApplyShape(LotButton button, Sprite sprite)
        {
            if (button == null)
            {
                return;
            }
            var mask = MaskFor(sprite);
            button.Mask = mask;
            button.EnableInClassList("hub-lot--shaped", mask != null);
            if (mask == null)
            {
                button.ShapeRect = Rect.zero;
                if (button.Outline != null)
                {
                    button.Outline.style.display = DisplayStyle.None;
                }
                return;
            }

            button.ShapeRect = SpriteAlphaMask.FitInside(button.DrawRectLocal, mask.Width, mask.Height);
            if (button.Outline == null)
            {
                return;
            }
            // One texel larger on every side, the size the ring texture is.
            float texel = button.ShapeRect.width / mask.Width;
            var outline = button.Outline;
            outline.style.display = DisplayStyle.Flex;
            outline.style.left = button.ShapeRect.x - texel;
            outline.style.top = button.ShapeRect.y - texel;
            outline.style.width = button.ShapeRect.width + texel * 2f;
            outline.style.height = button.ShapeRect.height + texel * 2f;
            outline.style.backgroundImage = new StyleBackground(OutlineFor(sprite, mask));
        }

        private static SpriteAlphaMask MaskFor(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }
            if (MaskCache.TryGetValue(sprite, out var cached))
            {
                return cached;
            }

            SpriteAlphaMask mask = null;
            var texture = sprite.texture;
            if (texture.isReadable)
            {
                try
                {
                    var rect = sprite.textureRect;
                    int x = Mathf.RoundToInt(rect.x);
                    int y = Mathf.RoundToInt(rect.y);
                    int w = Mathf.RoundToInt(rect.width);
                    int h = Mathf.RoundToInt(rect.height);
                    var colors = texture.GetPixels(x, y, w, h);
                    var pixels = new Color32[colors.Length];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        pixels[i] = colors[i];
                    }
                    mask = SpriteAlphaMask.FromPixelsBottomUp(pixels, w, h);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HubView] Could not read '{sprite.name}' for its shape; it clicks by its rectangle. {e.Message}");
                }
            }
            else if (!_warnedUnreadable)
            {
                _warnedUnreadable = true;
                Debug.LogWarning($"[HubView] '{texture.name}' is not imported with Read/Write, so its lot hovers and clicks by its rectangle. Tick Read/Write on the building sprites (Assets/Sprites/Hub).");
            }
            MaskCache[sprite] = mask;
            return mask;
        }

        /// <summary>The ring as a white, point-filtered texture; USS tints it per state.</summary>
        private static Texture2D OutlineFor(Sprite sprite, SpriteAlphaMask mask)
        {
            if (OutlineCache.TryGetValue(sprite, out var cached) && cached != null)
            {
                return cached;
            }
            int w = mask.Width + 2;
            int h = mask.Height + 2;
            var ring = mask.OutlineTopDown();
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // Textures are bottom row first; the ring is top row first.
                int target = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    pixels[target + x] = ring[y * w + x] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = sprite.name + "_outline",
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            OutlineCache[sprite] = texture;
            return texture;
        }

        // --- ambience ---------------------------------------------------------------

        /// <summary>
        /// The backdrop's life: the glow every light paints with, the backdrop's own lights and the
        /// twinkling stars (<see cref="AmbienceLayer"/>). Call before <see cref="SetTown"/>, which hands the
        /// same glow to every lot's lights; calling again replaces it.
        /// </summary>
        public void SetBackdropAmbience(Sprite glow, IReadOnlyList<HubAmbientLight> lights, int stars, Rect starField)
        {
            _backFxLayer.ClearAll();
            _backFxLayer.GlowSprite = glow;
            _fxLayer.GlowSprite = glow;
            _backFxLayer.SetGroup("backdrop", lights, Vector2.zero);
            _backFxLayer.SetStars(stars, starField);
        }

        /// <summary>
        /// A lot's idle loop and lights, replacing whatever it had. Null frames and lights clear it - an
        /// unbuilt lot is still. <paramref name="origin"/> is the lot's DrawRect corner, which every
        /// light's <see cref="HubAmbientLight.Point"/> is measured from.
        /// </summary>
        public void SetLotAmbience(string key, Sprite[] frames, float fps, IReadOnlyList<HubAmbientLight> lights, Vector2 origin)
        {
            _fxLayer.SetGroup(key, lights, origin, _sprites.TryGetValue(key, out var art) ? art : null, frames, fps);
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
            // The caller re-arms the loop (SetLotAmbience) for the lot's new state.
            _fxLayer.StopFrames(key);
            if (_buttons.TryGetValue(key, out var button))
            {
                ApplyArtMode(button, button.Q<Label>("hub-glyph-" + key), sprite != null);
                ApplyShape(button as LotButton, sprite);
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

        /// <summary>Sets the badge beside a lot's name: a hammer, a padlock, or none.</summary>
        public void SetLotBadge(string key, LotBadge badge)
        {
            if (!_buttons.TryGetValue(key, out var button))
            {
                return;
            }
            var element = button.Q<VisualElement>("hub-lot-badge-" + key);
            if (element == null)
            {
                return;
            }
            element.EnableInClassList("hub-lot__badge--build", badge == LotBadge.Build);
            element.EnableInClassList("hub-lot__badge--locked", badge == LotBadge.Locked);
            element.EnableInClassList("hub-lot__badge--new", badge == LotBadge.New);
            element.EnableInClassList("hub-lot__badge--ready", badge == LotBadge.Ready);
            element.EnableInClassList("cd-hidden", badge == LotBadge.None);
            // A lot that can be built or raised now says so with its name as well as its badge.
            button.EnableInClassList("hub-lot--ready", badge == LotBadge.Ready);
        }

        /// <summary>
        /// Whether a lot can be clicked. The tutorial locks every lot but the one it is walking the
        /// player to; a disabled Button also drops out of the keyboard cursor, so the arrows cannot
        /// reach what the mouse cannot.
        /// </summary>
        public void SetLotEnabled(string key, bool enabled)
        {
            if (_buttons.TryGetValue(key, out var button) && button.enabledSelf != enabled)
            {
                button.SetEnabled(enabled);
            }
        }

        /// <summary>Puts the tutorial's pulsing outline and pointer on a lot, or takes them off.</summary>
        public void SetLotGuided(string key, bool guided)
        {
            if (!_buttons.TryGetValue(key, out var button))
            {
                return;
            }
            button.EnableInClassList(TutorialTargetClass, guided);
            button.Q<VisualElement>("hub-lot-pointer-" + key)?.EnableInClassList("cd-hidden", !guided);
        }

        /// <summary>The class every tutorial target carries, on every screen (see CardDungeon.uss).</summary>
        public const string TutorialTargetClass = "cd-tutorial-target";

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
