using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The story map's view keys (playtest 4 finding 48): the arrows already move the node cursor and
    /// the view follows it, so the view itself takes <b>WASD to pan</b> and <b>+ / - (and the keypad's)
    /// to zoom</b>. None of these keys meant anything on the map before, and the hub's Page Up/Down
    /// (the detail column) and Z/X (confirm/back) stay free. Pure, so the mapping is tested.
    /// </summary>
    public static class MapViewKeys
    {
        /// <summary>Panel pixels one WASD press moves the view.</summary>
        public const float PanStep = 80f;

        /// <summary>Zoom factor of one + press; - divides by it. The wheel's step is 1.1.</summary>
        public const float ZoomStep = 1.2f;

        /// <summary>
        /// How far a key moves the view, in panel pixels, or zero. Moving the view left shows what lies
        /// to the left, so A pans the content right (+x); y is down in UI space.
        /// </summary>
        public static Vector2 PanFor(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.W:
                    return new Vector2(0f, PanStep);
                case KeyCode.S:
                    return new Vector2(0f, -PanStep);
                case KeyCode.A:
                    return new Vector2(PanStep, 0f);
                case KeyCode.D:
                    return new Vector2(-PanStep, 0f);
                default:
                    return Vector2.zero;
            }
        }

        /// <summary>The zoom factor a key applies, or 1 for any other key.</summary>
        public static float ZoomFor(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                    return ZoomStep;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    return 1f / ZoomStep;
                default:
                    return 1f;
            }
        }
    }
}
