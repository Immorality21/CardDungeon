using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Where the screen-space combat HUD sits, for the world-space readouts that must stay clear of
    /// it. Written by <c>RoomActionUI</c> every frame, read by <see cref="UnitHealthBar"/>.
    ///
    /// <para>Exists because a boss stands at the right of the stage and is tall, so its HP bar - a
    /// child of the unit, and scaled with it - ran under the top-right turn-order panel (playtest
    /// finding 12's leftover). Viewport coordinates, so the two sides never have to agree on a
    /// reference resolution.</para>
    /// </summary>
    public static class CombatHudLayout
    {
        /// <summary>
        /// The turn-order panel in viewport coordinates (0..1, origin bottom-left), or a zero-width
        /// rect while it is hidden.
        /// </summary>
        public static Rect TurnOrderViewport;
    }
}
