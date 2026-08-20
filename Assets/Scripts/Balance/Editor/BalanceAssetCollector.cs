using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// Finds every asset the analyzer needs and packages it into a <see cref="BalanceInput"/>. This is
    /// the only place AssetDatabase is used, which is what keeps the model itself testable and lets
    /// EditMode tests analyze hand-built asset sets instead of the whole project.
    /// </summary>
    public static class BalanceAssetCollector
    {
        public const string RulesAssetPath = "Assets/ScriptableObjects/BalanceRules.asset";

        public static BalanceInput Collect(BalanceRulesSO rules, bool runSimulation, bool includeSaveAudit)
        {
            var heroes = FindAll<HeroSO>();
            var items = FindAll<ItemSO>();

            // Measure against the *starting* party. Every hero asset in the project is not the
            // party: heroes are acquired by rescue or recruitment, so judging level 1 against a
            // fully-recruited roster would understate every danger and attrition number.
            var roster = FindAll<Assets.Scripts.Heroes.PartyRosterSO>();
            var startingParty = roster.Count > 0 && roster[0] != null
                ? roster[0].StartingLineup()
                : heroes;
            if (startingParty.Count == 0)
            {
                startingParty = heroes;
            }

            var input = new BalanceInput
            {
                Rules = rules,
                Heroes = startingParty,
                Enemies = FindAll<EnemySO>(),
                Runs = FindAll<RunDefinitionSO>(),
                Magic = FindAll<MagicSO>(),
                Combos = FindAll<MagicComboSO>(),
                Items = items,
                RunSimulation = runSimulation,
                IncludeSaveAudit = includeSaveAudit
            };

            input.HealingPotion = FindHealingPotion(items);

            // Saves store keys, not references; resolve them the same way the game does.
            input.ResolveHero = key =>
            {
                foreach (var hero in heroes)
                {
                    if (hero != null && hero.SaveKey == key)
                    {
                        return hero;
                    }
                }
                return null;
            };

            input.ResolveItem = key =>
            {
                foreach (var item in items)
                {
                    if (item != null && item.Key == key)
                    {
                        return item;
                    }
                }
                return null;
            };

            return input;
        }

        /// <summary>Every asset of a type in the project, name-sorted so tables are stable between runs.</summary>
        public static List<T> FindAll<T>() where T : ScriptableObject
        {
            var results = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    results.Add(asset);
                }
            }

            results.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return results;
        }

        /// <summary>
        /// The consumable the dungeon tops the belt up with. DungeonManager holds the reference in the
        /// scene, so it is identified here by category and effect instead of by scene wiring.
        /// </summary>
        private static ItemSO FindHealingPotion(List<ItemSO> items)
        {
            foreach (var item in items)
            {
                if (item == null || item.Category != ItemCategory.Consumable)
                {
                    continue;
                }
                if (item.ConsumableEffect == ConsumableEffectType.RestoreHealth && item.ConsumableAmount > 0)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>Loads the rules asset, or an unsaved default so the window works before one exists.</summary>
        public static BalanceRulesSO LoadOrCreateRules(bool createAsset)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BalanceRulesSO>(RulesAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var found = FindAll<BalanceRulesSO>();
            if (found.Count > 0)
            {
                return found[0];
            }

            if (!createAsset)
            {
                return BalanceRulesSO.CreateDefault();
            }

            var created = ScriptableObject.CreateInstance<BalanceRulesSO>();
            var directory = System.IO.Path.GetDirectoryName(RulesAssetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(created, RulesAssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
