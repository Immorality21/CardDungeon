using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The story line: every run in the campaign drawn as a graph, with the ones this save has cleared
    /// behind it and the ones it has opened ahead. Replaces the old single "New Run" button, which
    /// could only ever start the one run the menu happened to hold a reference to.
    ///
    /// <para>Pure wiring, like the other hub screens - <c>CampaignOps</c> decides what is startable and
    /// <see cref="CampaignPresenter"/> decides how it looks. Choosing a run does not start it here
    /// either: the screen raises <see cref="OnRunChosen"/> and <c>MainMenuManager</c> owns the run
    /// save, so there is still exactly one place that writes <c>Run.json</c>.</para>
    /// </summary>
    public class CampaignMapUI
    {
        private readonly VisualElement _root;
        private readonly CampaignSO _campaign;
        private readonly PartyRosterSO _roster;
        private readonly SphereGridView _view;

        private readonly Label _titleLabel;
        private readonly Label _detailName;
        private readonly Label _detailStatus;
        private readonly Label _detailBlurb;
        private readonly Label _detailRequires;
        private readonly VisualElement _detailBody;
        private int _activeLevelIndex;
        private readonly Button _startButton;
        private readonly Label _feedback;
        private readonly Button _closeButton;

        private readonly List<SphereGridView.NodeInfo> _nodeBuffer = new List<SphereGridView.NodeInfo>();
        private readonly List<(string A, string B)> _edgeBuffer = new List<(string A, string B)>();

        private List<CampaignNodeState> _states = new List<CampaignNodeState>();
        private string _selectedKey;
        private string _activeRunKey = string.Empty;
        private bool _isShown;

        /// <summary>Raised with the chosen run when the player commits to starting or continuing it.</summary>
        public event Action<RunDefinitionSO> OnRunChosen;

        public event Action OnClosed;

        public CampaignMapUI(VisualElement root, CampaignSO campaign, PartyRosterSO roster = null)
        {
            _root = root;
            _campaign = campaign;
            _roster = roster;

            _titleLabel = root.Q<Label>("campaign-title");
            _detailName = root.Q<Label>("campaign-detail-name");
            _detailStatus = root.Q<Label>("campaign-detail-status");
            _detailBlurb = root.Q<Label>("campaign-detail-blurb");
            _detailRequires = root.Q<Label>("campaign-detail-requires");
            _detailBody = root.Q<VisualElement>("campaign-detail-body");
            _startButton = root.Q<Button>("campaign-start");
            _feedback = root.Q<Label>("campaign-feedback");
            _closeButton = root.Q<Button>("campaign-close");

            // The graph widget is added in code, like the sphere grid's - it is a custom
            // VisualElement, so it cannot be declared in UXML.
            var graphHost = root.Q<VisualElement>("campaign-graph");
            if (graphHost != null)
            {
                _view = new SphereGridView
                {
                    StateClassNames = CampaignPresenter.StateClasses,
                    EdgeStrongStateClass = CampaignPresenter.CompletedClass,
                    EdgeOpenStateClass = CampaignPresenter.AvailableClass
                };
                _view.NodeClicked += OnNodeClicked;
                graphHost.Add(_view);
            }

            if (_startButton != null)
            {
                _startButton.clicked += OnStart;
                _startButton.focusable = false;
            }
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
                _closeButton.focusable = false;
            }

            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Opens the map. <paramref name="activeRunKey"/> is <c>RunSaveData.RunKey</c> - the manager
        /// already holds the run save, so the screen is told rather than re-reading it from disk.
        /// </summary>
        public void Show(string activeRunKey, int activeLevelIndex = 0)
        {
            _activeRunKey = activeRunKey ?? string.Empty;
            _activeLevelIndex = activeLevelIndex;
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            _root.focusable = true;
            SetFeedback(string.Empty);

            // Always open on the default pick - the run in progress, else the first one open - so
            // Enter does the obvious thing. Remembering the last node meant the map could reopen
            // on a locked run, where Enter only says "That way is still closed." (2026-09-30).
            _selectedKey = null;
            RebuildGraph();
            Refresh();

            if (_view != null)
            {
                _view.FrameAll();
            }
            if (_root.panel != null)
            {
                _root.Focus();
            }
        }

        public void Hide()
        {
            _isShown = false;
            _root.focusable = false;
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        // --- Rendering ---------------------------------------------------------------------

        /// <summary>
        /// Shape only. Rebuilt on every open rather than cached, because clearing a secret run changes
        /// which nodes exist at all - a cached graph would keep a discovered branch invisible until
        /// the game was restarted.
        /// </summary>
        private void RebuildGraph()
        {
            if (_view == null)
            {
                return;
            }
            _states = BuildStates();
            CampaignPresenter.BuildViewModel(_campaign, _states, _nodeBuffer, _edgeBuffer);
            _view.SetGraph(_nodeBuffer, _edgeBuffer);

            if (!string.IsNullOrEmpty(_selectedKey) && FindState(_selectedKey) == null)
            {
                _selectedKey = null;
            }
            if (string.IsNullOrEmpty(_selectedKey))
            {
                _selectedKey = DefaultSelection();
            }
        }

        /// <summary>State only - node classes and the detail panel.</summary>
        private void Refresh()
        {
            if (_titleLabel != null && _campaign != null && !string.IsNullOrEmpty(_campaign.DisplayName))
            {
                _titleLabel.text = _campaign.DisplayName;
            }

            if (_view != null)
            {
                foreach (var state in _states)
                {
                    if (state?.Node?.Run == null || !state.IsVisible)
                    {
                        continue;
                    }
                    _view.SetNodeState(CampaignOps.RunKeyOf(state.Node.Run),
                        CampaignPresenter.StateClass(state.Status));
                }
                _view.SetSelected(_selectedKey);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            var selected = FindState(_selectedKey);

            if (selected?.Node?.Run == null)
            {
                SetText(_detailName, "Nowhere yet");
                SetText(_detailStatus, string.Empty);
                SetText(_detailBlurb, _states.Count == 0
                    ? "No campaign is authored. Create a Campaign asset in Resources."
                    : "Pick a place on the map.");
                SetText(_detailRequires, string.Empty);
                SetShown(_startButton, false);
                _detailBody?.Clear();
                return;
            }

            var run = selected.Node.Run;
            int floors = run.Levels.Count;
            bool inProgress = selected.Status == CampaignNodeStatus.InProgress;
            int current = Mathf.Clamp(_activeLevelIndex, 0, Mathf.Max(0, floors - 1));
            SetText(_detailName, CampaignOps.DisplayNameOf(run));
            // Where you are, not how long it is: "Level 2 of 4" for the run underway.
            SetText(_detailStatus, inProgress
                ? $"In progress · Level {current + 1} of {floors}"
                : $"{CampaignPresenter.StatusLabel(selected)} · {floors} {(floors == 1 ? "floor" : "floors")}");
            SetText(_detailBlurb, run.Blurb);
            BuildDetailBody(selected, inProgress, current);

            if (selected.Status == CampaignNodeStatus.Locked)
            {
                var lines = new List<string>();
                if (selected.MissingRequirements.Count > 0)
                {
                    lines.Add("Requires: " + string.Join(", ", selected.MissingRequirements));
                }
                if (selected.MissingHeroes.Count > 0)
                {
                    lines.Add("Needs in your roster: " + string.Join(", ", selected.MissingHeroes));
                }
                SetText(_detailRequires, string.Join("\n", lines));
            }
            else
            {
                SetText(_detailRequires, string.Empty);
            }

            bool actionable = selected.CanStart || selected.CanContinue;
            SetShown(_startButton, actionable);
            if (actionable && _startButton != null)
            {
                _startButton.text = selected.CanContinue
                    ? "Continue"
                    : selected.Status == CampaignNodeStatus.Completed ? "Run again" : "Begin";
            }
        }

        /// <summary>
        /// The room the old panel left empty: every floor of the run (cleared, the one you are on, the
        /// rest ahead) and which runs clearing it opens - the map is where a run is chosen, so it
        /// should say what the choice leads to.
        /// </summary>
        private void BuildDetailBody(CampaignNodeState selected, bool inProgress, int current)
        {
            if (_detailBody == null)
            {
                return;
            }
            _detailBody.Clear();
            var run = selected.Node.Run;
            bool cleared = selected.Status == CampaignNodeStatus.Completed;

            _detailBody.Add(MakeLabel("Floors", "cd-inv-col__title"));
            for (int i = 0; i < run.Levels.Count; i++)
            {
                var level = run.Levels[i];
                string name = level != null && !string.IsNullOrEmpty(level.LevelName) ? level.LevelName : $"Floor {i + 1}";
                bool done = cleared || (inProgress && i < current);
                bool here = inProgress && i == current;
                string mark = done ? "✓" : here ? "▸" : "·";
                _detailBody.Add(MakeLabel($"{mark}  {name}", "cm-floor",
                    done ? "cm-floor--done" : here ? "cm-floor--current" : "cm-floor--ahead"));
            }

            var opens = new List<string>();
            foreach (var state in _states)
            {
                if (state?.Node?.Run == null || !state.IsVisible || state.Node.Requires == null)
                {
                    continue;
                }
                if (state.Node.Requires.Contains(run))
                {
                    opens.Add(CampaignOps.DisplayNameOf(state.Node.Run));
                }
            }
            if (opens.Count > 0)
            {
                _detailBody.Add(MakeLabel(cleared ? "Opened" : "Clearing it opens", "cd-inv-col__title"));
                foreach (var name in opens)
                {
                    _detailBody.Add(MakeLabel("→  " + name, "cm-floor", "cm-floor--ahead"));
                }
            }
        }

        private static Label MakeLabel(string text, string className, string extraClass = null)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            if (extraClass != null)
            {
                label.AddToClassList(extraClass);
            }
            return label;
        }

        // --- Actions -----------------------------------------------------------------------

        private void OnNodeClicked(string key)
        {
            _selectedKey = key;
            SetFeedback(string.Empty);
            if (_view != null)
            {
                _view.SetSelected(key);
            }
            RefreshDetail();
        }

        private void OnStart()
        {
            var selected = FindState(_selectedKey);
            if (selected?.Node?.Run == null)
            {
                return;
            }

            if (!selected.CanStart && !selected.CanContinue)
            {
                // The only way to reach this is a stale click, but the map must never be the thing
                // that discards a run in progress.
                SetFeedback(selected.Status == CampaignNodeStatus.Locked
                    ? "That way is still closed."
                    : "Finish the run you are on first.");
                return;
            }

            OnRunChosen?.Invoke(selected.Node.Run);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isShown)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    Hide();
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    MoveSelection(new Vector2(0f, -1f));
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(new Vector2(0f, 1f));
                    evt.StopPropagation();
                    break;
                case KeyCode.LeftArrow:
                    MoveSelection(new Vector2(-1f, 0f));
                    evt.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                    MoveSelection(new Vector2(1f, 0f));
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    OnStart();
                    evt.StopPropagation();
                    break;
            }
        }

        /// <summary>
        /// Walks the map with the arrow keys. The graph is laid out spatially, so the arrows follow the
        /// same geometry the player is looking at rather than some authoring order - pressing Right at
        /// a fork goes to the branch drawn on the right. The view pans to keep up, since a keyboard
        /// player cannot drag the graph back into sight.
        /// </summary>
        private void MoveSelection(Vector2 direction)
        {
            if (_view == null)
            {
                return;
            }

            var key = _view.NodeInDirection(_selectedKey, direction);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            OnNodeClicked(key);
            _view.EnsureNodeVisible(key);
        }

        // --- Helpers -----------------------------------------------------------------------

        private List<CampaignNodeState> BuildStates()
        {
            var completed = MetaProgressManager.Instance.GetCompletedRunKeys();

            // A run can also be gated on *owning a hero* (NEXT_STEPS.md section 5b), and ownership
            // lives in Party.json rather than in meta-progress - so the roster has to be read here
            // and handed to the rules, which stay pure.
            var owned = _roster != null ? HeroRoster.GetOwnedKeys(_roster) : null;
            return CampaignOps.GetStates(_campaign, completed, _activeRunKey, owned);
        }

        private CampaignNodeState FindState(string runKey)
        {
            if (string.IsNullOrEmpty(runKey))
            {
                return null;
            }
            foreach (var state in _states)
            {
                if (state?.Node?.Run != null && CampaignOps.RunKeyOf(state.Node.Run) == runKey)
                {
                    return state;
                }
            }
            return null;
        }

        /// <summary>
        /// What to show when the screen opens: the run underway, else the furthest thing the player can
        /// actually do, so the map lands on their next step rather than on the tutorial they finished.
        /// </summary>
        private string DefaultSelection()
        {
            CampaignNodeState best = null;
            foreach (var state in _states)
            {
                if (state?.Node?.Run == null || !state.IsVisible)
                {
                    continue;
                }
                if (state.CanContinue)
                {
                    return CampaignOps.RunKeyOf(state.Node.Run);
                }
                if (state.CanStart && best == null)
                {
                    best = state;
                }
            }
            if (best != null)
            {
                return CampaignOps.RunKeyOf(best.Node.Run);
            }
            foreach (var state in _states)
            {
                if (state?.Node?.Run != null && state.IsVisible)
                {
                    return CampaignOps.RunKeyOf(state.Node.Run);
                }
            }
            return null;
        }

        private void SetFeedback(string message)
        {
            SetText(_feedback, message);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
