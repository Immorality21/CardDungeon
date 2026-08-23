using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.IO;
using Assets.Scripts.Items.UI;
using Assets.Scripts.MainMenu;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Main menu (UI Toolkit). Drives a single UIDocument holding the home, run-progress,
/// run-complete, merchant, tavern, party-select, forge and inventory views. Each panel is a plain
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
    [SerializeField] private RunDefinitionSO _runDefinition;
    [SerializeField] private PartyRosterSO _partyRoster;
    [SerializeField] private UIDocument _document;

    private VisualElement _homeView;
    private VisualElement _progressView;
    private VisualElement _completeView;
    private VisualElement _merchantView;
    private VisualElement _tavernView;
    private VisualElement _partyView;
    private VisualElement _gridView;
    private VisualElement _forgeView;
    private VisualElement _inventoryView;

    private Button _continueButton;
    private Button _newRunButton;
    private Button _merchantButton;
    private Button _tavernButton;
    private Button _partyButton;
    private Button _gridButton;
    private Button _progressPartyButton;
    private Button _forgeButton;
    private Button _inventoryButton;
    private Button _backButton;
    private Button _enterButton;
    private Button _returnButton;

    private Label _homeGold;
    private Label _homeEssence;
    private Label _levelIndicator;
    private Label _levelName;
    private Label _progressParty;

    private MerchantUI _merchant;
    private TavernUI _tavern;
    private PartySelectUI _partySelect;
    private SphereGridUI _sphereGrid;
    private MagicForgeUI _forge;
    private InventoryHubUI _inventory;

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

        if (_document == null)
        {
            _document = GetComponent<UIDocument>();
        }
        var root = _document.rootVisualElement;

        _homeView = root.Q<VisualElement>("home-view");
        _progressView = root.Q<VisualElement>("progress-view");
        _completeView = root.Q<VisualElement>("complete-view");
        _merchantView = root.Q<VisualElement>("merchant-view");
        _tavernView = root.Q<VisualElement>("tavern-view");
        _partyView = root.Q<VisualElement>("party-view");
        _gridView = root.Q<VisualElement>("grid-view");
        _forgeView = root.Q<VisualElement>("forge-view");
        _inventoryView = root.Q<VisualElement>("inventory-view");

        _continueButton = root.Q<Button>("continue-btn");
        _newRunButton = root.Q<Button>("new-btn");
        _merchantButton = root.Q<Button>("merchant-btn");
        _tavernButton = root.Q<Button>("tavern-btn");
        _partyButton = root.Q<Button>("party-btn");
        _gridButton = root.Q<Button>("grid-btn");
        _progressPartyButton = root.Q<Button>("progress-party-btn");
        _forgeButton = root.Q<Button>("forge-btn");
        _inventoryButton = root.Q<Button>("inventory-btn");
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
        _inventoryButton.clicked += OnVisitInventory;

        _merchant = new MerchantUI(_merchantView);
        _tavern = new TavernUI(_tavernView, _partyRoster);
        _partySelect = _partyView != null ? new PartySelectUI(_partyView, _partyRoster) : null;
        _sphereGrid = _gridView != null ? new SphereGridUI(_gridView, _partyRoster) : null;
        _forge = new MagicForgeUI(_forgeView);
        _inventory = new InventoryHubUI(_inventoryView, _partyRoster);
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
        _inventory.OnClosed += ShowHomePanel;

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

    private void ShowHomePanel()
    {
        SetShown(_homeView, true);
        SetShown(_progressView, false);
        SetShown(_completeView, false);
        SetShown(_merchantView, false);
        SetShown(_tavernView, false);
        SetShown(_partyView, false);
        SetShown(_gridView, false);
        SetShown(_forgeView, false);
        SetShown(_inventoryView, false);

        bool hasActiveRun = !string.IsNullOrEmpty(_runSaveData.RunKey);
        SetShown(_continueButton, hasActiveRun);

        // A completed non-repeatable run (the tutorial) cannot be started again. An active run is
        // still continuable — completion only gates starting fresh.
        bool runLocked = _runDefinition != null
            && !_runDefinition.Repeatable
            && MetaProgressManager.Instance.HasCompletedRun(RunKeyOf(_runDefinition));
        SetShown(_newRunButton, !runLocked);

        RefreshCurrencyHeader();
    }

    private static string RunKeyOf(RunDefinitionSO run)
    {
        return !string.IsNullOrEmpty(run.Key) ? run.Key : run.name;
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

        var levelIndex = _runSaveData.CurrentLevelIndex;
        var totalLevels = _runDefinition.Levels.Count;
        var levelEntry = _runDefinition.Levels[levelIndex];

        _levelIndicator.text = $"Level {levelIndex + 1} of {totalLevels}";
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
        _justCompletedRun = false;
    }

    private void OnNewRun()
    {
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

    private void OnEnterDungeon()
    {
        var levelIndex = _runSaveData.CurrentLevelIndex;
        var levelEntry = _runDefinition.Levels[levelIndex];

        DungeonManager.ActiveRun = _runDefinition;
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
    }

    private void OnVisitTavern()
    {
        SetShown(_homeView, false);
        _tavern.Show();
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
    }

    private void OnVisitInventory()
    {
        SetShown(_homeView, false);
        _inventory.Show();
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
}
