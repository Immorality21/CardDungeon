using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// Living light over a painted menu backdrop (<c>.cd-bg</c>): an <see cref="AmbienceLayer"/> on a
    /// canvas the size of the image (4 design pixels to each of its pixels), scaled and cropped exactly
    /// as the backdrop's own <c>-unity-background-scale-mode: scale-and-crop</c> scales the image - so a
    /// torch's glow stays on the torch at any window size. Lights come from
    /// <see cref="BackdropAmbienceSO"/>, chosen by the backdrop class the screen shows.
    ///
    /// <para>Attach it as a child of the backdrop element with <see cref="AttachTo"/>; call
    /// <see cref="Show"/> whenever the backdrop's class may have changed (cheap when it has not).</para>
    /// </summary>
    public sealed class BackdropAmbienceView : VisualElement
    {
        private readonly VisualElement _canvas;
        private readonly AmbienceLayer _layer;
        private string _shown;
        private Vector2 _imageSize;

        private BackdropAmbienceView()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.overflow = Overflow.Hidden;

            _canvas = new VisualElement { name = "backdrop-fx-canvas", pickingMode = PickingMode.Ignore };
            _canvas.style.position = Position.Absolute;
            Add(_canvas);
            _layer = new AmbienceLayer { name = "backdrop-fx" };
            _canvas.Add(_layer);

            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
        }

        /// <summary>A view inside <paramref name="backdrop"/>, painting over its image. Null for a null host.</summary>
        public static BackdropAmbienceView AttachTo(VisualElement backdrop)
        {
            if (backdrop == null)
            {
                return null;
            }
            var view = new BackdropAmbienceView();
            backdrop.Add(view);
            return view;
        }

        /// <summary>Shows the lights for <paramref name="backdropClass"/>, or none for null / an unauthored class.</summary>
        public void Show(string backdropClass)
        {
            if (_shown == backdropClass)
            {
                return;
            }
            _shown = backdropClass;
            _layer.ClearAll();

            var asset = BackdropAmbienceSO.Load();
            var entry = asset != null ? asset.Find(backdropClass) : null;
            if (entry == null)
            {
                _imageSize = Vector2.zero;
                return;
            }

            _imageSize = entry.ImageSize;
            _layer.GlowSprite = asset.GlowSprite;
            _layer.SetGroup("backdrop", entry.Lights, Vector2.zero);
            _layer.SetStars(entry.TwinklingStars, entry.StarField);
            Relayout();
        }

        /// <summary>Cover-scales the canvas the way scale-and-crop scales the image: fill, centre, crop.</summary>
        private void Relayout()
        {
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (_imageSize.x <= 0f || _imageSize.y <= 0f || float.IsNaN(width) || float.IsNaN(height)
                || width <= 0f || height <= 0f)
            {
                return;
            }

            var design = _imageSize * HubAmbience.Pixel;
            float scale = Mathf.Max(width / design.x, height / design.y);
            _canvas.style.width = design.x;
            _canvas.style.height = design.y;
            _canvas.style.left = (width - design.x * scale) * 0.5f;
            _canvas.style.top = (height - design.y * scale) * 0.5f;
            _canvas.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _canvas.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }
    }
}
