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
                _handler = Object.FindAnyObjectByType<PanelEventHandler>();
                if (_handler == null)
                {
                    return;
                }
            }

            events.SetSelectedGameObject(_handler.gameObject);
        }

        /// <summary>
        /// Stops UI Toolkit from moving keyboard focus on its own anywhere under
        /// <paramref name="documentRoot"/>. Register it once, for a document whose screens drive their
        /// own cursors (every screen in this game).
        ///
        /// <para>Key presses go to the <i>focused</i> element and bubble up from there, so a screen's
        /// key handler only hears them while focus sits on its root or inside it. UI Toolkit moves that
        /// focus in two ways no screen asked for, and either one leaves the screen deaf
        /// (playtest 4 findings 12, 45, 46, 60):</para>
        /// <list type="bullet">
        /// <item><b>Every arrow key</b> also sends a <see cref="NavigationMoveEvent"/>, and its focus
        /// move runs after dispatch unless the focus controller is told to ignore it -
        /// <c>StopPropagation</c> does <b>not</b> stop it in Unity 6 (<c>FocusController.SwitchFocusOnEvent</c>
        /// only checks <c>processedByFocusController</c>). The 2D search stops at the first focusable
        /// element it meets walking down from the panel, which is the document root itself, so the
        /// first arrow worked and moved focus up to the root; every key after it bubbled past the
        /// screen that wanted it.</item>
        /// <item><b>A click on a non-focusable element</b> (every cursor-driven button here is
        /// <c>focusable = false</c>) <i>blurs</i> the focused element rather than leaving it alone, so
        /// after one mouse click the keys went to the bare panel root and nothing heard them.</item>
        /// </list>
        /// <para>Clicks on a text field still focus it, so typing keeps working.</para>
        /// </summary>
        public static void HoldFocus(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                return;
            }

            documentRoot.RegisterCallback<NavigationMoveEvent>(
                evt => IgnoreFocusChange(documentRoot, evt), TrickleDown.TrickleDown);
            documentRoot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!TakesFocusOnClick(evt.target as VisualElement))
                {
                    IgnoreFocusChange(documentRoot, evt);
                }
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Gives keyboard focus back to <paramref name="owner"/> when it has strayed: nothing focused,
        /// or something focused that is neither <paramref name="owner"/> nor inside it. Cheap enough
        /// to call every frame with whichever element should hear the keys right now - the safety net
        /// under <see cref="HoldFocus"/> for whatever else takes focus (a view hidden under the focused
        /// element blurs it, for one). Returns whether it moved focus.
        /// </summary>
        public static bool KeepFocusIn(VisualElement owner)
        {
            if (owner == null || owner.panel == null || !owner.canGrabFocus)
            {
                return false;
            }

            var focused = owner.focusController?.focusedElement as VisualElement;
            if (focused != null && (focused == owner || owner.Contains(focused)))
            {
                return false;
            }

            owner.Focus();
            return true;
        }

        private static void IgnoreFocusChange(VisualElement root, EventBase evt)
        {
            root.focusController?.IgnoreEvent(evt);
        }

        /// <summary>Whether a click on <paramref name="target"/> should move focus: only into something you type in.</summary>
        private static bool TakesFocusOnClick(VisualElement target)
        {
            for (var element = target; element != null; element = element.hierarchy.parent)
            {
                if (element.ClassListContains(TextInputBaseField<string>.ussClassName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
