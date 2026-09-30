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
            if (panel.width <= 0f || panel.height <= 0f || halfExtents.x <= 0f || halfExtents.y <= 0f)
            {
                return target;
            }

            var padded = Rect.MinMaxRect(room.xMin - margin, room.yMin - margin, room.xMax + margin, room.yMax + margin);
            Rect panelWorld = PanelInWorld(target, halfExtents, panel);
            if (!padded.Overlaps(panelWorld))
            {
                return target;
            }

            // Camera left moves the room right on screen, clear of the panel's right edge; camera up
            // moves it down, clear of the panel's bottom edge. Each is taken only if the far side of
            // the room is still on screen afterwards.
            float left = panelWorld.xMax - padded.xMin;
            float up = padded.yMax - panelWorld.yMin;
            bool leftFits = padded.xMax <= target.x - left + halfExtents.x;
            bool upFits = padded.yMin >= target.y + up - halfExtents.y;

            if (leftFits && (!upFits || left <= up))
            {
                return new Vector2(target.x - left, target.y);
            }
            if (upFits)
            {
                return new Vector2(target.x, target.y + up);
            }
            return target;
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
