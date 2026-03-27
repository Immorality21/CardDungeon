using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using UnityEditor;
using UnityEngine;

public class CardCollectionEditor : EditorWindow
{
    private const string FileName = "CardCollection.json";

    private string _savePath;
    private CardCollectionSaveData _saveData;
    private List<CardSO> _allCards;
    private List<HeroSO> _allHeroes;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Save Data/Card Collection")]
    public static void Open()
    {
        GetWindow<CardCollectionEditor>("Card Collection Save Data");
    }

    private void OnEnable()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "savedata", FileName);
        RefreshDatabase();
        LoadFromDisk();
    }

    private void RefreshDatabase()
    {
        var cardGuids = AssetDatabase.FindAssets("t:CardSO", new[] { "Assets/ScriptableObjects/Cards" });
        _allCards = cardGuids
            .Select(g => AssetDatabase.LoadAssetAtPath<CardSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .OrderBy(c => c.Key)
            .ToList();

        var heroGuids = AssetDatabase.FindAssets("t:HeroSO", new[] { "Assets/ScriptableObjects/Heroes" });
        _allHeroes = heroGuids
            .Select(g => AssetDatabase.LoadAssetAtPath<HeroSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(h => h != null)
            .OrderBy(h => h.Label)
            .ToList();
    }

    private void LoadFromDisk()
    {
        _saveData = new CardCollectionSaveData();

        if (File.Exists(_savePath))
        {
            var json = File.ReadAllText(_savePath);
            JsonUtility.FromJsonOverwrite(json, _saveData);
        }
    }

    private void SaveToDisk()
    {
        var dir = Path.GetDirectoryName(_savePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonUtility.ToJson(_saveData, true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"Card collection saved to {_savePath}");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Card Collection Save Data", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_savePath, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // --- Add card buttons ---
        EditorGUILayout.LabelField("Add Card:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        int col = 0;
        foreach (var card in _allCards)
        {
            if (GUILayout.Button(card.Key, GUILayout.MinWidth(80)))
            {
                _saveData.Cards.Add(new CardSaveData { CardKey = card.Key });
                SaveToDisk();
            }

            col++;
            if (col >= 5)
            {
                col = 0;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // --- Current cards ---
        EditorGUILayout.LabelField($"Owned Cards ({_saveData.Cards.Count})", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = _saveData.Cards.Count - 1; i >= 0; i--)
        {
            var entry = _saveData.Cards[i];
            var so = _allCards.Find(x => x.Key == entry.CardKey);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Icon
            if (so != null && so.Icon != null)
            {
                var rect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24));
                GUI.DrawTexture(rect, so.Icon.texture, ScaleMode.ScaleToFit);
            }

            // Key & info
            string label = so != null
                ? $"{so.Key}  ({so.DisplayName} — {so.Rarity} {so.EffectType} Pow:{so.Power})"
                : $"{entry.CardKey}  (MISSING SO)";
            EditorGUILayout.LabelField(label);

            // Hero assignment
            string currentHero = string.IsNullOrEmpty(entry.AssignedHeroKey)
                ? "Unassigned"
                : entry.AssignedHeroKey;

            var heroOptions = new List<string> { "Unassigned" };
            heroOptions.AddRange(_allHeroes.Select(h => h.Label));

            int selectedIndex = heroOptions.IndexOf(currentHero);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            int newIndex = EditorGUILayout.Popup(selectedIndex, heroOptions.ToArray(), GUILayout.Width(120));
            if (newIndex != selectedIndex)
            {
                entry.AssignedHeroKey = newIndex == 0 ? null : heroOptions[newIndex];
                SaveToDisk();
            }

            // Remove
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _saveData.Cards.RemoveAt(i);
                SaveToDisk();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        // --- Bottom toolbar ---
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Reload from Disk"))
        {
            LoadFromDisk();
        }

        if (GUILayout.Button("Clear All Cards"))
        {
            if (EditorUtility.DisplayDialog("Clear Cards",
                    "Remove all cards from save data?", "Yes", "Cancel"))
            {
                _saveData.Cards.Clear();
                SaveToDisk();
            }
        }

        if (GUILayout.Button("Open Save Folder"))
        {
            EditorUtility.RevealInFinder(_savePath);
        }

        EditorGUILayout.EndHorizontal();
    }
}
