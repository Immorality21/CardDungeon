using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.IO;
using Assets.Scripts.MainMenu;
using Assets.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Run Definition")]
    [SerializeField]
    private RunDefinitionSO _runDefinition;

    [Header("Home Panel")]
    [SerializeField]
    private GameObject _homePanel;

    [SerializeField]
    private Button _continueRunButton;

    [SerializeField]
    private Button _newRunButton;

    [SerializeField]
    private Button _merchantButton;

    [SerializeField]
    private MerchantUI _merchantUI;

    [SerializeField]
    private Button _forgeButton;

    [SerializeField]
    private MagicForgeUI _cardUpgradeUI;

    [Header("Currency Header (optional)")]
    [SerializeField]
    private TextMeshProUGUI _goldLabel;

    [SerializeField]
    private TextMeshProUGUI _essenceLabel;

    [Header("Run Progress Panel")]
    [SerializeField]
    private GameObject _runProgressPanel;

    [SerializeField]
    private TextMeshProUGUI _levelIndicatorLabel;

    [SerializeField]
    private TextMeshProUGUI _levelNameLabel;

    [SerializeField]
    private Button _enterDungeonButton;

    [SerializeField]
    private Button _backButton;

    [Header("Run Complete Panel")]
    [SerializeField]
    private GameObject _runCompletePanel;

    [SerializeField]
    private Button _runCompleteReturnButton;

    private FileHandler _fileHandler;
    private RunSaveData _runSaveData;

    private void Start()
    {
        _fileHandler = new FileHandler();
        _runSaveData = _fileHandler.Load<RunSaveData>();

        _newRunButton.onClick.AddListener(OnNewRun);
        _continueRunButton.onClick.AddListener(OnContinueRun);
        _enterDungeonButton.onClick.AddListener(OnEnterDungeon);
        _backButton.onClick.AddListener(OnBack);
        _runCompleteReturnButton.onClick.AddListener(OnRunCompleteReturn);

        if (_merchantButton != null)
        {
            _merchantButton.onClick.AddListener(OnVisitMerchant);
        }
        if (_forgeButton != null)
        {
            _forgeButton.onClick.AddListener(OnVisitForge);
        }

        // Check if run was just completed (ActiveRun cleared after final level)
        if (DungeonManager.ActiveRun == null && !string.IsNullOrEmpty(_runSaveData.RunKey))
        {
            // Run still in progress — show home
            ShowHomePanel();
        }
        else if (DungeonManager.ActiveRun == null && string.IsNullOrEmpty(_runSaveData.RunKey) && WasRunJustCompleted())
        {
            ShowRunCompletePanel();
        }
        else
        {
            ShowHomePanel();
        }
    }

    private bool WasRunJustCompleted()
    {
        // If we arrived from a dungeon clear and run save was deleted, the run is complete
        // We detect this by checking if ActiveRun was cleared by DungeonManager.OnDungeonCleared
        // Since ActiveRun is set to null when the final level is cleared, and we just came from
        // the game scene, we use a simple static flag
        return _justCompletedRun;
    }

    private static bool _justCompletedRun;

    public static void MarkRunCompleted()
    {
        _justCompletedRun = true;
    }

    private void ShowHomePanel()
    {
        _homePanel.SetActive(true);
        _runProgressPanel.SetActive(false);
        _runCompletePanel.SetActive(false);

        bool hasActiveRun = !string.IsNullOrEmpty(_runSaveData.RunKey);
        Debug.Log($"ShowHomePanel: RunKey='{_runSaveData.RunKey}', CurrentLevel={_runSaveData.CurrentLevelIndex}, hasActiveRun={hasActiveRun}");
        _continueRunButton.gameObject.SetActive(hasActiveRun);

        RefreshCurrencyHeader();
    }

    private void RefreshCurrencyHeader()
    {
        if (_goldLabel != null)
        {
            _goldLabel.text = $"Gold: {MetaProgressManager.Instance.Gold}";
        }
        if (_essenceLabel != null)
        {
            _essenceLabel.text = $"Essence: {MetaProgressManager.Instance.Essence}";
        }
    }

    private void ShowRunProgressPanel()
    {
        _homePanel.SetActive(false);
        _runProgressPanel.SetActive(true);
        _runCompletePanel.SetActive(false);

        var levelIndex = _runSaveData.CurrentLevelIndex;
        var totalLevels = _runDefinition.Levels.Count;
        var levelEntry = _runDefinition.Levels[levelIndex];

        _levelIndicatorLabel.text = $"Level {levelIndex + 1} of {totalLevels}";
        _levelNameLabel.text = levelEntry.LevelName;
    }

    private void ShowRunCompletePanel()
    {
        _homePanel.SetActive(false);
        _runProgressPanel.SetActive(false);
        _runCompletePanel.SetActive(true);
        _justCompletedRun = false;
    }

    private void OnNewRun()
    {
        // Create fresh run save
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

        // Resume from existing dungeon save if available
        if (_runSaveData.ActiveDungeonSeed != 0)
        {
            DungeonManager.SeedToLoad = _runSaveData.ActiveDungeonSeed;
        }
        else
        {
            DungeonManager.SeedToLoad = null;
        }

        SceneManager.LoadScene("MainGameScene");
    }

    private void OnVisitMerchant()
    {
        if (_merchantUI == null)
        {
            return;
        }

        _homePanel.SetActive(false);
        _merchantUI.OnClosed += OnMerchantClosed;
        _merchantUI.Show();
    }

    private void OnMerchantClosed()
    {
        _merchantUI.OnClosed -= OnMerchantClosed;
        ShowHomePanel();
    }

    private void OnVisitForge()
    {
        if (_cardUpgradeUI == null)
        {
            return;
        }

        _homePanel.SetActive(false);
        _cardUpgradeUI.OnClosed += OnForgeClosed;
        _cardUpgradeUI.Show();
    }

    private void OnForgeClosed()
    {
        _cardUpgradeUI.OnClosed -= OnForgeClosed;
        ShowHomePanel();
    }

    private void OnBack()
    {
        ShowHomePanel();
    }

    private void OnRunCompleteReturn()
    {
        ShowHomePanel();
    }
}
