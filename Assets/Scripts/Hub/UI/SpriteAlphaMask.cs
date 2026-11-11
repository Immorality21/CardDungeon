using UnityEngine;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// Which texels of a sprite are solid, for the town's shape-true hover and click (playtest 4
    /// finding 47: the outlines were rectangles that did not match the buildings). Built once per
    /// sprite from its pixels; pure apart from reading them, so the rules are tested on hand-made masks.
    ///
    /// <para>Coordinates are <b>UI-style, (0,0) top-left</b>, even though Unity hands pixels over
    /// bottom row first: everything here is asked about by a panel, which is y-down.</para>
    /// </summary>
    public sealed class SpriteAlphaMask
    {
        /// <summary>Alpha above which a texel counts as the building (soft edges and glows below do not).</summary>
        public const byte SolidAlpha = 40;

        private readonly bool[] _solid;

        public int Width { get; }
        public int Height { get; }

        /// <param name="solidTopDown">Row-major, top row first.</param>
        public SpriteAlphaMask(int width, int height, bool[] solidTopDown)
        {
            Width = Mathf.Max(0, width);
            Height = Mathf.Max(0, height);
            _solid = solidTopDown ?? new bool[0];
        }

        /// <summary>From pixels as Unity returns them (bottom row first).</summary>
        public static SpriteAlphaMask FromPixelsBottomUp(Color32[] pixels, int width, int height, byte solidAlpha = SolidAlpha)
        {
            var solid = new bool[Mathf.Max(0, width * height)];
            if (pixels != null && pixels.Length >= width * height)
            {
                for (int y = 0; y < height; y++)
                {
                    int sourceRow = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                    {
                        solid[y * width + x] = pixels[sourceRow + x].a > solidAlpha;
                    }
                }
            }
            return new SpriteAlphaMask(width, height, solid);
        }

        /// <summary>Whether texel (x, y) is solid; anything outside the sprite is not.</summary>
        public bool IsSolid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return false;
            }
            return _solid[y * Width + x];
        }

        /// <summary>
        /// Whether a point given as a fraction of the sprite (0..1 each way, top-left origin) lands on
        /// it, or within <paramref name="slackTexels"/> of it - one texel of slack forgives a click on
        /// the outline drawn around the shape, which is where the eye says the building ends.
        /// </summary>
        public bool Hits(Vector2 normalized, int slackTexels = 1)
        {
            if (Width == 0 || Height == 0)
            {
                return false;
            }
            int x = Mathf.FloorToInt(normalized.x * Width);
            int y = Mathf.FloorToInt(normalized.y * Height);
            for (int dy = -slackTexels; dy <= slackTexels; dy++)
            {
                for (int dx = -slackTexels; dx <= slackTexels; dx++)
                {
                    if (IsSolid(x + dx, y + dy))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The one-texel ring around the shape, as a texture-sized mask one texel larger on every side
        /// (so the ring is never cut off at the sprite's edge): (Width+2) x (Height+2), top-down. A
        /// texel is on the ring when it is not solid and one of its four neighbours is.
        /// </summary>
        public bool[] OutlineTopDown()
        {
            int w = Width + 2;
            int h = Height + 2;
            var ring = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sx = x - 1;
                    int sy = y - 1;
                    if (IsSolid(sx, sy))
                    {
                        continue;
                    }
                    ring[y * w + x] = IsSolid(sx - 1, sy) || IsSolid(sx + 1, sy)
                        || IsSolid(sx, sy - 1) || IsSolid(sx, sy + 1);
                }
            }
            return ring;
        }

        /// <summary>
        /// Where a sprite of this size paints inside <paramref name="box"/> under USS
        /// <c>scale-to-fit</c>: uniformly scaled to fit, centred. Same rect the art layer shows.
        /// </summary>
        public static Rect FitInside(Rect box, int width, int height)
        {
            if (width <= 0 || height <= 0 || box.width <= 0f || box.height <= 0f)
            {
                return box;
            }
            float scale = Mathf.Min(box.width / width, box.height / height);
            float w = width * scale;
            float h = height * scale;
            return new Rect(box.x + (box.width - w) * 0.5f, box.y + (box.height - h) * 0.5f, w, h);
        }
    }
}
