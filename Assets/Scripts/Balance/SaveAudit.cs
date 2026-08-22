using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One hero as the save file actually has them, with the level that XP total buys.</summary>
    public class SavedHero
    {
        public string HeroKey = "";
        public HeroSO Definition;
        public int Xp;
        public int Level;
        public int MaxDefinedLevel;
        public List<ItemSO> Gear = new List<ItemSO>();

        public bool AtMaxDefinedLevel => Level >= MaxDefinedLevel;

        /// <summary>XP still needed for the next level, or -1 when no further level is configured.</summary>
        public int XpToNextLevel = -1;
    }

    /// <summary>A magic the player has actually invested Essence in.</summary>
    public class SavedUpgrade
    {
        public string Key = "";
        public int Level;
        public int PowerBonus;
    }

    /// <summary>
    /// Reads the live save files and reconstructs what the player really has, so the level analysis
    /// can be re-run against a real party instead of a designed baseline. This answers the question
    /// the designed baseline cannot: not "is level 2 fair" but "would *this* save survive level 2".
    ///
    /// Save files live in <c>Application.persistentDataPath/savedata</c> and are read through the
    /// game's own <see cref="FileHandler"/>, so the shapes never drift from what the game writes.
    /// </summary>
    public class SaveAudit
    {
        public bool HasPartySave;
        public bool HasMetaSave;
        public bool HasRunSave;
        public string SaveDirectory = "";

        public List<SavedHero> Heroes = new List<SavedHero>();

        public int Gold;
        public int Essence;
        public int BonusMagicSlots;
        public List<SavedUpgrade> MagicUpgrades = new List<SavedUpgrade>();
        public List<SavedUpgrade> ComboUpgrades = new List<SavedUpgrade>();
        public int DiscoveredMagicCount;
        public int DiscoveredComboCount;
        public int ShopStockCount;

        public string RunKey = "";
        public int CurrentLevelIndex;
        public int EquippedMagicCount;

        public int PotionsCarried;
        public int PotionCap = PartyResourceManager.DEFAULT_HEALING_POTION_MAX;

        /// <summary>The real party, ready to feed straight back into the level analysis.</summary>
        public PartyBaseline Party;

        // ---- Economy pacing, derived from MetaProgressManager's own numbers ----
        public int EssencePerClear = MetaProgressManager.EssencePerLevelCleared;
        public int GoldPerClear = MetaProgressManager.GoldPerLevelCleared;
        public int EssenceToMaxOneMagic;
        public float ClearsToFirstUpgrade;
        public float ClearsToMaxOneMagic;

        /// <summary>
        /// Loads and interprets the save set. <paramref name="resolveHero"/> and
        /// <paramref name="resolveItem"/> map saved keys back to assets — the editor layer supplies
        /// them from the project, which keeps this class free of AssetDatabase.
        /// </summary>
        public static SaveAudit Load(
            Func<string, HeroSO> resolveHero,
            Func<string, ItemSO> resolveItem,
            ItemSO healingPotion)
        {
            var audit = new SaveAudit
            {
                SaveDirectory = $"{Application.persistentDataPath}/savedata"
            };

            audit.EssenceToMaxOneMagic = TotalEssenceToMaxOneMagic();
            audit.ClearsToFirstUpgrade = audit.EssencePerClear > 0
                ? (float)MetaProgressManager.MagicUpgradeCostForNextLevel(0) / audit.EssencePerClear
                : 0f;
            audit.ClearsToMaxOneMagic = audit.EssencePerClear > 0
                ? (float)audit.EssenceToMaxOneMagic / audit.EssencePerClear
                : 0f;

            var fileHandler = new FileHandler();

            audit.HasPartySave = File.Exists($"{audit.SaveDirectory}/Party.json");
            audit.HasMetaSave = File.Exists($"{audit.SaveDirectory}/Meta.json");
            audit.HasRunSave = File.Exists($"{audit.SaveDirectory}/Run.json");

            var meta = fileHandler.Load<MetaProgressSaveData>();
            audit.Gold = meta.Gold;
            audit.Essence = meta.Essence;
            audit.BonusMagicSlots = meta.BonusSlots;
            audit.ShopStockCount = meta.ShopStock != null ? meta.ShopStock.Count : 0;
            audit.DiscoveredMagicCount = meta.DiscoveredMagicKeys != null ? meta.DiscoveredMagicKeys.Count : 0;
            audit.DiscoveredComboCount = meta.DiscoveredComboKeys != null ? meta.DiscoveredComboKeys.Count : 0;

            if (meta.MagicUpgrades != null)
            {
                foreach (var entry in meta.MagicUpgrades)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    audit.MagicUpgrades.Add(new SavedUpgrade
                    {
                        Key = entry.MagicKey,
                        Level = entry.Level,
                        PowerBonus = MetaProgressManager.MagicPowerBonusForLevel(entry.Level)
                    });
                }
            }

            if (meta.ComboUpgrades != null)
            {
                foreach (var entry in meta.ComboUpgrades)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    audit.ComboUpgrades.Add(new SavedUpgrade
                    {
                        Key = entry.ComboKey,
                        Level = entry.Level,
                        PowerBonus = MetaProgressManager.MagicPowerBonusForLevel(entry.Level)
                    });
                }
            }

            var run = fileHandler.Load<RunSaveData>();
            audit.RunKey = run.RunKey ?? "";
            audit.CurrentLevelIndex = run.CurrentLevelIndex;
            audit.EquippedMagicCount = run.EquippedMagic != null ? run.EquippedMagic.Count : 0;

            var resourceMax = fileHandler.Load<ResourceMaxSaveData>();
            if (resourceMax.Entries != null)
            {
                foreach (var entry in resourceMax.Entries)
                {
                    if (entry != null && entry.ResourceType == PartyResourceType.HealingPotion)
                    {
                        audit.PotionCap = entry.MaxAmount;
                    }
                }
            }

            var items = fileHandler.Load<ItemCollectionSaveData>();
            InventoryOperations.NormalizeQuantities(items.Items);
            var gearByHero = CollectGear(items, resolveItem, healingPotion, audit);

            var partySave = fileHandler.Load<PartySaveData>();
            var heroDefinitions = new List<HeroSO>();

            if (partySave.Heroes != null)
            {
                foreach (var saved in partySave.Heroes)
                {
                    if (saved == null || string.IsNullOrEmpty(saved.HeroKey))
                    {
                        continue;
                    }

                    var definition = resolveHero != null ? resolveHero(saved.HeroKey) : null;
                    int level = HeroStatCalculator.LevelForXp(definition, saved.CurrentXp);
                    int maxLevel = HeroStatCalculator.MaxDefinedLevel(definition);
                    int nextXp = HeroStatCalculator.XpToReachLevel(definition, level + 1);

                    var hero = new SavedHero
                    {
                        HeroKey = saved.HeroKey,
                        Definition = definition,
                        Xp = saved.CurrentXp,
                        Level = level,
                        MaxDefinedLevel = maxLevel,
                        XpToNextLevel = nextXp >= 0 ? Mathf.Max(0, nextXp - saved.CurrentXp) : -1
                    };

                    if (gearByHero.TryGetValue(saved.HeroKey, out var gear))
                    {
                        hero.Gear = gear;
                    }

                    audit.Heroes.Add(hero);
                    if (definition != null)
                    {
                        heroDefinitions.Add(definition);
                    }
                }
            }

            audit.Party = BuildRealParty(audit, heroDefinitions, gearByHero, healingPotion);
            return audit;
        }

        /// <summary>
        /// Builds a <see cref="PartyBaseline"/> from the save: each hero at the level their saved XP
        /// buys, wearing what they actually have equipped. Heroes can sit at different levels — XP is
        /// split across whoever was fielded at the time (<c>Party.DistributeXp</c>), and a hero
        /// recruited or benched partway through banked a different amount — so each is levelled
        /// individually rather than through the shared level parameter.
        /// </summary>
        private static PartyBaseline BuildRealParty(
            SaveAudit audit,
            List<HeroSO> heroDefinitions,
            Dictionary<string, List<ItemSO>> gearByHero,
            ItemSO healingPotion)
        {
            // The healing pool is sized from the belt *cap*, not from what the inventory happens to
            // hold: DungeonManager tops the belt up to the cap on every fresh dungeon entry
            // (TopUpConsumableToCap), so the party starts each level with a full belt regardless of
            // how many potions were left when the game was saved. Using the carried count instead
            // makes the same save read as clearable or unclearable depending on when it was written.
            // PotionsCarried is still reported on its own for the mid-level picture.
            var party = new PartyBaseline
            {
                SourceLabel = "Save file",
                PotionCount = Mathf.Max(0, audit.PotionCap),
                PotionItem = healingPotion,
                PotionHealAmount = healingPotion != null ? healingPotion.ConsumableAmount : 0
            };

            foreach (var saved in audit.Heroes)
            {
                if (saved.Definition == null)
                {
                    continue;
                }

                var gear = saved.Gear ?? new List<ItemSO>();
                var baseStats = HeroStatCalculator.BaseStatsAtLevel(saved.Definition, saved.Level);
                var effective = HeroStatCalculator.WithGear(baseStats, gear);

                var hero = new HeroBaseline
                {
                    Definition = saved.Definition,
                    Level = saved.Level,
                    MaxDefinedLevel = saved.MaxDefinedLevel,
                    SavedXp = saved.Xp,
                    Effective = effective,
                    Gear = gear
                };

                hero.Unit = new SimUnit
                {
                    DisplayName = hero.Name,
                    HeroKey = saved.HeroKey,
                    IsHero = true,
                    Stats = new Rooms.Stats(effective),
                    Effective = effective.Clone(),
                    // Honour the hero's chosen attack stat here too; this used to hardcode Strength,
                    // so an audited Scout swung off a stat it does not invest in.
                    AttackStat = saved.Definition != null
                        ? saved.Definition.ResolvedAttackStat
                        : StatType.Strength,
                    EffectiveAttackPower = effective[saved.Definition != null
                        ? saved.Definition.ResolvedAttackStat
                        : StatType.Strength],
                    Resistances = InventoryOperations.ComputeResistances(gear)
                };

                party.Heroes.Add(hero);
            }

            return party;
        }

        private static Dictionary<string, List<ItemSO>> CollectGear(
            ItemCollectionSaveData items,
            Func<string, ItemSO> resolveItem,
            ItemSO healingPotion,
            SaveAudit audit)
        {
            var byHero = new Dictionary<string, List<ItemSO>>();
            if (items.Items == null)
            {
                return byHero;
            }

            string potionKey = healingPotion != null ? healingPotion.Key : null;

            foreach (var entry in items.Items)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemKey))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(potionKey) && entry.ItemKey == potionKey)
                {
                    audit.PotionsCarried += Mathf.Max(1, entry.Quantity);
                }

                if (string.IsNullOrEmpty(entry.EquippedHeroKey) || string.IsNullOrEmpty(entry.EquippedSlot))
                {
                    continue;
                }

                var item = resolveItem != null ? resolveItem(entry.ItemKey) : null;
                if (item == null)
                {
                    continue;
                }

                if (!byHero.ContainsKey(entry.EquippedHeroKey))
                {
                    byHero[entry.EquippedHeroKey] = new List<ItemSO>();
                }
                byHero[entry.EquippedHeroKey].Add(item);
            }

            return byHero;
        }

        /// <summary>Total Essence needed to take one magic from level 0 to the cap.</summary>
        public static int TotalEssenceToMaxOneMagic()
        {
            int total = 0;
            for (int level = 0; level < MetaProgressManager.MaxMagicUpgradeLevel; level++)
            {
                total += MetaProgressManager.MagicUpgradeCostForNextLevel(level);
            }
            return total;
        }
    }
}
