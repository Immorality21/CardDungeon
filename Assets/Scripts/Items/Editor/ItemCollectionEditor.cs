using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

public class ItemCollectionEditor : EditorWindow
{
    private const string FileName = "ItemCollection.json";

    private string _savePath;
    private ItemCollectionSaveData _saveData;
    private List<ItemSO> _allItems;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Save Data/Item Collection")]
    public static void Open()
    {
        GetWindow<ItemCollectionEditor>("Item Collection Save Data");
    }

    private void OnEnable()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "savedata", FileName);
        RefreshItemDatabase();
        LoadFromDisk();
    }

    private void RefreshItemDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/ScriptableObjects/Items" });
        _allItems = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null)
            .OrderBy(i => i.Key)
            .ToList();
    }

    private void LoadFromDisk()
    {
        _saveData = new ItemCollectionSaveData();

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
        Debug.Log($"Item collection saved to {_savePath}");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Item Collection Save Data", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_savePath, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // --- Add item dropdown ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Add Item:", GUILayout.Width(60));

        foreach (var item in _allItems)
        {
            if (GUILayout.Button(item.Key, GUILayout.MinWidth(80)))
            {
                _saveData.Items.Add(new ItemSaveData { ItemKey = item.Key });
                SaveToDisk();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // --- Current items ---
        EditorGUILayout.LabelField($"Owned Items ({_saveData.Items.Count})", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = _saveData.Items.Count - 1; i >= 0; i--)
        {
            var entry = _saveData.Items[i];
            var so = _allItems.Find(x => x.Key == entry.ItemKey);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Icon
            if (so != null && so.Icon != null)
            {
                var rect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24));
                GUI.DrawTexture(rect, so.Icon.texture, ScaleMode.ScaleToFit);
            }

            // Key & display name
            string label = so != null
                ? $"{so.Key}  ({so.DisplayName} — {so.Rarity} {so.SlotType})"
                : $"{entry.ItemKey}  (MISSING SO)";
            EditorGUILayout.LabelField(label);

            // Equipped info
            if (!string.IsNullOrEmpty(entry.EquippedHeroKey))
            {
                EditorGUILayout.LabelField($"[{entry.EquippedSlot} on {entry.EquippedHeroKey}]",
                    EditorStyles.miniLabel, GUILayout.Width(160));

                if (GUILayout.Button("Unequip", GUILayout.Width(60)))
                {
                    entry.EquippedSlot = null;
                    entry.EquippedHeroKey = null;
                    SaveToDisk();
                }
            }

            // Remove
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _saveData.Items.RemoveAt(i);
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

        if (GUILayout.Button("Clear All Items"))
        {
            if (EditorUtility.DisplayDialog("Clear Items",
                    "Remove all items from save data?", "Yes", "Cancel"))
            {
                _saveData.Items.Clear();
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
