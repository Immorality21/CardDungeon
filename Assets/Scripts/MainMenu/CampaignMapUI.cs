using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
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
        private readonly SphereGridView _view;

        private readonly Label _titleLabel;
        private readonly Label _detailName;
        private readonly Label _detailStatus;
        private readonly Label _detailBlurb;
        private readonly Label _detailRequires;
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

        public CampaignMapUI(VisualElement root, CampaignSO campaign)
        {
            _root = root;
            _campaign = campaign;

            _titleLabel = root.Q<Label>("campaign-title");
            _detailName = root.Q<Label>("campaign-detail-name");
            _detailStatus = root.Q<Label>("campaign-detail-status");
            _detailBlurb = root.Q<Label>("campaign-detail-blurb");
            _detailRequires = root.Q<Label>("campaign-detail-requires");
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
        public void Show(string activeRunKey)
        {
            _activeRunKey = activeRunKey ?? string.Empty;
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            _root.focusable = true;
            SetFeedback(string.Empty);

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
                return;
            }

            var run = selected.Node.Run;
            SetText(_detailName, CampaignOps.DisplayNameOf(run));
            SetText(_detailStatus, $"{CampaignPresenter.StatusLabel(selected)} · {run.Levels.Count} levels");
            SetText(_detailBlurb, run.Blurb);

            if (selected.Status == CampaignNodeStatus.Locked && selected.MissingRequirements.Count > 0)
            {
                SetText(_detailRequires, "Requires: " + string.Join(", ", selected.MissingRequirements));
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
            if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Backspace)
            {
                Hide();
                evt.StopPropagation();
            }
        }

        // --- Helpers -----------------------------------------------------------------------

        private List<CampaignNodeState> BuildStates()
        {
            var completed = MetaProgressManager.Instance.GetCompletedRunKeys();
            return CampaignOps.GetStates(_campaign, completed, _activeRunKey);
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
