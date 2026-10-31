using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Generates the thin wall sprites <see cref="RoomTilePainter"/> draws around a room, one per edge
    /// mask: Top=1, Right=2, Bottom=4, Left=8.
    /// </summary>
    public class WallGenerator
    {
        private const int TexSize = 32;
        private const int WallThickness = 6;

        // The outermost pixels of every wall are a near-black seam. Two rooms side by side each draw
        // their own wall, so between them the seams meet as one dark channel with a wall band either
        // side - without it, neighbours in the same stone read as one big room (playtest finding 17).
        private const int SeamThickness = 2;
        private static readonly Color SeamColor = new Color(0.03f, 0.02f, 0.05f, 1f);

        // Edge bitmask flags
        private const int Top = 1;
        private const int Right = 2;
        private const int Bottom = 4;
        private const int Left = 8;

        private readonly Dictionary<int, Sprite> _wallSprites = new Dictionary<int, Sprite>();
        private readonly Texture2D _wallTexture;
        private readonly Color _wallColor;
        private readonly int _sortingOrder;

        public WallGenerator(Texture2D wallTexture, Color wallColor, int sortingOrder = 5)
        {
            _wallTexture = wallTexture;
            _wallColor = wallColor;
            _sortingOrder = sortingOrder;
            GenerateSprites();
        }
        /// <summary>The band sprite for an edge mask (Top=1, Right=2, Bottom=4, Left=8), or null for 0.</summary>
        public Sprite SpriteFor(int mask)
        {
            return _wallSprites.TryGetValue(mask, out var sprite) ? sprite : null;
        }

        private void GenerateSprites()
        {
            // Generate a sprite for each of the 15 non-zero bitmask combinations
            for (int mask = 1; mask <= 15; mask++)
            {
                var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;

                // Fill fully transparent
                var clear = new Color(0, 0, 0, 0);
                var pixels = new Color[TexSize * TexSize];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = clear;

                // Draw walls on each flagged edge
                if ((mask & Top) != 0)
                    FillRect(pixels, 0, TexSize - WallThickness, TexSize, WallThickness);

                if ((mask & Bottom) != 0)
                    FillRect(pixels, 0, 0, TexSize, WallThickness);

                if ((mask & Left) != 0)
                    FillRect(pixels, 0, 0, WallThickness, TexSize);

                if ((mask & Right) != 0)
                    FillRect(pixels, TexSize - WallThickness, 0, WallThickness, TexSize);

                // Seams last, so a corner's seam is not painted over by the other edge's wall.
                if ((mask & Top) != 0)
                {
                    FillSeam(pixels, 0, TexSize - SeamThickness, TexSize, SeamThickness);
                }
                if ((mask & Bottom) != 0)
                {
                    FillSeam(pixels, 0, 0, TexSize, SeamThickness);
                }
                if ((mask & Left) != 0)
                {
                    FillSeam(pixels, 0, 0, SeamThickness, TexSize);
                }
                if ((mask & Right) != 0)
                {
                    FillSeam(pixels, TexSize - SeamThickness, 0, SeamThickness, TexSize);
                }

                tex.SetPixels(pixels);
                tex.Apply();

                var sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
                sprite.name = $"Wall_{mask}";
                _wallSprites[mask] = sprite;
            }
        }

        private static void FillSeam(Color[] pixels, int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width && x < TexSize; x++)
            {
                for (int y = startY; y < startY + height && y < TexSize; y++)
                {
                    pixels[y * TexSize + x] = SeamColor;
                }
            }
        }

        private void FillRect(Color[] pixels, int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width && x < TexSize; x++)
            {
                for (int y = startY; y < startY + height && y < TexSize; y++)
                {
                    Color source = Color.white;

                    if (_wallTexture != null)
                    {
                        int tx = x % _wallTexture.width;
                        int ty = y % _wallTexture.height;

                        source = _wallTexture.GetPixel(tx, ty);
                    }

                    pixels[y * TexSize + x] =
                        source * Color.Lerp(Color.white, _wallColor, 0.3f);
                }
            }
        }
    }
}
 