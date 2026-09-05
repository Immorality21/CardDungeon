using Assets.Scripts.Audio;
using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.IO;
using Assets.Scripts.Items.UI;
using Assets.Scripts.MainMenu;
using Assets.Scripts.Progression;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Main menu (UI Toolkit). Drives a single UIDocument holding the home, run-progress,
/// run-complete, merchant, tavern, party-select, forge, bestiary, inventory and options views. Each panel is a plain
/// view-controller operating on its subtree. Run-save and scene-load logic is unchanged from the
/// prior uGUI version.
///
/// Party select is reachable from two places on purpose: from home like any other hub screen, and
/// from the run-progress screen next to "Enter Dungeon", which is the moment the choice actually
/// matters. Closing it returns to whichever of the two opened it.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Fallback run for a scene with no Campaign asset in Resources. With a campaign the run " +
             "is resolved from the graph by the key in Run.json, so this is not the run that plays.")]
    private RunDefinitionSO _runDefinition;
    [SerializeField] private PartyRosterSO _partyRoster;
    [SerializeField] private UIDocument _document;

    private VisualElement _campaignView;
    private VisualElement _homeView;
    private VisualElement _progressView;
    private VisualElement _completeView;
    private VisualElement _merchantView;
    private VisualElement _tavernView;
    private VisualElement _partyView;
    private VisualElement _gridView;
    private VisualElement _forgeView;
    private VisualElement _bestiaryView;
    private VisualElement _inventoryView;
    private VisualElement _optionsView;

    private Button _continueButton;
    private Button _newRunButton;
    private Button _merchantButton;
    private Button _tavernButton;
    private Button _partyButton;
    private Button _gridButton;
    private Button _progressPartyButton;
    private Button _forgeButton;
    private Button _bestiaryButton;
    private Button _inventoryButton;
    private Button _optionsButton;
    private Button _backButton;
    private Button _enterButton;
    private Button _returnButton;

    private Label _homeGold;
    private Label _homeEssence;
    private Label _levelIndicator;
    private Label _levelName;
    private Label _progressParty;

    private VisualElement _root;
    private KeyboardNavigator _nav;

    private CampaignSO _campaign;
    private CampaignMapUI _campaignMap;
    private MerchantUI _merchant;
    private TavernUI _tavern;
    private PartySelectUI _partySelect;
    private SphereGridUI _sphereGrid;
    private MagicForgeUI _forge;
    private BestiaryUI _bestiary;
    private InventoryHubUI _inventory;
    private AudioOptionsUI _options;

    private FileHandler _fileHandler;
    private RunSaveData _runSaveData;

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

        // The campaign is Resources-loaded like the item catalog, so the hub resolves the whole run
        // graph without a scene reference - and without AssetDatabase, which does not exist in a build.
        _campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);

        // A spell a hero starts with is never "bought", so nothing else would ever record it as
        // discovered and the Forge would refuse to upgrade the one spell everyone owns.
        HeroRoster.MarkDefaultUnlocksDiscovered(_partyRoster);

        if (_document == null)
        {
            _document = GetComponent<UIDocument>();
        }
        var root = _document.rootVisualElement;
        _root = root;

        _campaignView = root.Q<VisualElement>("campaign-view");
        _homeView = root.Q<VisualElement>("home-view");
        _progressView = root.Q<VisualElement>("progress-view");
        _completeView = root.Q<VisualElement>("complete-view");
        _merchantView = root.Q<VisualElement>("merchant-view");
        _tavernView = root.Q<VisualElement>("tavern-view");
        _partyView = root.Q<VisualElement>("party-view");
        _gridView = root.Q<VisualElement>("grid-view");
        _forgeView = root.Q<VisualElement>("forge-view");
        _bestiaryView = root.Q<VisualElement>("bestiary-view");
        _inventoryView = root.Q<VisualElement>("inventory-view");
        _optionsView = root.Q<VisualElement>("options-view");

        _continueButton = root.Q<Button>("continue-btn");
        _newRunButton = root.Q<Button>("new-btn");
        _merchantButton = root.Q<Button>("merchant-btn");
        _tavernButton = root.Q<Button>("tavern-btn");
        _partyButton = root.Q<Button>("party-btn");
        _gridButton = root.Q<Button>("grid-btn");
        _progressPartyButton = root.Q<Button>("progress-party-btn");
        _forgeButton = root.Q<Button>("forge-btn");
        _bestiaryButton = root.Q<Button>("bestiary-btn");
        _inventoryButton = root.Q<Button>("inventory-btn");
        _optionsButton = root.Q<Button>("options-btn");
        _backButton = root.Q<Button>("back-btn");
        _enterButton = root.Q<Button>("enter-btn");
        _returnButton = root.Q<Button>("return-btn");

        _homeGold = root.Q<Label>("home-gold");
        _homeEssence = root.Q<Label>("home-essence");
        _levelIndicator = root.Q<Label>("level-indicator");
        _levelName = root.Q<Label>("level-name");
        _progressParty = root.Q<Label>("progress-party");

        _newRunButton.clicked += OnNewRun;
        _continueButton.clicked += OnContinueRun;
        _enterButton.clicked += OnEnterDungeon;
        _backButton.clicked += OnBack;
        _returnButton.clicked += OnRunCompleteReturn;
        _merchantButton.clicked += OnVisitMerchant;
        if (_tavernButton != null)
        {
            _tavernButton.clicked += OnVisitTavern;
        }
        if (_partyButton != null)
        {
            _partyButton.clicked += OnVisitParty;
        }
        if (_gridButton != null)
        {
            _gridButton.clicked += OnVisitSphereGrid;
        }
        if (_progressPartyButton != null)
        {
            _progressPartyButton.clicked += OnChangePartyFromProgress;
        }
        _forgeButton.clicked += OnVisitForge;
        if (_bestiaryButton != null)
        {
            _bestiaryButton.clicked += OnVisitBestiary;
        }
        _inventoryButton.clicked += OnVisitInventory;
        if (_optionsButton != null)
        {
            _optionsButton.clicked += OnVisitOptions;
        }

        _campaignMap = _campaignView != null && _campaign != null
            ? new CampaignMapUI(_campaignView, _campaign)
            : null;
        _merchant = new MerchantUI(_merchantView);
        _tavern = new TavernUI(_tavernView, _partyRoster);
        _partySelect = _partyView != null ? new PartySelectUI(_partyView, _partyRoster) : null;
        _sphereGrid = _gridView != null ? new SphereGridUI(_gridView, _partyRoster) : null;
        _forge = new MagicForgeUI(_forgeView);
        _bestiary = _bestiaryView != null ? new BestiaryUI(_bestiaryView) : null;
        _inventory = new InventoryHubUI(_inventoryView, _partyRoster);
        _options = _optionsView != null ? new AudioOptionsUI(_optionsView) : null;
        if (_campaignMap != null)
        {
            _campaignMap.OnClosed += ShowHomePanel;
            _campaignMap.OnRunChosen += OnRunChosen;
        }
        _merchant.OnClosed += ShowHomePanel;
        _tavern.OnClosed += ShowHomePanel;
        if (_partySelect != null)
        {
            _partySelect.OnClosed += OnPartyClosed;
        }
        if (_sphereGrid != null)
        {
            _sphereGrid.OnClosed += ShowHomePanel;
        }
        _forge.OnClosed += ShowHomePanel;
        if (_bestiary != null)
        {
            _bestiary.OnClosed += ShowHomePanel;
        }
        _inventory.OnClosed += ShowHomePanel;
        if (_options != null)
        {
            _options.OnClosed += ShowHomePanel;
        }

        SetUpKeyboardNavigation();

        // The hub's own bed. Starting it here rather than on the button that leaves the dungeon means
        // it also covers a fresh launch and a return after a wipe.
        MusicPlayer.Play(MusicTrack.Hub);

        // Initial panel: run complete only when we arrived from clearing the final level.
        if (DungeonManager.ActiveRun == null && string.IsNullOrEmpty(_runSaveData.RunKey) && _justCompletedRun)
        {
            ShowRunCompletePanel();
        }
        else
        {
            ShowHomePanel();
        }
    }

    // ============================================================
    //  KEYBOARD NAVIGATION
    // ============================================================

    /// <summary>
    /// Arrow keys for the hub, matching how combat and the room bar are driven. One
    /// <see cref="KeyboardNavigator"/> serves every screen this class owns, because it navigates
    /// whatever buttons are visible rather than a list wired per screen - and only one of these views
    /// is ever displayed at a time.
    ///
    /// <para>The screens that build their own cursors - the campaign map, the sphere grid, the
    /// bestiary, the inventory - are excluded by <see cref="NavigatesCurrentView"/>. They are children
    /// of this same root, so without that gate a key they chose not to handle would bubble up here and
    /// be acted on twice.</para>
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
        return IsShown(_homeView) || IsShown(_progressView) || IsShown(_completeView)
            || IsShown(_merchantView) || IsShown(_tavernView) || IsShown(_partyView)
            || IsShown(_forgeView) || IsShown(_optionsView);
    }

    /// <summary>
    /// Escape leaves the screen by pressing its own Back button rather than calling the panel's Hide
    /// directly, so backing out with the keyboard runs exactly the same path as clicking it - the
    /// panels raise <c>OnClosed</c> from there and this class depends on that to get home again.
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
        if (IsShown(_tavernView))
        {
            return _root.Q<Button>("tavern-close");
        }
        if (IsShown(_partyView))
        {
            return _root.Q<Button>("party-close");
        }
        if (IsShown(_optionsView))
        {
            return _root.Q<Button>("options-close");
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
        // Home has nowhere to back out to.
        return null;
    }

    /// <summary>
    /// Puts keyboard focus back on the root and drops the cursor. Called from every panel switch: the
    /// cursor pointed at a button on the screen that just went away, and focus may have been taken by
    /// a panel that builds its own (the inventory and sphere grid both focus their own subtree).
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
    /// Keeps the keyboard reachable. The hub only ever *seemed* not to need this: every screen here is
    /// entered by clicking a button, and clicking a UI Toolkit element selects the keyboard bridge as a
    /// side effect. Clicking the background instead clears it again, so the arrows would quietly stop
    /// working with nothing on screen to explain why. See <see cref="PanelKeyboard"/>.
    /// </summary>
    private void Update()
    {
        PanelKeyboard.Claim();
    }

    private void ShowHomePanel()
    {
        SetShown(_homeView, true);
        SetShown(_campaignView, false);
        SetShown(_progressView, false);
        SetShown(_completeView, false);
        SetShown(_merchantView, false);
        SetShown(_tavernView, false);
        SetShown(_partyView, false);
        SetShown(_gridView, false);
        SetShown(_forgeView, false);
        SetShown(_bestiaryView, false);
        SetShown(_inventoryView, false);
        SetShown(_optionsView, false);
        ResetKeyboardNavigation();

        bool hasActiveRun = !string.IsNullOrEmpty(_runSaveData.RunKey);
        SetShown(_continueButton, hasActiveRun);

        // With a campaign authored the button opens the map and is always available: which runs may
        // be started is the map's decision, per node. Only the no-campaign fallback still hides it,
        // and then only because that path can offer exactly one run - the tutorial - which is
        // one-shot. Gating the button on the tutorial while a campaign exists is what left a
        // finished save with no way into any dungeon at all.
        if (_campaignMap != null)
        {
            SetShown(_newRunButton, true);
        }
        else
        {
            bool runLocked = _runDefinition != null
                && !_runDefinition.Repeatable
                && MetaProgressManager.Instance.HasCompletedRun(RunKeyOf(_runDefinition));
            SetShown(_newRunButton, !runLocked);
        }

        RefreshCurrencyHeader();
    }

    private static string RunKeyOf(RunDefinitionSO run)
    {
        return CampaignOps.RunKeyOf(run);
    }

    /// <summary>
    /// The run this save is on: looked up in the campaign by the key in <c>Run.json</c>, so the
    /// progress screen and the dungeon load whichever branch the player actually chose. Falls back to
    /// the single serialized run for a scene with no campaign authored.
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
        _homeGold.text = $"Gold: {MetaProgressManager.Instance.Gold}";
        _homeEssence.text = $"Essence: {MetaProgressManager.Instance.Essence}";
    }

    private void ShowRunProgressPanel()
    {
        SetShown(_homeView, false);
        SetShown(_progressView, true);
        SetShown(_completeView, false);
        SetShown(_partyView, false);
        ResetKeyboardNavigation();

        var run = ActiveRunDefinition();
        if (run == null || run.Levels.Count == 0)
        {
            Debug.LogError($"No run definition resolves for RunKey '{_runSaveData.RunKey}'.");
            ShowHomePanel();
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
        SetShown(_homeView, false);
        SetShown(_progressView, false);
        SetShown(_completeView, true);
        ResetKeyboardNavigation();
        _justCompletedRun = false;
    }

    private void OnNewRun()
    {
        if (_campaignMap != null)
        {
            SetShown(_homeView, false);
            _campaignMap.Show(_runSaveData.RunKey);
            return;
        }

        var runKey = RunKeyOf(_runDefinition);

        // Backstop for the home-panel gate: never restart a completed one-shot run.
        if (!_runDefinition.Repeatable && MetaProgressManager.Instance.HasCompletedRun(runKey))
        {
            ShowHomePanel();
            return;
        }

        _runSaveData = new RunSaveData
        {
            RunKey = runKey,
            CurrentLevelIndex = 0
        };
        _fileHandler.Save(_runSaveData);

        ShowRunProgressPanel();
    }

    private void OnContinueRun()
    {
        ShowRunProgressPanel();
    }

    /// <summary>
    /// A run picked on the campaign map. Continuing the active run just re-opens the progress screen;
    /// anything else starts fresh. This is the only place besides <c>OnNewRun</c> that writes
    /// <c>Run.json</c>, so the map cannot overwrite a run in progress by accident - <c>CampaignOps</c>
    /// has already refused to mark another node startable while one is underway.
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

    private void OnVisitMerchant()
    {
        SetShown(_homeView, false);
        _merchant.Show();
        ResetKeyboardNavigation();
    }

    private void OnVisitTavern()
    {
        SetShown(_homeView, false);
        _tavern.Show();
        ResetKeyboardNavigation();
    }

    private void OnVisitSphereGrid()
    {
        if (_sphereGrid == null)
        {
            return;
        }
        SetShown(_homeView, false);
        _sphereGrid.Show();
    }

    private void OnVisitParty()
    {
        if (_partySelect == null)
        {
            return;
        }
        _partyOpenedFromProgress = false;
        SetShown(_homeView, false);
        _partySelect.Show();
        ResetKeyboardNavigation();
    }

    private void OnChangePartyFromProgress()
    {
        if (_partySelect == null)
        {
            return;
        }
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
        ShowHomePanel();
    }

    private void OnVisitForge()
    {
        SetShown(_homeView, false);
        _forge.Show();
        ResetKeyboardNavigation();
    }

    private void OnVisitBestiary()
    {
        if (_bestiary == null)
        {
            return;
        }
        SetShown(_homeView, false);
        _bestiary.Show();
    }

    private void OnVisitInventory()
    {
        SetShown(_homeView, false);
        _inventory.Show();
    }

    private void OnVisitOptions()
    {
        if (_options == null)
        {
            return; // an older scene whose UXML predates the screen
        }
        SetShown(_homeView, false);
        _options.Show();
        ResetKeyboardNavigation();
    }

    private void OnBack()
    {
        ShowHomePanel();
    }

    private void OnRunCompleteReturn()
    {
        ShowHomePanel();
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
        return element != null && element.resolvedStyle.display == DisplayStyle.Flex;
    }
}
