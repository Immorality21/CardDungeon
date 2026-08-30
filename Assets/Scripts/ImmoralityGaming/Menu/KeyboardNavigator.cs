using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Menu
{
    /// <summary>
    /// An arrow-key cursor over whatever buttons a UI Toolkit subtree is currently showing, so a
    /// screen becomes keyboard-navigable without listing its controls by hand.
    ///
    /// <para>The candidate list is rescanned on every key press rather than cached. Hub screens
    /// rebuild their rows constantly - buying a potion, equipping a sword, recruiting a hero all
    /// replace whole lists - and a cached list would hand the player a cursor pointing at a button
    /// that no longer exists. Rescanning a menu-sized tree costs nothing at key-press rates.</para>
    ///
    /// <para>There is deliberately <b>no cursor until the first arrow key</b>. Showing one the moment
    /// a screen opens would put a keyboard highlight on a mouse player's screen forever; this way the
    /// cursor appears exactly when it is asked for, and the timing question of whether layout has run
    /// yet never arises (it always has, by the time a key is pressed). A bar that is *asking a
    /// question* rather than offering destinations - Fight or Flee - opts out with
    /// <see cref="SelectFirst"/> so Enter alone answers it.</para>
    ///
    /// <para>Screens that already own their arrow keys - the inventory, the magic picker, the combat
    /// command menu - are not driven by this and keep their hand-built cursors.</para>
    /// </summary>
    public class KeyboardNavigator
    {
        /// <summary>Marks the button under the cursor. Styled in <c>CardDungeon.uss</c>.</summary>
        public const string SelectedClass = "cd-nav--selected";

        /// <summary>Opts a button out of the cursor entirely.</summary>
        public const string SkipClass = "cd-nav-skip";

        private readonly VisualElement _root;
        private readonly List<VisualElement> _items = new List<VisualElement>();
        private readonly List<Vector2> _centers = new List<Vector2>();

        private VisualElement _selected;
        private VisualElement _styled;

        public KeyboardNavigator(VisualElement root)
        {
            _root = root;
        }

        /// <summary>Raised by Escape/Backspace. With nothing subscribed those keys are left alone.</summary>
        public event Action Cancelled;

        /// <summary>
        /// Whether the cursor is currently on something. Screens that share Enter with another
        /// mechanic - the room bar shares it with walking through the selected door - need to know
        /// whether this cursor is the one that should answer.
        /// </summary>
        public bool HasSelection => _selected != null;

        /// <summary>Drops the cursor - call it whenever the screen underneath changes.</summary>
        public void Reset()
        {
            Select(null);
        }

        /// <summary>
        /// Puts the cursor on the first navigable button, for a bar that should answer Enter without
        /// an arrow press first. Does nothing once something is selected, so it is safe to call every
        /// frame - which is also how a caller sidesteps the layout timing: on the frame a bar is shown
        /// its resolved style is still stale, and by the next one it is not.
        /// </summary>
        public bool SelectFirst()
        {
            if (_selected != null)
            {
                return true;
            }

            Rebuild();
            if (_items.Count == 0)
            {
                return false;
            }

            Select(_items[0]);
            return true;
        }

        /// <summary>
        /// Handles one key. Returns whether it was used, so the caller can decide about
        /// <c>StopPropagation</c>: an unused arrow still belongs to whatever else is listening.
        /// </summary>
        public bool HandleKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    return Move(new Vector2(0f, -1f), -1);
                case KeyCode.DownArrow:
                    return Move(new Vector2(0f, 1f), 1);
                case KeyCode.LeftArrow:
                    return Move(new Vector2(-1f, 0f), 0);
                case KeyCode.RightArrow:
                    return Move(new Vector2(1f, 0f), 0);
                case KeyCode.Tab:
                    return Step(evt.shiftKey ? -1 : 1);
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    return Activate();
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    if (Cancelled == null)
                    {
                        return false;
                    }
                    Cancelled.Invoke();
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Moves the cursor. <paramref name="fallbackStep"/> is what to do when nothing lies that way:
        /// up/down fall back to the previous/next button in document order (so a plain vertical list
        /// wraps at its ends), while left/right pass 0 and simply do not consume the key - on a
        /// single-column menu there is nothing sideways to go to.
        /// </summary>
        private bool Move(Vector2 direction, int fallbackStep)
        {
            Rebuild();
            if (_items.Count == 0)
            {
                return false;
            }

            if (_selected == null)
            {
                Select(_items[0]);
                return true;
            }

            int from = _items.IndexOf(_selected);
            int target = DirectionalNav.PickInDirection(_centers, from, direction);
            if (target >= 0)
            {
                Select(_items[target]);
                return true;
            }

            return fallbackStep != 0 && Step(fallbackStep);
        }

        /// <summary>Document-order move, wrapping at both ends.</summary>
        private bool Step(int delta)
        {
            Rebuild();
            if (_items.Count == 0)
            {
                return false;
            }

            int index = _selected != null ? _items.IndexOf(_selected) : -1;
            if (index < 0)
            {
                Select(delta > 0 ? _items[0] : _items[_items.Count - 1]);
                return true;
            }

            Select(_items[(index + delta + _items.Count) % _items.Count]);
            return true;
        }

        /// <summary>Presses the button under the cursor.</summary>
        private bool Activate()
        {
            Rebuild();
            return Press(_selected as Button);
        }

        /// <summary>
        /// Presses a button from code, as if the player had submitted on it. A
        /// <see cref="NavigationSubmitEvent"/> is what UI Toolkit itself sends when Enter is pressed
        /// on a focused button, so this goes through the control's own handling rather than reaching
        /// around it - which matters because most of the buttons here are deliberately
        /// <c>focusable = false</c> (the screens turn off UI Toolkit's built-in focus navigation so it
        /// cannot fight their cursors) and so would never receive that event by the ordinary route.
        ///
        /// <para>Public because screens need the same thing for keys the cursor does not own -
        /// Escape pressing a Back button, say.</para>
        /// </summary>
        public static bool Press(Button button)
        {
            if (button == null || button.panel == null)
            {
                return false;
            }

            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            return true;
        }

        private void Select(VisualElement element)
        {
            if (_styled != null && _styled != element)
            {
                _styled.EnableInClassList(SelectedClass, false);
            }

            _selected = element;
            _styled = element;

            if (element == null)
            {
                return;
            }

            element.EnableInClassList(SelectedClass, true);
            ScrollIntoView(element);
        }

        private void Rebuild()
        {
            // Where the cursor sat in the *old* list, so a screen that replaced its rows under us
            // keeps the player in roughly the same place instead of snapping back to the top.
            int previousIndex = _selected != null ? _items.IndexOf(_selected) : -1;

            _items.Clear();
            _centers.Clear();

            if (_root == null)
            {
                _selected = null;
                return;
            }

            _root.Query<VisualElement>().ForEach(Consider);

            if (_selected == null || _items.Contains(_selected))
            {
                return;
            }

            var replacement = _items.Count == 0
                ? null
                : _items[Mathf.Clamp(previousIndex, 0, _items.Count - 1)];
            Select(replacement);
        }

        private void Consider(VisualElement element)
        {
            if (!(element is Button) || element.ClassListContains(SkipClass) || !IsUsable(element))
            {
                return;
            }

            _items.Add(element);
            _centers.Add(element.worldBound.center);
        }

        /// <summary>
        /// Whether a button is really on screen and reachable. Visibility is read from the *resolved*
        /// style rather than from any hidden-marker class: screens here toggle an inline
        /// <c>style.display</c> that outranks the class in USS but never removes it, so a shown view
        /// still carries its <c>cd-hidden</c> class and testing for that class would find nothing
        /// navigable anywhere. Resolved style is authoritative, and by the time a key is pressed
        /// layout has long since run.
        /// </summary>
        private static bool IsUsable(VisualElement element)
        {
            if (element.panel == null || !element.enabledInHierarchy)
            {
                return false;
            }

            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None
                    || e.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ScrollIntoView(VisualElement element)
        {
            for (var e = element.hierarchy.parent; e != null; e = e.hierarchy.parent)
            {
                if (e is ScrollView scroll)
                {
                    scroll.ScrollTo(element);
                    return;
                }
            }
        }
    }
}
