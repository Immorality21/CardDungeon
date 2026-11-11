using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Progression;
using Assets.Scripts.Tutorial;
using ImmoralityGaming.Localization;
using ImmoralityGaming.Menu;
using UnityEngine.UIElements;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// The room's screen, by job: the first-time hints (docs/TUTORIAL.md, "First-time hints") - a line
    /// under the top banners the first time a rule matters, and the pause menu's Hints window that lists
    /// every line seen. A hint never asks anything and never takes the keyboard: it shows for
    /// <see cref="HintNoticeMs"/> and goes, and two at once take turns.
    /// </summary>
    public partial class RoomActionUI
    {
        // Long enough to read two sentences without hurrying; a fight's turn does not wait for it.
        private const long HintNoticeMs = 9000;

        private VisualElement _hintNotice;
        private Label _hintNoticeText;
        private readonly Queue<string> _hintQueue = new Queue<string>();
        private int _hintNoticeShown;

        private VisualElement _hintsWindow;
        private ScrollView _hintsList;
        private Button _hintsBack;
        private Button _pauseHints;
        private KeyboardNavigator _hintsNav;
        private bool _hintsFromPause;

        private TutorialSO _tutorialLines;

        private TutorialSO TutorialLines
        {
            get
            {
                if (_tutorialLines == null)
                {
                    _tutorialLines = UnityEngine.Resources.Load<TutorialSO>(TutorialSO.ResourcePath);
                }
                return _tutorialLines;
            }
        }

        private void BindHints(VisualElement root)
        {
            _hintNotice = root.Q<VisualElement>("hint-notice");
            _hintNoticeText = root.Q<Label>("hint-notice-text");
            _hintsWindow = root.Q<VisualElement>("hints-window");
            _hintsList = root.Q<ScrollView>("hints-list");
            _hintsBack = root.Q<Button>("hints-back");
            _pauseHints = root.Q<Button>("pause-hints");
            if (_hintsWindow != null)
            {
                _hintsNav = new KeyboardNavigator(_hintsWindow);
                _hintsNav.Cancelled += CloseHints;
            }
            if (_hintsBack != null)
            {
                _hintsBack.clicked += CloseHints;
            }
            if (_pauseHints != null)
            {
                _pauseHints.clicked += () => OpenHints(fromPause: true);
            }

            // The Controls screen opens over the pause menu, which stays up beneath it: pause already
            // stops the walking and the room's keys, and the panel takes every key while it is open
            // (Tutorial/ControlsPanel), so nothing else here has to know about it. No F1: walking
            // polls the arrows, and pause is what makes the room hold still.
            _controls = ControlsPanel.Attach(root, openWithF1: false);
            _controls?.AddOpener(root.Q<Button>("pause-controls"));
        }

        private ControlsPanel _controls;

        /// <summary>
        /// Says <paramref name="cue"/>'s line if this save has never seen it, and writes it down. Safe to
        /// call every time the moment comes: it is a lookup until the first time.
        /// </summary>
        private void ShowHintOnce(TutorialCue cue)
        {
            if (_hintNotice == null || !MetaProgressManager.HasInstance || MetaProgressManager.Instance.HasSeenHint(cue))
            {
                return;
            }
            string text = HintText(cue);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            MetaProgressManager.Instance.MarkHintSeen(cue);
            _hintQueue.Enqueue(text);
            if (!IsShown(_hintNotice))
            {
                ShowNextHint();
            }
        }

        private void ShowNextHint()
        {
            if (_hintQueue.Count == 0)
            {
                SetShown(_hintNotice, false);
                return;
            }
            _hintNoticeText.text = _hintQueue.Dequeue();
            SetShown(_hintNotice, true);
            int shown = ++_hintNoticeShown;
            _hintNotice.schedule.Execute(() =>
            {
                if (shown == _hintNoticeShown)
                {
                    ShowNextHint();
                }
            }).StartingIn(HintNoticeMs);
        }

        /// <summary>
        /// A cue's words, ready to read out of context: the challenge warning's "{run}" becomes the
        /// campaign's challenge level, since the Hints window has no story map to name it from.
        /// </summary>
        private string HintText(TutorialCue cue)
        {
            var tutorial = TutorialLines;
            string text = tutorial != null ? tutorial.TextFor(cue) : string.Empty;
            if (string.IsNullOrEmpty(text) || !text.Contains("{run}"))
            {
                return text;
            }
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var challenge = campaign != null && campaign.Nodes != null
                ? campaign.Nodes.Select(n => n?.Run).FirstOrDefault(r => r != null && r.Challenge)
                : null;
            return text.Replace("{run}", challenge != null ? CampaignOps.DisplayNameOf(challenge) : string.Empty);
        }

        // ------------------------------------------------------------------ the Hints window

        private void OpenHints(bool fromPause)
        {
            if (_hintsWindow == null)
            {
                return;
            }
            _hintsFromPause = fromPause;
            if (fromPause)
            {
                ClosePause();
            }

            _hintsList?.Clear();
            var seen = MetaProgressManager.HasInstance
                ? TutorialHints.SeenInOrder(MetaProgressManager.Instance.HintsSeen)
                : new List<TutorialCue>();
            if (seen.Count == 0)
            {
                AddHintEntry(Loc.Get("room.hints.none"));
            }
            foreach (var cue in seen)
            {
                AddHintEntry(HintText(cue));
            }

            SetShown(_hintsWindow, true);
            _hintsNav?.Reset();
            FocusRoot();
        }

        private void AddHintEntry(string text)
        {
            if (_hintsList == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            var entry = new Label(text) { pickingMode = PickingMode.Ignore };
            entry.AddToClassList("cd-hints-entry");
            _hintsList.Add(entry);
        }

        private void CloseHints()
        {
            if (_hintsWindow == null || !IsShown(_hintsWindow))
            {
                return;
            }
            SetShown(_hintsWindow, false);
            _hintsNav?.Reset();
            bool backToPause = _hintsFromPause;
            _hintsFromPause = false;
            if (backToPause)
            {
                OpenPause();
                return;
            }
            FocusRoot();
        }

        private bool HintsOpen => _hintsWindow != null && IsShown(_hintsWindow);

        /// <summary>The Hints window's keys: Escape and its kin go back, the rest move the cursor (only Back).</summary>
        private void HandleHintsKey(KeyDownEvent evt)
        {
            if (evt.keyCode == UnityEngine.KeyCode.Escape || evt.keyCode == UnityEngine.KeyCode.Backspace
                || evt.keyCode == UnityEngine.KeyCode.X || evt.keyCode == UnityEngine.KeyCode.C)
            {
                CloseHints();
                return;
            }
            if (evt.keyCode == UnityEngine.KeyCode.UpArrow && _hintsList != null)
            {
                _hintsList.scrollOffset -= new UnityEngine.Vector2(0f, 60f);
                return;
            }
            if (evt.keyCode == UnityEngine.KeyCode.DownArrow && _hintsList != null)
            {
                _hintsList.scrollOffset += new UnityEngine.Vector2(0f, 60f);
                return;
            }
            _hintsNav?.HandleKey(evt);
        }
    }
}
