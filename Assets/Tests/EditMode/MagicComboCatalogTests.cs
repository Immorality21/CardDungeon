using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The scene-wired combo catalog, checked against the combo assets that actually exist. The list
    /// is hand-populated in the inspector, so the two failures here are both invisible in code: a
    /// combo dragged in twice (it lists twice in the Forge and its detector fires twice), and a
    /// combo never dragged in at all (it can never trigger and can never be upgraded). Both shipped
    /// at once - Freeze was in the prefab twice and Infection was missing.
    /// </summary>
    public class MagicComboCatalogTests
    {
        private const string CatalogPath = "Assets/Prefabs/MagicComboCatalog.prefab";

        private static IReadOnlyList<MagicComboSO> LoadCatalogCombos()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatalogPath);
            Assert.IsNotNull(prefab, $"No combo catalog prefab at {CatalogPath}.");
            var catalog = prefab.GetComponent<MagicComboCatalog>();
            Assert.IsNotNull(catalog, $"{CatalogPath} has no MagicComboCatalog component.");
            return catalog.AllCombos;
        }

        private static List<MagicComboSO> LoadEveryComboAsset()
        {
            var combos = new List<MagicComboSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:MagicComboSO"))
            {
                var combo = AssetDatabase.LoadAssetAtPath<MagicComboSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (combo != null)
                {
                    combos.Add(combo);
                }
            }
            return combos;
        }

        [Test]
        public void Catalog_HasNoEmptySlots()
        {
            var combos = LoadCatalogCombos();
            for (int i = 0; i < combos.Count; i++)
            {
                Assert.IsNotNull(combos[i],
                    $"Combo catalog slot {i} is empty; GetCombo skips nulls but the Forge grid does not.");
            }
        }

        [Test]
        public void Catalog_ListsNoComboTwice()
        {
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            foreach (var combo in LoadCatalogCombos())
            {
                if (combo != null && !seen.Add(combo.Key))
                {
                    duplicates.Add(combo.ComboName);
                }
            }
            CollectionAssert.IsEmpty(duplicates,
                $"Combo(s) {string.Join(", ", duplicates)} appear more than once in {CatalogPath}; " +
                "the Forge lists each entry, so a duplicate shows as two tiles.");
        }

        [Test]
        public void Catalog_ListsEveryComboAsset()
        {
            var listed = new HashSet<string>();
            foreach (var combo in LoadCatalogCombos())
            {
                if (combo != null)
                {
                    listed.Add(combo.Key);
                }
            }

            var missing = new List<string>();
            foreach (var combo in LoadEveryComboAsset())
            {
                if (!listed.Contains(combo.Key))
                {
                    missing.Add(combo.ComboName);
                }
            }
            CollectionAssert.IsEmpty(missing,
                $"Combo(s) {string.Join(", ", missing)} exist as assets but are not in {CatalogPath}, " +
                "so ComboDetector can never fire them and the Forge cannot upgrade them.");
        }

        [Test]
        public void Catalog_EveryComboHasAUniqueKey()
        {
            var byKey = new Dictionary<string, string>();
            var clashes = new List<string>();
            foreach (var combo in LoadEveryComboAsset())
            {
                if (byKey.TryGetValue(combo.Key, out var other))
                {
                    clashes.Add($"{combo.ComboName} shares key '{combo.Key}' with {other}");
                }
                else
                {
                    byKey[combo.Key] = combo.ComboName;
                }
            }
            CollectionAssert.IsEmpty(clashes,
                string.Join("; ", clashes) + " - keys index discovery and upgrade level in the save.");
        }
    }
}
