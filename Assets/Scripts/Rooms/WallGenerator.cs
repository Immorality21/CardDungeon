using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Generates wall overlay sprites at runtime and places them around room perimeters.
    /// Uses a bitmask approach: Top=1, Right=2, Bottom=4, Left=8.
    /// Walls are skipped on edges where doors exist.
    /// </summary>
    public class WallGenerator
    {
        private const int TexSize = 32;
        private const int WallThickness = 4;

        // Edge bitmask flags
        private const int Top = 1;
        private const int Right = 2;
        private const int Bottom = 4;
        private const int Left = 8;

        private readonly Dictionary<int, Sprite> _wallSprites = new Dictionary<int, Sprite>();
        private readonly Color _wallColor;
        private readonly int _sortingOrder;

        public WallGenerator(Color wallColor, int sortingOrder = 5)
        {
            _wallColor = wallColor;
            _sortingOrder = sortingOrder;
            GenerateSprites();
        }

        /// <summary>
        /// Places walls around all rooms, skipping door edges.
        /// Walls appear on any edge where the neighbor tile belongs to a different room or is empty.
        /// Adjacent rooms each draw their own wall, giving a double-width wall between rooms.
        /// </summary>
        public void PlaceWalls(List<Room> rooms)
        {
            var tileOwner = BuildTileOwnerMap(rooms);

            foreach (var room in rooms)
            {
                PlaceRoomWalls(room, tileOwner);
            }
        }

        private void PlaceRoomWalls(Room room, Dictionary<Vector2Int, Room> tileOwner)
        {
            var origin = room.GridPosition;
            var w = room.RoomSO.Width;
            var h = room.RoomSO.Height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var tilePos = origin + new Vector2Int(x, y);
                    int mask = 0;

                    if (!IsSameRoom(tileOwner, tilePos + Vector2Int.up, room))
                        mask |= Top;
                    if (!IsSameRoom(tileOwner, tilePos + Vector2Int.right, room))
                        mask |= Right;
                    if (!IsSameRoom(tileOwner, tilePos + Vector2Int.down, room))
                        mask |= Bottom;
                    if (!IsSameRoom(tileOwner, tilePos + Vector2Int.left, room))
                        mask |= Left;

                    if (mask == 0) continue;

                    var wallObj = new GameObject($"Wall_{tilePos.x}_{tilePos.y}");
                    wallObj.transform.SetParent(room.transform, false);
                    wallObj.transform.position = new Vector3(tilePos.x, tilePos.y, -0.5f);

                    var sr = wallObj.AddComponent<SpriteRenderer>();
                    sr.sprite = _wallSprites[mask];
                    sr.sortingOrder = _sortingOrder;
                }
            }
        }

        private static bool IsSameRoom(Dictionary<Vector2Int, Room> tileOwner, Vector2Int pos, Room room)
        {
            return tileOwner.TryGetValue(pos, out var owner) && owner == room;
        }

        private static Dictionary<Vector2Int, Room> BuildTileOwnerMap(List<Room> rooms)
        {
            var map = new Dictionary<Vector2Int, Room>();
            foreach (var room in rooms)
            {
                var origin = room.GridPosition;
                for (int x = 0; x < room.RoomSO.Width; x++)
                {
                    for (int y = 0; y < room.RoomSO.Height; y++)
                    {
                        map[origin + new Vector2Int(x, y)] = room;
                    }
                }
            }
            return map;
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

                tex.SetPixels(pixels);
                tex.Apply();

                var sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
                sprite.name = $"Wall_{mask}";
                _wallSprites[mask] = sprite;
            }
        }

        private void FillRect(Color[] pixels, int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width && x < TexSize; x++)
            {
                for (int y = startY; y < startY + height && y < TexSize; y++)
                {
                    pixels[y * TexSize + x] = _wallColor;
                }
            }
        }
    }
}
 