using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// The bedrock the floor is cut into: one tiled sprite under every room, so a room no longer
    /// floats in a black void (playtest finding 7).
    ///
    /// <para>It is <b>uniform</b> on purpose. Unexplored rooms are hidden by switching their
    /// renderers off, and anything drawn per room - a lighter patch, a carved outline - would show
    /// the player where the rooms they have not found yet are. One texture everywhere says
    /// nothing.</para>
    /// </summary>
    public static class DungeonBackdrop
    {
        /// <summary>Loaded when the level names no <see cref="LevelDefinitionSO.MapBackdrop"/>.</summary>
        public const string DefaultResourcePath = "DungeonBackdrops/bedrock";

        // Below every dungeon sprite (tiles 0, markers 2-3, walls 5) and far below the battle
        // stage's own background (400), so it never shows through a fight.
        private const int SortOrder = -100;

        // Behind the floor tiles, which sit at z = 1.
        private const float Depth = 2f;

        // How far past the outermost room the rock runs. At the camera's widest zoom (orthographic
        // size 11) the view is ~39 x 22 units, so a view centred on the floor's edge room still
        // lands on rock rather than on the camera's clear colour.
        private const float Margin = 40f;

        /// <summary>
        /// Places the backdrop under <paramref name="rooms"/> and returns it, or null when there is
        /// no sprite to draw (a missing sprite leaves the old black, which is still playable).
        /// </summary>
        public static GameObject Place(List<Room> rooms, Transform parent, LevelDefinitionSO level)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return null;
            }

            var sprite = level != null && level.MapBackdrop != null
                ? level.MapBackdrop
                : UnityEngine.Resources.Load<Sprite>(DefaultResourcePath);
            if (sprite == null)
            {
                return null;
            }

            // Tiles are centred on their grid position, so a room spans half a tile either side.
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var room in rooms)
            {
                if (room == null || room.RoomSO == null)
                {
                    continue;
                }

                var origin = room.GridPosition;
                min = Vector2.Min(min, new Vector2(origin.x - 0.5f, origin.y - 0.5f));
                max = Vector2.Max(max, new Vector2(
                    origin.x + room.RoomSO.Width - 0.5f,
                    origin.y + room.RoomSO.Height - 0.5f));
            }

            var centre = (min + max) * 0.5f;
            var size = (max - min) + Vector2.one * (Margin * 2f);

            var obj = new GameObject("DungeonBackdrop");
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3(centre.x, centre.y, Depth);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            sr.sortingOrder = SortOrder;
            return obj;
        }
    }
}
