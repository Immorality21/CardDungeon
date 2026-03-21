using UnityEditor;
using UnityEngine;
using System.Linq;
using Assets.Scripts.Cards;

public class CardAssetPostprocessor : AssetPostprocessor
{
    private const string CardCollectionManagerPrefabPath = "Assets/Prefabs/CardCollectionManager.prefab";

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool cardChanged = importedAssets
            .Concat(deletedAssets)
            .Concat(movedAssets)
            .Concat(movedFromAssetPaths)
            .Any(path => path.EndsWith(".asset") &&
                         path.Contains("ScriptableObjects/Cards") &&
                         !path.Contains("Combo"));

        if (cardChanged)
        {
            UpdateCardCollectionPrefab();
        }
    }

    [MenuItem("Tools/Cards/Sync Card Collection Prefab")]
    public static void UpdateCardCollectionPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardCollectionManagerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"CardCollectionManager prefab not found at {CardCollectionManagerPrefabPath}");
            return;
        }

        var manager = prefab.GetComponent<CardCollectionManager>();
        if (manager == null)
        {
            Debug.LogWarning("CardCollectionManager component not found on prefab.");
            return;
        }

        var allCardGuids = AssetDatabase.FindAssets("t:CardSO", new[] { "Assets/ScriptableObjects/Cards" });
        var allCards = allCardGuids
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null)
            .OrderBy(card => card.Key)
            .ToList();

        var serializedObject = new SerializedObject(manager);
        var allCardsProperty = serializedObject.FindProperty("_allCards");
        allCardsProperty.ClearArray();

        for (int i = 0; i < allCards.Count; i++)
        {
            allCardsProperty.InsertArrayElementAtIndex(i);
            allCardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = allCards[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);

        Debug.Log($"CardCollectionManager prefab synced with {allCards.Count} cards.");
    }
}
