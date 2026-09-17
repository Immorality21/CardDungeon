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
                // Asset-level checks (level-curve shape) want every hero, not just the party.
                AllHeroes = heroes,
                // Everyone the player can end up fielding, recruits included. Only the frontier
                // sweep reads this: recruiting is a gold purchase, so it is part of the width axis.
                Roster = roster.Count > 0 && roster[0] != null && roster[0].Heroes.Count > 0
                    ? new List<HeroSO>(roster[0].Heroes)
                    : heroes,
                Enemies = FindAll<EnemySO>(),
                Runs = FindAll<RunDefinitionSO>(),
                Campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath),
                Magic = FindAll<MagicSO>(),
                Combos = FindAll<MagicComboSO>(),
                Items = items,
                // Reached through the room pools by the run curves; collected here as well so the
                // analyzer can name an event asset that no room offers at all.
                RoomEvents = FindAll<Assets.Scripts.Rooms.Events.RoomEventSO>(),
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
        ///
        /// <para><b>The weakest one, deliberately.</b> The belt is the <i>free</i> top-up every run
        /// starts with, so it is the basic potion - the better tiers are loot and shop stock the
        /// player had to go and get. This used to take the first match in catalog order, which
        /// silently became "whichever healing item happens to be listed first": the moment a Greater
        /// Healing Potion was authored, the model sized the free belt off the rare tier and reported
        /// a sustain pool the player does not have. Lowest <c>ItemLevel</c>, then lowest rarity, then
        /// the smallest heal on a nominal bar, so the answer is stable however the catalog is
        /// ordered.</para>
        /// </summary>
        private static ItemSO FindHealingPotion(List<ItemSO> items)
        {
            const int NominalBar = 26;

            ItemSO best = null;
            foreach (var item in items)
            {
                if (item == null || item.Category != ItemCategory.Consumable)
                {
                    continue;
                }
                // Percent-only potions are legal and are the point of the scaling half, so this
                // must not go on testing ConsumableAmount alone or it would skip them entirely.
                if (item.ConsumableEffect != ConsumableEffectType.RestoreHealth
                    || (item.ConsumableAmount <= 0 && item.ConsumablePercent <= 0f))
                {
                    continue;
                }

                if (best == null || IsWeaker(item, best, NominalBar))
                {
                    best = item;
                }
            }
            return best;
        }

        private static bool IsWeaker(ItemSO candidate, ItemSO incumbent, int nominalBar)
        {
            if (candidate.ItemLevel != incumbent.ItemLevel)
            {
                return candidate.ItemLevel < incumbent.ItemLevel;
            }
            if (candidate.Rarity != incumbent.Rarity)
            {
                return candidate.Rarity < incumbent.Rarity;
            }
            return candidate.HealAmountFor(nominalBar) < incumbent.HealAmountFor(nominalBar);
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
