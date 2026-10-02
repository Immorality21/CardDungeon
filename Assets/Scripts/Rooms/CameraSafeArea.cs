using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Keeps the room the party stands in out from under a screen-corner panel (the dungeon HUD).
    /// Pure, so the rule is testable without a camera.
    ///
    /// <para>The camera follows the party, not the room, so a party standing at a room's right-hand
    /// door leaves the rest of the room reaching toward the top-left - where the HUD is, and where it
    /// covered a door in the menu review (2026-09-28). Given where the camera <i>wants</i> to be, this
    /// returns the smallest shift, left or up, that puts the whole room clear of the panel. It only
    /// takes a shift that keeps the room on screen: a room too big to clear the panel is left where
    /// the follow put it, because showing the party matters more than showing the far corner.</para>
    ///
    /// <para>It is a function of the <i>target</i>, never of where the camera is now, so the follow
    /// lerp converges on one point instead of chasing its own nudge.</para>
    /// </summary>
    public static class CameraSafeArea
    {
        /// <summary>
        /// The camera position to use instead of <paramref name="target"/>.
        /// <paramref name="room"/> is the room's world rectangle; <paramref name="halfExtents"/> the
        /// camera's half width and half height in world units; <paramref name="panel"/> the panel's
        /// rectangle in viewport space (0..1, origin bottom-left, as <c>Camera.ViewportToWorldPoint</c>
        /// reads it). <paramref name="margin"/> is extra world space kept between room and panel.
        /// </summary>
        public static Vector2 Nudge(Rect room, Vector2 target, Vector2 halfExtents, Rect panel, float margin = 0f)
        {
            return Nudge(room, target, halfExtents, new[] { panel }, margin);
        }

        /// <summary>
        /// The same for several panels, most important first. Since playtest 2 finding 8 the room bar
        /// and the Fight/Flee bar (bottom centre) and the party window (bottom right) are protected as
        /// well as the HUD: an enemy on a room's bottom row stood under the Fight/Flee bar, and in one
        /// room the bar covered a door.
        ///
        /// <para>Of every shift that clears all the panels and keeps the room on screen, the shortest
        /// wins. When no shift clears them all, the least important panel is given up and the rest
        /// tried again, so a big room still keeps the important ones clear.</para>
        /// </summary>
        public static Vector2 Nudge(Rect room, Vector2 target, Vector2 halfExtents, IReadOnlyList<Rect> panels, float margin = 0f)
        {
            if (panels == null || halfExtents.x <= 0f || halfExtents.y <= 0f)
            {
                return target;
            }

            var padded = Rect.MinMaxRect(room.xMin - margin, room.yMin - margin, room.xMax + margin, room.yMax + margin);
            var world = new List<Rect>();
            foreach (var panel in panels)
            {
                if (panel.width > 0f && panel.height > 0f)
                {
                    world.Add(PanelInWorld(target, halfExtents, panel));
                }
            }

            for (int count = world.Count; count > 0; count--)
            {
                if (TryClear(padded, target, halfExtents, world, count, out Vector2 shift))
                {
                    return target + shift;
                }
            }
            return target;
        }

        /// <summary>
        /// The shortest camera shift that moves the first <paramref name="count"/> panels off the room
        /// while the room stays on screen. Each panel can be cleared four ways (camera left, right, up or
        /// down by just enough), so the answer is among those offsets per axis, or zero.
        /// </summary>
        private static bool TryClear(Rect room, Vector2 target, Vector2 halfExtents, List<Rect> panels, int count, out Vector2 shift)
        {
            var xs = new List<float> { 0f };
            var ys = new List<float> { 0f };
            for (int i = 0; i < count; i++)
            {
                // Camera left moves the panel left on the world: its right edge to the room's left.
                xs.Add(room.xMin - panels[i].xMax);
                xs.Add(room.xMax - panels[i].xMin);
                ys.Add(room.yMin - panels[i].yMax);
                ys.Add(room.yMax - panels[i].yMin);
            }

            shift = Vector2.zero;
            float best = float.MaxValue;
            foreach (float dx in xs)
            {
                foreach (float dy in ys)
                {
                    float cost = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (cost >= best || !Clears(room, target, halfExtents, panels, count, dx, dy))
                    {
                        continue;
                    }
                    best = cost;
                    shift = new Vector2(dx, dy);
                }
            }
            return best < float.MaxValue;
        }

        private static bool Clears(Rect room, Vector2 target, Vector2 halfExtents, List<Rect> panels, int count, float dx, float dy)
        {
            const float Epsilon = 1e-4f;
            var cam = new Vector2(target.x + dx, target.y + dy);
            if (room.xMin < cam.x - halfExtents.x - Epsilon || room.xMax > cam.x + halfExtents.x + Epsilon
                || room.yMin < cam.y - halfExtents.y - Epsilon || room.yMax > cam.y + halfExtents.y + Epsilon)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                var moved = new Rect(panels[i].x + dx, panels[i].y + dy, panels[i].width, panels[i].height);
                if (Overlaps(room, moved, Epsilon))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Strict overlap with a little slack, so a shift computed to touch an edge counts as clear.</summary>
        private static bool Overlaps(Rect a, Rect b, float epsilon)
        {
            return a.xMin < b.xMax - epsilon && a.xMax > b.xMin + epsilon
                && a.yMin < b.yMax - epsilon && a.yMax > b.yMin + epsilon;
        }

        /// <summary>A viewport-space rectangle as world space, for a camera centred on <paramref name="cameraPos"/>.</summary>
        public static Rect PanelInWorld(Vector2 cameraPos, Vector2 halfExtents, Rect panel)
        {
            float left = cameraPos.x - halfExtents.x;
            float bottom = cameraPos.y - halfExtents.y;
            float width = halfExtents.x * 2f;
            float height = halfExtents.y * 2f;
            return Rect.MinMaxRect(
                left + panel.xMin * width,
                bottom + panel.yMin * height,
                left + panel.xMax * width,
                bottom + panel.yMax * height);
        }
    }
}
