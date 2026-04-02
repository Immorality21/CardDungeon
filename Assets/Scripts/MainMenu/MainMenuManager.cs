using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.IO;
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
    private Button _manageDeckButton;

    [SerializeField]
    private DeckManagementUI _deckManagementUI;

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
        _manageDeckButton.onClick.AddListener(OnManageDeck);

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
        _continueRunButton.gameObject.SetActive(hasActiveRun);
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
        // Delete any existing dungeon saves for a clean start
        var dungeonSaveManager = new DungeonSaveManager();

        // Create fresh run save
        _runSaveData = new RunSaveData
        {
            RunKey = _runDefinition.Key,
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
        DungeonManager.SeedToLoad = null;

        if (levelEntry.IsStatic)
        {
            DungeonManager.FixedSeed = levelEntry.FixedSeed;
        }
        else
        {
            DungeonManager.FixedSeed = 0;
        }

        SceneManager.LoadScene("MainGameScene");
    }

    private void OnManageDeck()
    {
        _homePanel.SetActive(false);

        if (_deckManagementUI != null)
        {
            _deckManagementUI.OnClosed += OnDeckClosed;
            _deckManagementUI.Show();
        }
    }

    private void OnDeckClosed()
    {
        if (_deckManagementUI != null)
        {
            _deckManagementUI.OnClosed -= OnDeckClosed;
        }

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
