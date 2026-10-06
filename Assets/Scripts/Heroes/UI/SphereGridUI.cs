using System;
using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Progression;
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
        private readonly VisualElement _detailBody;
        private readonly Label _reasonLabel;
        private readonly Button _activateButton;
        private readonly Label _feedbackLabel;
        private readonly Button _closeButton;
        private readonly SphereGridView _view;

        private readonly List<HeroSO> _heroes = new List<HeroSO>();
        private HeroSO _selectedHero;
        private string _selectedNodeKey;
        private bool _isShown;

        // The tutorial's guide: while set, this is the only hero and the only node the screen will
        // act on, and it cannot be backed out of. See SetGuide.
        private HeroSO _guideHero;
        private string _guideNodeKey;

        /// <summary>The class the tutorial puts on whatever it is pointing at (CardDungeon.uss).</summary>
        public const string TutorialTargetClass = "cd-tutorial-target";

        private const float FrontierMinZoom = 0.7f;
        private const float FrontierMaxZoom = 1.1f;

        // How many steps past the frontier the opening frame reaches, so a fresh hero sees the fork.
        private const int FrameLookahead = 3;

        public event Action OnClosed;

        /// <summary>Raised after a node is bought, with its key.</summary>
        public event Action<string> NodeActivated;

        /// <summary>Raised whenever the selected node or hero changes.</summary>
        public event Action SelectionChanged;

        /// <summary>Whether the tutorial is walking the player to one node.</summary>
        public bool IsGuiding => _guideHero != null && !string.IsNullOrEmpty(_guideNodeKey);

        /// <summary>Whether that node is the one selected — the cue for "now press Activate".</summary>
        public bool IsGuidedNodeSelected => IsGuiding && _selectedNodeKey == _guideNodeKey;

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
            _detailBody = root.Q<VisualElement>("grid-detail-body");
            _reasonLabel = root.Q<Label>("grid-reason");
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
            if (IsGuiding && _heroes.Contains(_guideHero))
            {
                _selectedHero = _guideHero;
            }
            else if (_selectedHero == null || !_heroes.Contains(_selectedHero))
            {
                _selectedHero = FirstHeroWithGrid();
            }

            BuildHeroTabs();
            RebuildGraph();
            if (IsGuiding)
            {
                // Close in on the one node that matters rather than fitting the whole grid.
                _view.FrameNode(_guideNodeKey);
            }
            Refresh();
            RefreshGuide();

            _root.focusable = true;
            if (_root.panel != null)
            {
                _root.Focus();
            }
        }

        public void Hide()
        {
            // The guided step cannot be walked away from - Escape lands here too.
            if (IsGuiding)
            {
                return;
            }
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

        // --- the tutorial's guide ------------------------------------------------

        /// <summary>
        /// Walks the player to one node: the screen opens on <paramref name="hero"/>, every other
        /// hero tab and node is inert, Back is disabled, and the node and then the Activate button
        /// carry the tutorial's outline. Set it before <see cref="Show"/>; <see cref="ClearGuide"/>
        /// hands the screen back.
        /// </summary>
        public void SetGuide(HeroSO hero, string nodeKey)
        {
            ClearGuideMarks();
            _guideHero = hero;
            _guideNodeKey = nodeKey;
            if (_isShown)
            {
                RefreshGuide();
            }
        }

        public void ClearGuide()
        {
            ClearGuideMarks();
            _guideHero = null;
            _guideNodeKey = null;
            if (_isShown)
            {
                BuildHeroTabs();
                RefreshGuide();
            }
        }

        private void ClearGuideMarks()
        {
            if (!string.IsNullOrEmpty(_guideNodeKey))
            {
                _view.SetNodeFlag(_guideNodeKey, TutorialTargetClass, false);
            }
            _activateButton?.EnableInClassList(TutorialTargetClass, false);
        }

        private void RefreshGuide()
        {
            _closeButton?.SetEnabled(!IsGuiding);
            if (!IsGuiding)
            {
                return;
            }
            // Only while the node is still to be bought: once it is, the outline has done its job.
            _view.SetNodeFlag(_guideNodeKey, TutorialTargetClass, !IsGuidedNodeSelected);
            _activateButton?.EnableInClassList(TutorialTargetClass, IsGuidedNodeSelected);
        }

        // --- hero switching -----------------------------------------------------

        /// <summary>
        /// The house hero picker (the Storehouse's portrait strip), with each hero's banked XP as a
        /// tab on the card - so the player sees who has points to spend without Q/E-ing through them.
        /// </summary>
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

                var card = new Button { text = string.Empty, focusable = false, tooltip = hero.DisplayName };
                card.AddToClassList("cd-inv-hero");
                card.EnableInClassList("cd-inv-hero--active", hero == _selectedHero);
                if (hero.SphereGrid == null)
                {
                    card.SetEnabled(false);
                    card.tooltip = "No grid authored for this hero yet.";
                }
                else if (IsGuiding && hero != _guideHero)
                {
                    card.SetEnabled(false);
                }

                var sprite = new VisualElement { pickingMode = PickingMode.Ignore };
                sprite.AddToClassList("cd-inv-hero__sprite");
                if (hero.Sprite != null)
                {
                    sprite.style.backgroundImage = new StyleBackground(hero.Sprite);
                }
                card.Add(sprite);

                var name = new Label(hero.DisplayName) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("cd-inv-hero__name");
                card.Add(name);

                var save = HeroRoster.GetHeroSave(hero);
                int xp = save != null ? save.CurrentXp : 0;
                if (xp > 0)
                {
                    var badge = new Label($"{xp} XP") { pickingMode = PickingMode.Ignore };
                    badge.AddToClassList("sg-hero-xp");
                    card.Add(badge);
                }

                var captured = hero;
                card.clicked += () => SelectHero(captured);
                _heroTabs.Add(card);
            }
        }

        private void SelectHero(HeroSO hero)
        {
            if (hero == null || hero == _selectedHero || (IsGuiding && hero != _guideHero))
            {
                return;
            }

            _selectedHero = hero;
            _selectedNodeKey = null;
            SetFeedback(string.Empty);
            BuildHeroTabs();
            RebuildGraph();
            Refresh();
            SelectionChanged?.Invoke();
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

            // Frame the frontier - what is owned and what can be reached - rather than the whole
            // grid, which fitted to the viewport made every node a speck. The floor keeps a node
            // about 30px across; the rest of a large grid is a drag away.
            var save = HeroRoster.GetHeroSave(_selectedHero) ?? new HeroSaveData();
            var frontier = new List<string>();
            foreach (var pair in SphereGridPresenter.ClassifyAll(grid, save.ActivatedNodes ?? new List<string>(),
                         save.CurrentXp, HeroRoster.CanPayMaterials))
            {
                if (pair.Value != NodeUiState.Locked)
                {
                    frontier.Add(pair.Key);
                }
            }

            // And a few steps past it: a fresh hero's frontier is the trunk alone, which framed the
            // view so tight that both branches were off screen (revisit playtest finding 11). Three
            // rings out reaches the first fork on every grid, showing where each path goes without
            // shrinking the whole grid to specks.
            // Symmetric adjacency: an edge is authored on one of its two nodes only, so walking a
            // node's own Neighbors list misses half the graph.
            var adjacency = SphereGridOps.BuildAdjacency(grid);
            var framed = new List<string>(frontier);
            var ring = new List<string>(frontier);
            for (int step = 0; step < FrameLookahead; step++)
            {
                var nextRing = new List<string>();
                foreach (var key in ring)
                {
                    if (!adjacency.TryGetValue(key, out var neighbours))
                    {
                        continue;
                    }
                    foreach (var next in neighbours)
                    {
                        if (!string.IsNullOrEmpty(next) && !framed.Contains(next))
                        {
                            framed.Add(next);
                            nextRing.Add(next);
                        }
                    }
                }
                ring = nextRing;
            }
            _view.FrameNodes(framed, FrontierMinZoom, FrontierMaxZoom);
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
                    ? $"{_selectedHero.DisplayName}: {save.CurrentXp} XP to spend"
                    : "No heroes owned.";
            }
            BuildHeroTabs();

            if (grid != null)
            {
                foreach (var pair in SphereGridPresenter.ClassifyAll(
                    grid, activated, save.CurrentXp, HeroRoster.CanPayMaterials))
                {
                    _view.SetNodeState(pair.Key, SphereGridPresenter.StateClass(pair.Value));
                }
            }

            _view.SetSelected(_selectedNodeKey);
            RefreshDetail(grid, save);
        }

        /// <summary>
        /// The chosen node: the thing it grants as the title, its kind as a chip, what it changes for
        /// this hero, the price, and - when Activate is dimmed - why. The title, kind and payload
        /// used to say the same thing three times ("+10 HP / Stat / +10 HP").
        /// </summary>
        private void RefreshDetail(SphereGridSO grid, HeroSaveData save)
        {
            _detailBody?.Clear();
            var node = SphereGridOps.FindNode(grid, _selectedNodeKey);
            if (node == null)
            {
                SetDetail("Select a node.", "", "Click a node, or move to one with the arrow keys.", "");
                SetShown(_detailKind, false);
                SetReason(string.Empty);
                if (_activateButton != null)
                {
                    // Reset the label too, or switching hero after an activation left the button
                    // reading "Activated" over an empty selection (revisit playtest finding 9).
                    _activateButton.text = "Activate";
                    _activateButton.SetEnabled(false);
                }
                return;
            }

            var activated = save.ActivatedNodes ?? new List<string>();
            bool isActive = SphereGridOps.ActiveNodes(grid, activated).Contains(node.Key);

            SetDetail(Title(node), SphereGridPresenter.KindLabel(node), Payload(node),
                SphereGridPresenter.DescribeCost(node, isActive));
            SetShown(_detailKind, true);
            AddChanges(node, activated, isActive);

            bool canActivate = SphereGridOps.CanActivate(grid, activated, save.CurrentXp, node.Key)
                               && HeroRoster.CanPayMaterials(node);
            if (_activateButton != null)
            {
                _activateButton.text = isActive ? "Activated" : "Activate";
                _activateButton.SetEnabled(canActivate);
            }

            if (isActive || canActivate)
            {
                SetReason(string.Empty);
            }
            else if (!SphereGridOps.IsReachable(grid, activated, node.Key))
            {
                SetReason("Activate a node next to it first.");
            }
            else if (save.CurrentXp < node.XpCost)
            {
                SetReason($"Need {node.XpCost} XP — {save.CurrentXp} banked.");
            }
            else
            {
                SetReason($"Needs {SphereGridPresenter.DescribeMaterialCost(node)}.");
            }
        }

        /// <summary>The thing itself: an authored name, else the ability, summon or stat it grants.</summary>
        private static string Title(SphereGridNode node)
        {
            if (!string.IsNullOrEmpty(node.DisplayName))
            {
                return node.DisplayName;
            }
            switch (node.Kind)
            {
                case SphereNodeKind.MagicKnown:
                    return MagicName(node.GrantedMagicKey);
                case SphereNodeKind.Summon:
                    return SummonName(node.GrantedSummonKey);
                case SphereNodeKind.Ultra:
                    return UltraName(node.GrantedUltraKey);
                case SphereNodeKind.MagicSlot:
                    return "+1 ability slot";
                default:
                    return SphereGridPresenter.DescribePayload(node);
            }
        }

        /// <summary>One line under the title that says what the node does, not what it is.</summary>
        private static string Payload(SphereGridNode node)
        {
            switch (node.Kind)
            {
                case SphereNodeKind.MagicKnown:
                {
                    // Learned is not carried: it still has to win a slot in the Storehouse, and the
                    // charge count is the whole run's allowance of it.
                    var magic = MagicCatalog.HasInstance ? MagicCatalog.Instance.GetMagic(node.GrantedMagicKey) : null;
                    string line = $"Learns {MagicName(node.GrantedMagicKey)}, {Mathf.Max(1, node.GrantedCharges)} charges a run. "
                                  + "Carry it by giving it a slot in the Storehouse.";
                    return magic != null && !string.IsNullOrEmpty(magic.Description)
                        ? magic.Description + "\n\n" + line
                        : line;
                }
                case SphereNodeKind.MagicSlot:
                    return "Carry one more of the abilities this hero knows into every run.";
                case SphereNodeKind.Summon:
                    return $"Learns the summon {SummonName(node.GrantedSummonKey)}. Always carried and cast with Summon; it takes no ability slot.";
                case SphereNodeKind.Ultra:
                {
                    var ultra = string.IsNullOrEmpty(node.GrantedUltraKey) ? null : UltraCatalogSO.Resolve(node.GrantedUltraKey);
                    string what = ultra != null ? UltraOps.Describe(ultra) + ". " : "";
                    return $"Learns the Ultra {UltraName(node.GrantedUltraKey)}. {what}"
                           + "Use it from the Ultra command once the gauge is full - losing health in a fight fills it.";
                }
                case SphereNodeKind.Stat:
                    return "Raises this hero's stats for good.";
                case SphereNodeKind.Resistance:
                    return SphereGridPresenter.DescribePayload(node);
                default:
                {
                    // Summon upgrades name the summon by its key; show its name instead.
                    string text = SphereGridPresenter.DescribePayload(node);
                    return string.IsNullOrEmpty(node.GrantedSummonKey)
                        ? text
                        : text.Replace(node.GrantedSummonKey, SummonName(node.GrantedSummonKey));
                }
            }
        }

        /// <summary>For a stat node, each stat it raises as "HP 26 → 36" on this hero.</summary>
        private void AddChanges(SphereGridNode node, List<string> activated, bool isActive)
        {
            if (_detailBody == null || node.Kind != SphereNodeKind.Stat || node.Gains == null || _selectedHero == null)
            {
                return;
            }

            var without = new List<string>(activated);
            without.Remove(node.Key);
            var with = new List<string>(without) { node.Key };
            // With gear, so the numbers match the Storehouse and the fight. Base-only previews said
            // "Health 38 -> 48" while the Storehouse said 56 (revisit playtest finding 11).
            var gear = Items.InventoryManager.HasInstance
                ? Items.InventoryManager.Instance.GetEquippedItems(_selectedHero.SaveKey)
                : new List<Items.ItemSO>();
            var before = HeroStatCalculator.WithGear(HeroStatCalculator.BaseStatsForNodes(_selectedHero, without), gear);
            var after = HeroStatCalculator.WithGear(HeroStatCalculator.BaseStatsForNodes(_selectedHero, with), gear);

            foreach (var entry in node.Gains.NonZero())
            {
                var cell = Items.UI.InventoryHubUI.StatCell(StatCatalog.DisplayName(entry.Type),
                    $"{before[entry.Type]} → {after[entry.Type]}", 1, StatCatalog.DisplayName(entry.Type));
                cell.style.width = new Length(100f, LengthUnit.Percent);
                _detailBody.Add(cell);
            }
        }

        private static string MagicName(string key)
        {
            var magic = MagicCatalog.HasInstance ? MagicCatalog.Instance.GetMagic(key) : null;
            return magic != null && !string.IsNullOrEmpty(magic.DisplayName) ? magic.DisplayName : key;
        }

        private static string UltraName(string key)
        {
            var ultra = string.IsNullOrEmpty(key) ? null : UltraCatalogSO.Resolve(key);
            return ultra != null ? ultra.Label : key;
        }

        private static string SummonName(string key)
        {
            var summon = string.IsNullOrEmpty(key) ? null : SummonCatalogSO.Resolve(key);
            return summon != null ? summon.Label : key;
        }

        private void SetReason(string text)
        {
            if (_reasonLabel != null)
            {
                _reasonLabel.text = text;
            }
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
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
            if (IsGuiding && key != _guideNodeKey)
            {
                return;
            }
            _selectedNodeKey = key;
            SetFeedback(string.Empty);
            Refresh();
            RefreshGuide();
            SelectionChanged?.Invoke();
        }

        private void OnActivate()
        {
            var node = SphereGridOps.FindNode(
                _selectedHero != null ? _selectedHero.SphereGrid : null, _selectedNodeKey);
            if (node == null || (IsGuiding && node.Key != _guideNodeKey))
            {
                return;
            }

            if (HeroRoster.TryActivateNode(_selectedHero, _selectedNodeKey))
            {
                // Learning a spell is what "discovered" means now, so the Forge can be told here.
                // Drawing it from an enemy used to be the trigger; with Draw gone this is the only
                // moment a magic enters the player's possession, and gating Forge upgrades behind a
                // spell nobody can obtain would leave the collection permanently half-locked.
                if (node.Kind == SphereNodeKind.MagicKnown && !string.IsNullOrEmpty(node.GrantedMagicKey))
                {
                    MetaProgressManager.Instance.MarkMagicDiscovered(node.GrantedMagicKey);
                }

                SetFeedback($"{SphereGridPresenter.NodeName(node)} activated.");
                Refresh();
                NodeActivated?.Invoke(node.Key);
                return;
            }
            else if (!HeroRoster.CanPayMaterials(node))
            {
                // The one failure the player cannot fix at the hub, so it is worth naming: XP is
                // banked from kills, materials only drop in a run.
                SetFeedback("Not enough materials — needs "
                            + SphereGridPresenter.DescribeMaterialCost(node) + ".");
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

            // While guided there is one node worth moving to, so any arrow goes there.
            var key = IsGuiding ? _guideNodeKey : _view.NodeInDirection(_selectedNodeKey, direction);
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
