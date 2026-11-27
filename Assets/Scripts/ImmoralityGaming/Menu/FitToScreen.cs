using UnityEngine;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Menu
{
    /// <summary>
    /// Shrinks a window that is larger than the screen until it fits, by a uniform <c>scale</c>
    /// around its centre, and leaves a window that fits at full size.
    ///
    /// <para>The use is a larger interface on a small screen: most windows have room to grow, but a
    /// dense window laid out in fixed pixels (an inventory of 1180×720) would run off a phone's
    /// screen. Fitting it keeps every part of it visible and clickable; UI Toolkit's picking follows
    /// the transform, so nothing has to be laid out twice. A window must not cap its own size with a
    /// percentage <c>max-width</c>/<c>max-height</c> for this to work: the cap clips it before it is
    /// ever measured as too big.</para>
    ///
    /// <para>The sizes compared are layout sizes, which a transform does not change, so setting the
    /// scale never feeds back into the next measurement.</para>
    ///
    /// <para>The screen is the window's panel root, which several scenes' documents can share and
    /// which can outlive a scene. The window listens to it only while attached to it, so a destroyed
    /// scene's windows are not kept alive by the panel.</para>
    /// </summary>
    public static class FitToScreen
    {
        /// <summary>The share of the screen a fitted window may use, leaving a rim around it.</summary>
        public const float Margin = 0.97f;

        /// <summary>A window pinned to the screen's edges: it is the screen's size by construction, so
        /// it is never shrunk (it would only lose the rim <see cref="Margin"/> keeps).</summary>
        public const string FillsScreenClass = "cd-window--fullscreen";

        /// <summary>Marks a window that is already fitted, so attaching twice adds nothing.</summary>
        private const string AttachedClass = "fit-to-screen";

        /// <summary>Fits <paramref name="window"/> now and whenever it or its panel's root changes size.</summary>
        public static void Attach(VisualElement window)
        {
            if (window == null || window.ClassListContains(AttachedClass))
            {
                return;
            }
            window.AddToClassList(AttachedClass);

            VisualElement screen = null;
            EventCallback<GeometryChangedEvent> onScreen = _ => Fit(window);

            void Listen(IPanel panel)
            {
                Release();
                if (panel == null)
                {
                    return;
                }
                screen = panel.visualTree;
                screen.RegisterCallback(onScreen);
            }

            void Release()
            {
                if (screen != null)
                {
                    screen.UnregisterCallback(onScreen);
                    screen = null;
                }
            }

            window.RegisterCallback<GeometryChangedEvent>(_ => Fit(window));
            window.RegisterCallback<AttachToPanelEvent>(evt => Listen(evt.destinationPanel));
            window.RegisterCallback<DetachFromPanelEvent>(_ => Release());
            Listen(window.panel);
            Fit(window);
        }

        public static void Fit(VisualElement window)
        {
            var screen = window.panel != null ? window.panel.visualTree : null;
            var factor = screen != null && !window.ClassListContains(FillsScreenClass)
                ? Factor(window.layout.size, screen.layout.size, Margin)
                : 1f;
            if (factor >= 1f)
            {
                // Fits: hand the scale back to the stylesheet rather than pinning it at 1.
                window.style.scale = StyleKeyword.Null;
                return;
            }
            window.style.scale = new Scale(new Vector3(factor, factor, 1f));
        }

        /// <summary>The scale that fits <paramref name="window"/> inside <paramref name="margin"/> of
        /// <paramref name="screen"/>: 1 when it already fits (or either size is not known yet).</summary>
        public static float Factor(Vector2 window, Vector2 screen, float margin)
        {
            if (window.x <= 0f || window.y <= 0f || screen.x <= 0f || screen.y <= 0f
                || float.IsNaN(window.x) || float.IsNaN(window.y) || float.IsNaN(screen.x) || float.IsNaN(screen.y))
            {
                return 1f;
            }
            var fit = Mathf.Min(screen.x * margin / window.x, screen.y * margin / window.y);
            return Mathf.Min(1f, fit);
        }
    }
}
