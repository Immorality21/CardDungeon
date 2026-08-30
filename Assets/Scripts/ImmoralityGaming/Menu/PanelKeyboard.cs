using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Menu
{
    /// <summary>
    /// Makes the OS keyboard actually reach a runtime UI Toolkit panel.
    ///
    /// <para>Focusing an element inside the panel - <c>root.Focus()</c> - is only half of it, and it is
    /// the half that is easy to mistake for the whole. At runtime the bridge that turns OS key presses
    /// into UI Toolkit <see cref="KeyDownEvent"/>s is a <see cref="PanelEventHandler"/>, and the
    /// EventSystem pumps keys into it only while it is the <b>selected</b> GameObject (that is what
    /// <c>IUpdateSelectedHandler</c> means). Pointer events take a different route entirely, through
    /// the panel's raycaster, which needs no selection - so a panel whose handler was never selected
    /// clicks perfectly and ignores every single key.</para>
    ///
    /// <para>Clicking any UI Toolkit element selects the handler as a side effect, which is why a menu
    /// the player <i>enters by clicking a button</i> appears to work without any of this. A screen
    /// reached without ever touching UI does not: in a dungeon room the doors are world-space
    /// colliders, so a player can walk the whole floor by mouse and never once select the panel, and
    /// every arrow key falls on the floor. Worse, clicking outside the UI actively clears the
    /// selection, so a screen that worked a moment ago stops.</para>
    ///
    /// <para><see cref="Claim"/> closes that gap, and is cheap enough to call every frame from
    /// whatever is currently driving the keyboard.</para>
    /// </summary>
    public static class PanelKeyboard
    {
        private static PanelEventHandler _handler;

        /// <summary>
        /// Points the EventSystem's selection at the UI Toolkit keyboard bridge, so key presses reach
        /// the panel.
        ///
        /// <para>Only ever claims an <i>empty</i> selection: whatever else is selected wanted the
        /// keyboard more recently than we did, and stealing it every frame would make any other
        /// selectable unusable. Harmless to call repeatedly - once the handler is selected this is a
        /// reference comparison.</para>
        /// </summary>
        public static void Claim()
        {
            var events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject != null)
            {
                return;
            }

            // Unity creates the handler at runtime beside the PanelSettings asset, so it cannot be
            // wired in the inspector and has to be found. One handler serves every UIDocument sharing
            // those settings; which panel then receives the keys is ordinary UI Toolkit focus, and
            // stays the caller's business.
            if (_handler == null)
            {
                _handler = Object.FindFirstObjectByType<PanelEventHandler>();
                if (_handler == null)
                {
                    return;
                }
            }

            events.SetSelectedGameObject(_handler.gameObject);
        }
    }
}
