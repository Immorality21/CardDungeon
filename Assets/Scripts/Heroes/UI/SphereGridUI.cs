using System;
using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Heroes.UI
{
    /// <summary>
    /// The hub's sphere-grid screen (UI Toolkit view-controller): pick an owned hero, pan/zoom
    /// their grid, click a node, spend banked XP to activate it. The one place XP is ever spent —
    /// the dungeon only banks it — which is what keeps room-event spawn thresholds stable mid-run.
    ///
    /// <para>Rendering is <see cref="SphereGridView"/> (shared with the authoring window) and every
    /// decision — node states, names, payload text — comes from <see cref="SphereGridPresenter"/> /
    /// <see cref="SphereGridOps"/>, so this class is only wiring: query controls, refresh, spend.
    /// Operates on a VisualElement subtree owned by the menu's UIDocument, same as the merchant and
    /// the party screen.</para>
    /// </summary>
    public class SphereGridUI
    {
        private readonly VisualElement _root;
        private readonly PartyRosterSO _catalog;
        private readonly VisualElement _heroTabs;
        private readonly Label _xpLabel;
        private readonly Label _detailName;
        private readonly Label _detailKind;
        private readonly Label _detailPayload;
        private readonly Label _detailCost;
        private readonly Button _activateButton;
        private readonly Label _feedbackLabel;
        private readonly Button _closeButton;
        private readonly SphereGridView _view;

        private readonly List<HeroSO> _heroes = new List<HeroSO>();
        private HeroSO _selectedHero;
        private string _selectedNodeKey;
        private bool _isShown;

        public event Action OnClosed;

        public SphereGridUI(VisualElement root, PartyRosterSO catalog)
        {
            _root = root;
            _catalog = catalog;

            _heroTabs = root.Q<VisualElement>("grid-heroes");
            _xpLabel = root.Q<Label>("grid-xp");
            _detailName = root.Q<Label>("grid-detail-name");
            _detailKind = root.Q<Label>("grid-detail-kind");
            _detailPayload = root.Q<Label>("grid-detail-payload");
            _detailCost = root.Q<Label>("grid-detail-cost");
            _activateButton = root.Q<Button>("grid-activate");
            _feedbackLabel = root.Q<Label>("grid-feedback");
            _closeButton = root.Q<Button>("grid-close");

            var graphHost = root.Q<VisualElement>("grid-graph");
            _view = new SphereGridView();
            if (graphHost != null)
            {
                graphHost.Add(_view);
            }
            _view.NodeClicked += OnNodeClicked;

            if (_activateButton != null)
            {
                _activateButton.focusable = false;
                _activateButton.clicked += OnActivate;
            }
            if (_closeButton != null)
            {
                _closeButton.focusable = false;
                _closeButton.clicked += Hide;
            }

            // The view root owns keyboard input while shown (the InventoryHubUI pattern).
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);
            _selectedNodeKey = null;

            _heroes.Clear();
            _heroes.AddRange(HeroRoster.GetOwnedHeroes(_catalog));
            if (_selectedHero == null || !_heroes.Contains(_selectedHero))
            {
                _selectedHero = FirstHeroWithGrid();
            }

            BuildHeroTabs();
            RebuildGraph();
            Refresh();

            _root.focusable = true;
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

        private HeroSO FirstHeroWithGrid()
        {
            foreach (var hero in _heroes)
            {
                if (hero != null && hero.SphereGrid != null)
                {
                    return hero;
                }
            }
            return _heroes.Count > 0 ? _heroes[0] : null;
        }

        // --- hero switching -----------------------------------------------------

        private void BuildHeroTabs()
        {
            if (_heroTabs == null)
            {
                return;
            }

            _heroTabs.Clear();
            foreach (var hero in _heroes)
            {
                if (hero == null)
                {
                    continue;
                }

                var tab = new Button { text = hero.DisplayName, focusable = false };
                tab.AddToClassList("cd-tab");
                if (hero == _selectedHero)
                {
                    tab.AddToClassList("cd-tab--active");
                }
                if (hero.SphereGrid == null)
                {
                    tab.SetEnabled(false);
                    tab.tooltip = "No grid authored for this hero yet.";
                }

                var captured = hero;
                tab.clicked += () => SelectHero(captured);
                _heroTabs.Add(tab);
            }
        }

        private void SelectHero(HeroSO hero)
        {
            if (hero == null || hero == _selectedHero)
            {
                return;
            }

            _selectedHero = hero;
            _selectedNodeKey = null;
            SetFeedback(string.Empty);
            BuildHeroTabs();
            RebuildGraph();
            Refresh();
        }

        private void CycleHero(int direction)
        {
            if (_heroes.Count == 0 || _selectedHero == null)
            {
                return;
            }

            int index = _heroes.IndexOf(_selectedHero);
            for (int step = 1; step <= _heroes.Count; step++)
            {
                int next = (index + direction * step + _heroes.Count * step) % _heroes.Count;
                if (_heroes[next] != null && _heroes[next].SphereGrid != null)
                {
                    SelectHero(_heroes[next]);
                    return;
                }
            }
        }

        // --- graph -----------------------------------------------------------------

        /// <summary>Pushes the selected hero's grid shape into the view and frames it.</summary>
        private void RebuildGraph()
        {
            var grid = _selectedHero != null ? _selectedHero.SphereGrid : null;
            if (grid == null || grid.Nodes == null)
            {
                _view.SetGraph(null, null);
                return;
            }

            var nodes = new List<SphereGridView.NodeInfo>();
            var edges = new List<(string A, string B)>();
            SphereGridPresenter.BuildViewModel(grid, nodes, edges);

            _view.SetGraph(nodes, edges);
            _view.FrameAll();
        }

        // --- refresh ------------------------------------------------------------------

        private void Refresh()
        {
            var grid = _selectedHero != null ? _selectedHero.SphereGrid : null;
            var save = _selectedHero != null ? HeroRoster.GetHeroSave(_selectedHero) : new HeroSaveData();
            var activated = save.ActivatedNodes ?? new List<string>();

            if (_xpLabel != null)
            {
                _xpLabel.text = _selectedHero != null
                    ? $"{_selectedHero.DisplayName} — {save.CurrentXp} XP banked"
                    : "No heroes owned.";
            }

            if (grid != null)
            {
                foreach (var pair in SphereGridPresenter.ClassifyAll(grid, activated, save.CurrentXp))
                {
                    _view.SetNodeState(pair.Key, SphereGridPresenter.StateClass(pair.Value));
                }
            }

            _view.SetSelected(_selectedNodeKey);
            RefreshDetail(grid, save);
        }

        private void RefreshDetail(SphereGridSO grid, HeroSaveData save)
        {
            var node = SphereGridOps.FindNode(grid, _selectedNodeKey);
            if (node == null)
            {
                SetDetail("Select a node.", "", "", "");
                _activateButton?.SetEnabled(false);
                return;
            }

            var activated = save.ActivatedNodes ?? new List<string>();
            bool isActivated = activated.Contains(node.Key);

            SetDetail(
                SphereGridPresenter.NodeName(node),
                SphereGridPresenter.KindLabel(node),
                SphereGridPresenter.DescribePayload(node),
                isActivated ? "Activated" : $"Costs {node.XpCost} XP");

            if (_activateButton != null)
            {
                _activateButton.text = isActivated ? "Activated" : "Activate";
                _activateButton.SetEnabled(
                    SphereGridOps.CanActivate(grid, activated, save.CurrentXp, node.Key));
            }
        }

        private void SetDetail(string name, string kind, string payload, string cost)
        {
            if (_detailName != null)
            {
                _detailName.text = name;
            }
            if (_detailKind != null)
            {
                _detailKind.text = kind;
            }
            if (_detailPayload != null)
            {
                _detailPayload.text = payload;
            }
            if (_detailCost != null)
            {
                _detailCost.text = cost;
            }
        }

        // --- actions --------------------------------------------------------------------

        private void OnNodeClicked(string key)
        {
            _selectedNodeKey = key;
            SetFeedback(string.Empty);
            Refresh();
        }

        private void OnActivate()
        {
            var node = SphereGridOps.FindNode(
                _selectedHero != null ? _selectedHero.SphereGrid : null, _selectedNodeKey);
            if (node == null)
            {
                return;
            }

            if (HeroRoster.TryActivateNode(_selectedHero, _selectedNodeKey))
            {
                SetFeedback($"{SphereGridPresenter.NodeName(node)} activated.");
            }
            else
            {
                SetFeedback("Cannot activate that node.");
            }
            Refresh();
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
                case KeyCode.Q:
                    CycleHero(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.E:
                    CycleHero(1);
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
                    OnActivate();
                    evt.StopPropagation();
                    break;
            }
        }

        /// <summary>
        /// Walks the grid with the arrow keys, following the shape the player can see rather than any
        /// authoring order. The view pans to keep the cursor on screen - without a mouse there is no
        /// other way to bring a far node back into view.
        /// </summary>
        private void MoveSelection(Vector2 direction)
        {
            if (_view == null)
            {
                return;
            }

            var key = _view.NodeInDirection(_selectedNodeKey, direction);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            OnNodeClicked(key);
            _view.EnsureNodeVisible(key);
        }

        private void SetFeedback(string message)
        {
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = message;
            }
        }
    }
}
