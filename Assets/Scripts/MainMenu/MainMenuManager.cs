using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;
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

    private FileHandler _fileHandler;
    private List<GameObject> _spawnedEntries = new List<GameObject>();

    private void Start()
    {
        _fileHandler = new FileHandler();
        PopulateSaveList();
    }

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
                button.onClick.AddListener(() => LoadDungeon(seed));
            }
        }
    }

    public void StartNewDungeon()
    {
        RoomManager.SeedToLoad = null;
        SceneManager.LoadScene("MainGameScene");
    }

    private void LoadDungeon(int seed)
    {
        RoomManager.SeedToLoad = seed;
        SceneManager.LoadScene("MainGameScene");
    }
}
