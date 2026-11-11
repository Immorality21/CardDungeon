using System;
using ImmoralityGaming.Localization;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// The Controls screen (<see cref="ControlsList"/>): a full-screen overlay holding one window that
    /// lists every key and mouse action. The same view-controller serves the dungeon's pause menu, the
    /// title screen's Options and the town, each over its own copy of the markup (<c>controls-overlay</c>
    /// with <c>controls-list</c> and <c>controls-back</c> - RoomAction.uxml, MainMenu.uxml, Hub.uxml).
    /// The rows are built here, like every list; the window itself is authored.
    ///
    /// <para><b>It takes the keyboard without the host's help.</b> <see cref="Attach"/> listens on the
    /// document root in the trickle-down phase, which runs before any screen's own key handler, so
    /// while it is open no key reaches the screen under it, and the host needs no new branch in its
    /// key routing. The overlay is the document's last child, so it also paints over everything and
    /// its scrim takes the mouse. Open it over a screen that is not ticking (the pause menu, Options,
    /// a hub screen) - a polled key (the dungeon's walking reads <c>Input.GetKey</c>) still sees through
    /// it, which is why the dungeon opens it only from pause.</para>
    /// </summary>
    public sealed class ControlsPanel
    {
        /// <summary>Pixels one Up/Down press scrolls the list.</summary>
        public const float ScrollStep = 60f;

        private readonly VisualElement _overlay;
        private readonly ScrollView _list;
        private readonly bool _openWithF1;

        /// <summary>Raised after the overlay closes, by key or by its Back button.</summary>
        public event Action Closed;

        public bool IsOpen => _overlay != null && !_overlay.ClassListContains(HiddenClass);

        private const string HiddenClass = "cd-hidden";

        private ControlsPanel(VisualElement overlay, bool openWithF1)
        {
            _overlay = overlay;
            _openWithF1 = openWithF1;
            _list = overlay.Q<ScrollView>("controls-list");
            if (_list != null)
            {
                _list.focusable = false;
            }
            var back = overlay.Q<Button>("controls-back");
            if (back != null)
            {
                back.focusable = false;
                back.clicked += Close;
            }
            _overlay.AddToClassList(HiddenClass);
        }

        /// <summary>
        /// Wires the overlay found under <paramref name="documentRoot"/> (by the name
        /// <c>controls-overlay</c>). Returns null when the document has none, so a scene without it
        /// simply has no Controls screen. <paramref name="openWithF1"/>: F1 opens it from anywhere in
        /// the document (the town and the title screen; not the dungeon, where walking polls the keys).
        /// </summary>
        public static ControlsPanel Attach(VisualElement documentRoot, bool openWithF1)
        {
            var overlay = documentRoot?.Q<VisualElement>("controls-overlay");
            if (overlay == null)
            {
                return null;
            }
            var panel = new ControlsPanel(overlay, openWithF1);
            documentRoot.RegisterCallback<KeyDownEvent>(panel.OnKeyDown, TrickleDown.TrickleDown);
            return panel;
        }

        /// <summary>A button that opens the screen (the pause menu's, Options', the town's).</summary>
        public void AddOpener(Button button)
        {
            if (button != null)
            {
                button.clicked += Open;
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            Build();
            _overlay.RemoveFromClassList(HiddenClass);
            // Last child: paints over every window and its scrim takes every click.
            _overlay.BringToFront();
            if (_list != null)
            {
                _list.scrollOffset = Vector2.zero;
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            _overlay.AddToClassList(HiddenClass);
            Closed?.Invoke();
        }

        /// <summary>Rebuilt on every open, so a language switch shows at once.</summary>
        private void Build()
        {
            if (_list == null)
            {
                return;
            }
            _list.Clear();
            foreach (var group in ControlsList.Groups)
            {
                var head = new Label(Loc.Get(group.TitleKey)) { pickingMode = PickingMode.Ignore };
                head.AddToClassList("cd-section-head");
                head.AddToClassList("cd-controls-head");
                _list.Add(head);
                foreach (var line in group.Lines)
                {
                    var row = new VisualElement { pickingMode = PickingMode.Ignore };
                    row.AddToClassList("cd-controls-row");
                    var keys = new Label(Loc.Get(line.KeysKey)) { pickingMode = PickingMode.Ignore };
                    keys.AddToClassList("cd-controls-keys");
                    var action = new Label(Loc.Get(line.ActionKey)) { pickingMode = PickingMode.Ignore };
                    action.AddToClassList("cd-controls-action");
                    row.Add(keys);
                    row.Add(action);
                    _list.Add(row);
                }
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen)
            {
                if (_openWithF1 && evt.keyCode == KeyCode.F1)
                {
                    Open();
                    evt.StopPropagation();
                }
                return;
            }
            HandleKey(evt.keyCode);
            // Open, it owns every key: nothing may reach the screen underneath.
            evt.StopPropagation();
        }

        /// <summary>The overlay's keys: the back keys (and F1) close it, Up/Down and Page Up/Down scroll.</summary>
        public void HandleKey(KeyCode key)
        {
            switch (ControlsKeys.ActionFor(key))
            {
                case ControlsKeys.Action.Close:
                    Close();
                    return;
                case ControlsKeys.Action.ScrollUp:
                    Scroll(-ScrollStep);
                    return;
                case ControlsKeys.Action.ScrollDown:
                    Scroll(ScrollStep);
                    return;
                case ControlsKeys.Action.PageUp:
                    Page(-1);
                    return;
                case ControlsKeys.Action.PageDown:
                    Page(1);
                    return;
            }
        }

        private void Scroll(float delta)
        {
            if (_list != null)
            {
                _list.scrollOffset += new Vector2(0f, delta);
            }
        }

        private void Page(int direction)
        {
            if (_list != null)
            {
                KeyboardNavigator.Page(_list, direction);
            }
        }
    }

    /// <summary>What each key does on the Controls screen. Pure, so the mapping is tested without a panel.</summary>
    public static class ControlsKeys
    {
        public enum Action
        {
            None,
            Close,
            ScrollUp,
            ScrollDown,
            PageUp,
            PageDown,
        }

        public static Action ActionFor(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Escape:
                case KeyCode.Backspace:
                case KeyCode.X:
                case KeyCode.C:
                case KeyCode.F1:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Z:
                case KeyCode.Space:
                    return Action.Close;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    return Action.ScrollUp;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    return Action.ScrollDown;
                case KeyCode.PageUp:
                    return Action.PageUp;
                case KeyCode.PageDown:
                    return Action.PageDown;
                default:
                    return Action.None;
            }
        }
    }
}
