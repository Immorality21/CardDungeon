using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Hub.UI;
using Assets.Scripts.IO;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The hub scene (UI Toolkit). Drives one UIDocument holding the town plus every service that
    /// hangs off it — run progress, run complete, merchant, campfire/party, forge, bestiary,
    /// inventory, sphere grid and the campaign map. Each panel is a plain view-controller operating
    /// on its own subtree; this class is wiring, view toggling and the run hand-off.
    ///
    /// <para><b>The town replaced a column of ten buttons.</b> Home used to be a
    /// <c>cd-window--tall</c> list with ~85 units of headroom left — one more button — which is what
    /// <c>docs/plans/HUB.md</c> §7 was written to fix. Services are lots on a painted town now, and
    /// the hub can grow without a layout budget.</para>
    ///
    /// <para><b>This scene is where the game lives.</b> MenuScene is a title screen that opens a save
    /// file; from the moment it does, the loop is hub → dungeon → hub. Both ways out of a dungeon —
    /// <c>DungeonManager.OnDungeonCleared</c> and the death screen — load HubScene, never MenuScene.</para>
    ///
    /// <para>Party select is reachable from two places on purpose: from the campfire like any other
    /// service, and from the run-progress screen next to "Enter Dungeon", which is the moment the
    /// choice actually matters. Closing it returns to whichever of the two opened it.</para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HubManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Fallback run for a scene with no Campaign asset in Resources. With a campaign the " +
                 "run is resolved from the graph by the key in Run.json, so this is not the run that plays.")]
        private RunDefinitionSO _runDefinition;
        [SerializeField] private PartyRosterSO _partyRoster;
        [SerializeField] private UIDocument _document;

        private VisualElement _hubView;
        private VisualElement _campaignView;
        private VisualElement _progressView;
        private VisualElement _completeView;
        private VisualElement _merchantView;
        private VisualElement _partyView;
        private VisualElement _gridView;
        private VisualElement _forgeView;
        private VisualElement _bestiaryView;
        private VisualElement _inventoryView;

        private Button _roadButton;
        private Button _menuButton;
        private Button _progressPartyButton;
        private Button _backButton;
        private Button _enterButton;
        private Button _returnButton;

        private Label _goldLabel;
        private Label _essenceLabel;
        private Label _hubFeedback;
        private Label _levelIndicator;
        private Label _levelName;
        private Label _progressParty;

        private VisualElement _root;
        private KeyboardNavigator _nav;

        private HubSO _hub;
        private HubView _town;

        private CampaignSO _campaign;
        private CampaignMapUI _campaignMap;
        private MerchantUI _merchant;
        private PartySelectUI _partySelect;
        private SphereGridUI _sphereGrid;
        private MagicForgeUI _forge;
        private BestiaryUI _bestiary;
        private InventoryHubUI _inventory;

        private FileHandler _fileHandler;
        private RunSaveData _runSaveData;

        /// <summary>
        /// Set by <c>DungeonManager</c> on the way out of a finished run, read once here to decide
        /// whether the victory screen is owed. A static because it has to survive the scene load and
        /// there is nothing else crossing that boundary but <c>DungeonManager</c>'s own statics.
        /// </summary>
        private static bool _justCompletedRun;

        // Which view the party screen was opened from, so Back goes where the player came from.
        private bool _partyOpenedFromProgress;

        public static void MarkRunCompleted()
        {
            _justCompletedRun = true;
        }

        private void Start()
        {
            _fileHandler = new FileHandler();
            _runSaveData = _fileHandler.Load<RunSaveData>();

            // Both Resources-loaded like the item catalog, so the hub resolves the run graph and the
            // town without scene wiring - and without AssetDatabase, which does not exist in a build.
            _campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            _hub = UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);

            // A spell a hero starts with is never "bought", so nothing else would ever record it as
            // discovered and the Forge would refuse to upgrade the one spell everyone owns.
            HeroRoster.MarkDefaultUnlocksDiscovered(_partyRoster);

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }
            var root = _document.rootVisualElement;
            _root = root;

            _hubView = root.Q<VisualElement>("hub-view");
            _campaignView = root.Q<VisualElement>("campaign-view");
            _progressView = root.Q<VisualElement>("progress-view");
            _completeView = root.Q<VisualElement>("complete-view");
            _merchantView = root.Q<VisualElement>("merchant-view");
            _partyView = root.Q<VisualElement>("party-view");
            _gridView = root.Q<VisualElement>("grid-view");
            _forgeView = root.Q<VisualElement>("forge-view");
            _bestiaryView = root.Q<VisualElement>("bestiary-view");
            _inventoryView = root.Q<VisualElement>("inventory-view");

            _roadButton = root.Q<Button>("road-btn");
            _menuButton = root.Q<Button>("hub-menu-btn");
            _progressPartyButton = root.Q<Button>("progress-party-btn");
            _backButton = root.Q<Button>("back-btn");
            _enterButton = root.Q<Button>("enter-btn");
            _returnButton = root.Q<Button>("return-btn");

            _goldLabel = root.Q<Label>("hub-gold");
            _essenceLabel = root.Q<Label>("hub-essence");
            _hubFeedback = root.Q<Label>("hub-feedback");
            _levelIndicator = root.Q<Label>("level-indicator");
            _levelName = root.Q<Label>("level-name");
            _progressParty = root.Q<Label>("progress-party");

            _roadButton.clicked += OnTakeTheRoad;
            _menuButton.clicked += OnLeaveToMainMenu;
            _enterButton.clicked += OnEnterDungeon;
            _backButton.clicked += OnBack;
            _returnButton.clicked += OnRunCompleteReturn;
            _progressPartyButton.clicked += OnChangePartyFromProgress;

            BuildTown();

            _campaignMap = _campaignView != null && _campaign != null
                ? new CampaignMapUI(_campaignView, _campaign)
                : null;
            _merchant = new MerchantUI(_merchantView);
            _partySelect = new PartySelectUI(_partyView, _partyRoster);
            _sphereGrid = new SphereGridUI(_gridView, _partyRoster);
            _forge = new MagicForgeUI(_forgeView);
            _bestiary = new BestiaryUI(_bestiaryView);
            _inventory = new InventoryHubUI(_inventoryView, _partyRoster);

            if (_campaignMap != null)
            {
                _campaignMap.OnClosed += ShowTown;
                _campaignMap.OnRunChosen += OnRunChosen;
            }
            _merchant.OnClosed += ShowTown;
            _partySelect.OnClosed += OnPartyClosed;
            _sphereGrid.OnClosed += ShowTown;
            _forge.OnClosed += ShowTown;
            _bestiary.OnClosed += ShowTown;
            _inventory.OnClosed += ShowTown;

            SetUpKeyboardNavigation();

            // The hub's own bed. Requesting the track already playing is a no-op, so arriving from
            // MenuScene does not restart it - the walk from the title screen into town is seamless.
            MusicPlayer.Play(MusicTrack.Hub);

            // Run complete only when we arrived from clearing the final level.
            if (DungeonManager.ActiveRun == null && string.IsNullOrEmpty(_runSaveData.RunKey) && _justCompletedRun)
            {
                ShowRunCompletePanel();
            }
            else
            {
                ShowTown();
            }
        }

        // ============================================================
        //  THE TOWN
        // ============================================================

        /// <summary>
        /// Builds the town once. The lots come from <see cref="HubPresenter.BuildViewModel"/> already
        /// in paint order — UI Toolkit has no z-index, so the order they are added *is* the order they
        /// are drawn, and nothing may re-sort them afterwards.
        /// </summary>
        private void BuildTown()
        {
            var host = _root.Q<VisualElement>("hub-town");
            if (host == null)
            {
                Debug.LogError("Hub.uxml has no hub-town host; the town cannot be drawn.");
                return;
            }

            _town = new HubView();
            _town.LotClicked += OnLotClicked;
            // Inserted at 0 so the road, authored in the UXML, keeps painting over the town.
            host.Insert(0, _town);

            if (_hub == null)
            {
                Debug.LogError($"No hub at Assets/Resources/{HubSO.ResourcePath}.asset; the town is empty.");
                return;
            }

            var lots = new List<HubView.LotInfo>();
            HubPresenter.BuildViewModel(_hub, SavedBuildings(), lots);
            _town.SetTown(_hub.ReferenceSize, _hub.Backdrop, lots);
            RefreshTown();
        }

        private static List<BuildingProgress> SavedBuildings()
        {
            return MetaProgressManager.HasInstance
                ? MetaProgressManager.Instance.GetBuildings()
                : new List<BuildingProgress>();
        }

        /// <summary>Repaints each lot's state. Cheap, and run on every return to the town so a
        /// building placed on another screen shows up without a scene reload.</summary>
        private void RefreshTown()
        {
            if (_town == null || _hub == null)
            {
                return;
            }

            var saved = SavedBuildings();
            foreach (var building in BuildingOps.InDrawOrder(_hub))
            {
                var state = BuildingOps.StateOf(building, saved);
                _town.SetLotState(building.SaveKey, HubPresenter.StateClass(state));
                _town.SetLotNote(building.SaveKey, HubPresenter.DescribeState(building, saved));
            }
        }

        /// <summary>
        /// A lot was clicked. An unbuilt lot is scenery — it says what it is waiting for rather than
        /// opening a screen the player has not earned.
        /// </summary>
        private void OnLotClicked(string buildingKey)
        {
            var building = _hub != null ? _hub.Find(buildingKey) : null;
            if (building == null)
            {
                return;
            }

            var saved = SavedBuildings();
            if (!HubPresenter.IsOpenable(building, saved))
            {
                SetFeedback($"{building.Label} — {HubPresenter.DescribeState(building, saved)}");
                return;
            }

            SetFeedback(string.Empty);
            OpenService(building.Service);
        }

        private void OpenService(HubService service)
        {
            switch (service)
            {
                case HubService.Party:
                    OnVisitParty();
                    break;
                case HubService.Merchant:
                    OnVisitMerchant();
                    break;
                case HubService.Forge:
                    OnVisitForge();
                    break;
                case HubService.Inventory:
                    OnVisitInventory();
                    break;
                case HubService.Bestiary:
                    OnVisitBestiary();
                    break;
                case HubService.SphereGrid:
                    OnVisitSphereGrid();
                    break;
            }
        }

        // ============================================================
        //  KEYBOARD NAVIGATION
        // ============================================================

        /// <summary>
        /// Arrow keys for the hub, matching how combat and the room bar are driven. One
        /// <see cref="KeyboardNavigator"/> serves every screen this class owns, because it navigates
        /// whatever buttons are visible rather than a list wired per screen - and only one of these
        /// views is ever displayed at a time.
        ///
        /// <para>The town is included rather than excluded: its lots are ordinary Buttons, and the
        /// navigator moves between them spatially on <c>worldBound</c> centres, which carry the town's
        /// letterbox transform. So the arrows follow the town as drawn, and the road and the menu
        /// button join the same cursor for free.</para>
        ///
        /// <para>The screens that build their own cursors - the campaign map, the sphere grid, the
        /// bestiary, the inventory - are excluded by <see cref="NavigatesCurrentView"/>. They are
        /// children of this same root, so without that gate a key they chose not to handle would
        /// bubble up here and be acted on twice.</para>
        /// </summary>
        private void SetUpKeyboardNavigation()
        {
            _nav = new KeyboardNavigator(_root);
            _nav.Cancelled += OnNavigatorCancelled;

            _root.focusable = true;
            _root.RegisterCallback<KeyDownEvent>(OnMenuKeyDown);

            // Swallow UI Toolkit's own focus navigation while our cursor owns the screen, or the first
            // arrow key moves keyboard focus off the root and the second one never reaches us.
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (NavigatesCurrentView()) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (NavigatesCurrentView()) { evt.StopPropagation(); } });
        }

        private void OnMenuKeyDown(KeyDownEvent evt)
        {
            if (!NavigatesCurrentView())
            {
                return;
            }
            if (_nav.HandleKey(evt))
            {
                evt.StopPropagation();
            }
        }

        /// <summary>The views whose buttons the shared cursor drives - see <see cref="SetUpKeyboardNavigation"/>.</summary>
        private bool NavigatesCurrentView()
        {
            return IsShown(_hubView) || IsShown(_progressView) || IsShown(_completeView)
                || IsShown(_merchantView) || IsShown(_partyView) || IsShown(_forgeView);
        }

        /// <summary>
        /// Escape leaves the screen by pressing its own Back button rather than calling the panel's
        /// Hide directly, so backing out with the keyboard runs exactly the same path as clicking it -
        /// the panels raise <c>OnClosed</c> from there and this class depends on that to get home again.
        /// </summary>
        private void OnNavigatorCancelled()
        {
            KeyboardNavigator.Press(CancelButtonForCurrentView());
        }

        private Button CancelButtonForCurrentView()
        {
            if (IsShown(_merchantView))
            {
                return _root.Q<Button>("merchant-close");
            }
            if (IsShown(_partyView))
            {
                return _root.Q<Button>("party-close");
            }
            if (IsShown(_forgeView))
            {
                // The forge stacks an inspect page over its grid; Escape backs out one layer at a time.
                var inspect = _root.Q<VisualElement>("forge-inspect");
                return IsShown(inspect) ? _root.Q<Button>("inspect-back") : _root.Q<Button>("forge-close");
            }
            if (IsShown(_progressView))
            {
                return _backButton;
            }
            if (IsShown(_completeView))
            {
                return _returnButton;
            }
            // The town has nowhere to back out to. Leaving for the title screen is a deliberate
            // click, not something Escape should do by accident mid-run.
            return null;
        }

        /// <summary>
        /// Puts keyboard focus back on the root and drops the cursor. Called from every panel switch:
        /// the cursor pointed at a button on the screen that just went away, and focus may have been
        /// taken by a panel that builds its own (the inventory and sphere grid both focus their
        /// subtree).
        /// </summary>
        private void ResetKeyboardNavigation()
        {
            if (_nav == null)
            {
                return;
            }
            _nav.Reset();
            if (_root.panel != null)
            {
                _root.Focus();
            }
            PanelKeyboard.Claim();
        }

        /// <summary>
        /// Keeps the keyboard reachable. A UITK panel receives the OS keyboard only while its
        /// PanelEventHandler is the EventSystem's selected object; clicking a UITK element selects it
        /// as a side effect and clicking the background clears it again, so without this the arrows
        /// would quietly stop working with nothing on screen to explain why.
        /// </summary>
        private void Update()
        {
            PanelKeyboard.Claim();
        }

        // ============================================================
        //  VIEW SWITCHING
        // ============================================================

        private void ShowTown()
        {
            SetShown(_hubView, true);
            SetShown(_campaignView, false);
            SetShown(_progressView, false);
            SetShown(_completeView, false);
            SetShown(_merchantView, false);
            SetShown(_partyView, false);
            SetShown(_gridView, false);
            SetShown(_forgeView, false);
            SetShown(_bestiaryView, false);
            SetShown(_inventoryView, false);
            ResetKeyboardNavigation();

            RefreshTown();
            RefreshCurrencyHeader();
        }

        private static string RunKeyOf(RunDefinitionSO run)
        {
            return CampaignOps.RunKeyOf(run);
        }

        /// <summary>
        /// The run this save is on: looked up in the campaign by the key in <c>Run.json</c>, so the
        /// progress screen and the dungeon load whichever branch the player actually chose. Falls back
        /// to the single serialized run for a scene with no campaign authored.
        /// </summary>
        private RunDefinitionSO ActiveRunDefinition()
        {
            if (_campaign != null)
            {
                var node = _campaign.FindNode(_runSaveData.RunKey);
                if (node?.Run != null)
                {
                    return node.Run;
                }
            }
            return _runDefinition;
        }

        private void RefreshCurrencyHeader()
        {
            _goldLabel.text = $"Gold: {MetaProgressManager.Instance.Gold}";
            _essenceLabel.text = $"Essence: {MetaProgressManager.Instance.Essence}";
        }

        private void SetFeedback(string message)
        {
            if (_hubFeedback != null)
            {
                _hubFeedback.text = message ?? string.Empty;
            }
        }

        private void ShowRunProgressPanel()
        {
            SetShown(_hubView, false);
            SetShown(_campaignView, false);
            SetShown(_progressView, true);
            SetShown(_completeView, false);
            SetShown(_partyView, false);
            ResetKeyboardNavigation();

            var run = ActiveRunDefinition();
            if (run == null || run.Levels.Count == 0)
            {
                Debug.LogError($"No run definition resolves for RunKey '{_runSaveData.RunKey}'.");
                ShowTown();
                return;
            }

            var levelIndex = Mathf.Clamp(_runSaveData.CurrentLevelIndex, 0, run.Levels.Count - 1);
            var levelEntry = run.Levels[levelIndex];

            _levelIndicator.text = $"Level {levelIndex + 1} of {run.Levels.Count}";
            _levelName.text = levelEntry.LevelName;

            if (_progressParty != null)
            {
                _progressParty.text = _partySelect != null ? _partySelect.FieldedSummary() : string.Empty;
            }
        }

        private void ShowRunCompletePanel()
        {
            SetShown(_hubView, false);
            SetShown(_progressView, false);
            SetShown(_completeView, true);
            ResetKeyboardNavigation();
            _justCompletedRun = false;
        }

        // ============================================================
        //  THE ROAD — starting and continuing a run
        // ============================================================

        /// <summary>
        /// The way out of town. Always available: the story is not a building, and per-node
        /// startability is <c>CampaignOps</c>' decision, not the hub's.
        ///
        /// <para>The map already renders the active run as continuable, which is why there is no
        /// separate "Continue Run" affordance here — one door, two meanings, decided by the save.</para>
        /// </summary>
        private void OnTakeTheRoad()
        {
            if (_campaignMap != null)
            {
                SetShown(_hubView, false);
                _campaignMap.Show(_runSaveData.RunKey);
                return;
            }

            // No campaign authored: fall back to the single serialized run, and never restart a
            // completed one-shot.
            if (_runDefinition == null)
            {
                Debug.LogError("No campaign in Resources and no fallback run assigned; nowhere to go.");
                return;
            }

            var runKey = RunKeyOf(_runDefinition);
            if (!_runDefinition.Repeatable && MetaProgressManager.Instance.HasCompletedRun(runKey))
            {
                SetFeedback("Nothing left to run.");
                return;
            }

            if (_runSaveData.RunKey != runKey)
            {
                _runSaveData = new RunSaveData { RunKey = runKey, CurrentLevelIndex = 0 };
                _fileHandler.Save(_runSaveData);
            }
            ShowRunProgressPanel();
        }

        /// <summary>
        /// A run picked on the campaign map. Continuing the active run just re-opens the progress
        /// screen; anything else starts fresh. This is the only place besides <see cref="OnTakeTheRoad"/>
        /// that writes <c>Run.json</c>, so the map cannot overwrite a run in progress by accident -
        /// <c>CampaignOps</c> has already refused to mark another node startable while one is underway.
        /// </summary>
        private void OnRunChosen(RunDefinitionSO run)
        {
            if (run == null)
            {
                return;
            }

            var runKey = RunKeyOf(run);
            if (_runSaveData.RunKey != runKey)
            {
                _runSaveData = new RunSaveData
                {
                    RunKey = runKey,
                    CurrentLevelIndex = 0
                };
                _fileHandler.Save(_runSaveData);
            }

            SetShown(_campaignView, false);
            ShowRunProgressPanel();
        }

        private void OnEnterDungeon()
        {
            var run = ActiveRunDefinition();
            if (run == null || run.Levels.Count == 0)
            {
                Debug.LogError($"No run definition resolves for RunKey '{_runSaveData.RunKey}'.");
                return;
            }

            var levelIndex = Mathf.Clamp(_runSaveData.CurrentLevelIndex, 0, run.Levels.Count - 1);
            var levelEntry = run.Levels[levelIndex];

            DungeonManager.ActiveRun = run;
            DungeonManager.RunLevelIndex = levelIndex;
            DungeonManager.LevelToLoad = levelEntry.LevelTemplate;

            DungeonManager.SeedToLoad = _runSaveData.ActiveDungeonSeed != 0
                ? _runSaveData.ActiveDungeonSeed
                : (int?)null;

            SceneManager.LoadScene("MainGameScene");
        }

        // ============================================================
        //  SERVICES
        // ============================================================

        private void OnVisitMerchant()
        {
            SetShown(_hubView, false);
            _merchant.Show();
            ResetKeyboardNavigation();
        }

        private void OnVisitSphereGrid()
        {
            SetShown(_hubView, false);
            _sphereGrid.Show();
        }

        private void OnVisitParty()
        {
            _partyOpenedFromProgress = false;
            SetShown(_hubView, false);
            _partySelect.Show();
            ResetKeyboardNavigation();
        }

        private void OnChangePartyFromProgress()
        {
            _partyOpenedFromProgress = true;
            SetShown(_progressView, false);
            _partySelect.Show();
            ResetKeyboardNavigation();
        }

        /// <summary>
        /// Back out of party select. Returning to the run-progress screen re-renders it, so the lineup
        /// line reflects whatever was just changed.
        /// </summary>
        private void OnPartyClosed()
        {
            if (_partyOpenedFromProgress)
            {
                ShowRunProgressPanel();
                return;
            }
            ShowTown();
        }

        private void OnVisitForge()
        {
            SetShown(_hubView, false);
            _forge.Show();
            ResetKeyboardNavigation();
        }

        private void OnVisitBestiary()
        {
            SetShown(_hubView, false);
            _bestiary.Show();
        }

        private void OnVisitInventory()
        {
            SetShown(_hubView, false);
            _inventory.Show();
        }

        private void OnBack()
        {
            ShowTown();
        }

        private void OnRunCompleteReturn()
        {
            ShowTown();
        }

        /// <summary>
        /// Back to the title screen — the only route out of the hub, and where Options lives. Nothing
        /// is lost by leaving: every manager here reloads from disk, and progress is written as it
        /// happens rather than on exit.
        /// </summary>
        private void OnLeaveToMainMenu()
        {
            SceneManager.LoadScene("MenuScene");
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static bool IsShown(VisualElement element)
        {
            return element != null && element.resolvedStyle.display != DisplayStyle.None;
        }
    }
}
