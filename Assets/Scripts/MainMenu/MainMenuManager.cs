using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField]
    private Transform _levelParent;

    [SerializeField]
    private GameObject _levelSelectPrefab;

    [SerializeField]
    private List<LevelDefinitionSO> _availableLevelTemplates;

    [SerializeField]
    private TMP_Dropdown _levelDropdown;

    private FileHandler _fileHandler;
    private List<GameObject> _spawnedEntries = new List<GameObject>();

    private void Start()
    {
        _fileHandler = new FileHandler();
        PopulateLevelDropdown();
        PopulateSaveList();
    }

    private void PopulateLevelDropdown()
    {
        _levelDropdown.ClearOptions();

        var options = new List<string>();
        foreach (var level in _availableLevelTemplates)
        {
            options.Add(level.name);
        }

        _levelDropdown.AddOptions(options);
    }

    private LevelDefinitionSO SelectedLevel => _availableLevelTemplates[_levelDropdown.value];

    private void PopulateSaveList()
    {
        foreach (var entry in _spawnedEntries)
        {
            Destroy(entry);
        }
        _spawnedEntries.Clear();

        var files = _fileHandler.FindFiles("Dungeon_");

        foreach (var filePath in files)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var data = _fileHandler.LoadFromFile<DungeonSaveData>(fileName);

            if (data.Seed == 0)
            {
                continue;
            }

            var entry = Instantiate(_levelSelectPrefab, _levelParent);
            _spawnedEntries.Add(entry);

            var label = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                int exploredCount = 0;
                foreach (var room in data.Rooms)
                {
                    if (room.IsExplored) exploredCount++;
                }
                label.text = $"Seed: {data.Seed}  ({exploredCount}/{data.Rooms.Count} rooms)";
            }

            var button = entry.GetComponentInChildren<Button>();
            if (button != null)
            {
                var seed = data.Seed;
                var levelKey = data.LevelKey;
                button.onClick.AddListener(() => LoadDungeon(seed, levelKey));
            }
        }
    }

    public void StartNewDungeon()
    {
        DungeonManager.SeedToLoad = null;
        DungeonManager.LevelToLoad = SelectedLevel;
        SceneManager.LoadScene("MainGameScene");
    }

    private void LoadDungeon(int seed, string levelKey)
    {
        DungeonManager.SeedToLoad = seed;
        DungeonManager.LevelToLoad = FindLevel(levelKey);
        SceneManager.LoadScene("MainGameScene");
    }

    private LevelDefinitionSO FindLevel(string key)
    {
        foreach (var level in _availableLevelTemplates)
        {
            if (level.Key == key)
            {
                return level;
            }
        }

        Debug.LogWarning($"Level with key '{key}' not found, using first available.");
        return _availableLevelTemplates[0];
    }
}
