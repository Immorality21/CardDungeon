using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.IO;
using Assets.Scripts.MainMenu;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Main menu (UI Toolkit). Drives a single UIDocument holding the home, run-progress,
/// run-complete, merchant, and forge views. Merchant/Forge are plain view-controllers
/// operating on their subtrees. Run-save and scene-load logic is unchanged from the
/// prior uGUI version.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private RunDefinitionSO _runDefinition;
    [SerializeField] private UIDocument _document;

    private VisualElement _homeView;
    private VisualElement _progressView;
    private VisualElement _completeView;
    private VisualElement _merchantView;
    private VisualElement _forgeView;

    private Button _continueButton;
    private Button _newRunButton;
    private Button _merchantButton;
    private Button _forgeButton;
    private Button _backButton;
    private Button _enterButton;
    private Button _returnButton;

    private Label _homeGold;
    private Label _homeEssence;
    private Label _levelIndicator;
    private Label _levelName;

    private MerchantUI _merchant;
    private MagicForgeUI _forge;

    private FileHandler _fileHandler;
    private RunSaveData _runSaveData;

    private static bool _justCompletedRun;

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
        _forgeView = root.Q<VisualElement>("forge-view");

        _continueButton = root.Q<Button>("continue-btn");
        _newRunButton = root.Q<Button>("new-btn");
        _merchantButton = root.Q<Button>("merchant-btn");
        _forgeButton = root.Q<Button>("forge-btn");
        _backButton = root.Q<Button>("back-btn");
        _enterButton = root.Q<Button>("enter-btn");
        _returnButton = root.Q<Button>("return-btn");

        _homeGold = root.Q<Label>("home-gold");
        _homeEssence = root.Q<Label>("home-essence");
        _levelIndicator = root.Q<Label>("level-indicator");
        _levelName = root.Q<Label>("level-name");

        _newRunButton.clicked += OnNewRun;
        _continueButton.clicked += OnContinueRun;
        _enterButton.clicked += OnEnterDungeon;
        _backButton.clicked += OnBack;
        _returnButton.clicked += OnRunCompleteReturn;
        _merchantButton.clicked += OnVisitMerchant;
        _forgeButton.clicked += OnVisitForge;

        _merchant = new MerchantUI(_merchantView);
        _forge = new MagicForgeUI(_forgeView);
        _merchant.OnClosed += ShowHomePanel;
        _forge.OnClosed += ShowHomePanel;

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
        SetShown(_forgeView, false);

        bool hasActiveRun = !string.IsNullOrEmpty(_runSaveData.RunKey);
        SetShown(_continueButton, hasActiveRun);

        RefreshCurrencyHeader();
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

        var levelIndex = _runSaveData.CurrentLevelIndex;
        var totalLevels = _runDefinition.Levels.Count;
        var levelEntry = _runDefinition.Levels[levelIndex];

        _levelIndicator.text = $"Level {levelIndex + 1} of {totalLevels}";
        _levelName.text = levelEntry.LevelName;
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
        var runKey = !string.IsNullOrEmpty(_runDefinition.Key) ? _runDefinition.Key : _runDefinition.name;
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

    private void OnVisitForge()
    {
        SetShown(_homeView, false);
        _forge.Show();
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
