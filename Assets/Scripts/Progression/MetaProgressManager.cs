using System;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Owns the persistent meta-progression currencies (Gold, Essence) and
    /// permanent card upgrade levels. Persists immediately on every change, so
    /// awards survive party death even though dungeon/run saves are wiped.
    /// </summary>
    public class MetaProgressManager : SingletonBehaviour<MetaProgressManager>
    {
        // --- Card upgrade tuning ---
        public const int PowerPerUpgradeLevel = 2;
        public const int MaxCardUpgradeLevel = 5;
        private const int BaseCardUpgradeCost = 15;
        private const int CardUpgradeCostIncrement = 15;

        // --- Award tuning ---
        public const int GoldPerLevelCleared = 25;
        public const int EssencePerLevelCleared = 5;
        public const int GoldPerLevelOnDeath = 10;

        private FileHandler _fileHandler;
        private MetaProgressSaveData _saveData;

        /// <summary>Raised whenever gold, essence, or a card upgrade changes.</summary>
        public event Action OnChanged;

        public int Gold => _saveData.Gold;
        public int Essence => _saveData.Essence;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            Load();
        }

        // --- Pure helpers (no state / disk) so economy math is unit-testable ---

        /// <summary>Flat power added to Damage/Heal effects for a card at the given upgrade level.</summary>
        public static int CardPowerBonusForLevel(int level)
        {
            if (level <= 0)
            {
                return 0;
            }
            return level * PowerPerUpgradeLevel;
        }

        /// <summary>Essence cost to go from currentLevel to currentLevel + 1.</summary>
        public static int CardUpgradeCostForNextLevel(int currentLevel)
        {
            if (currentLevel < 0)
            {
                currentLevel = 0;
            }
            return BaseCardUpgradeCost + (currentLevel * CardUpgradeCostIncrement);
        }

        // --- Currency ---

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            _saveData.Gold += amount;
            Save();
            OnChanged?.Invoke();
        }

        public void AddEssence(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            _saveData.Essence += amount;
            Save();
            OnChanged?.Invoke();
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || _saveData.Gold < amount)
            {
                return false;
            }
            _saveData.Gold -= amount;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public bool TrySpendEssence(int amount)
        {
            if (amount <= 0 || _saveData.Essence < amount)
            {
                return false;
            }
            _saveData.Essence -= amount;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        // --- Awards (called from the run chokepoints) ---

        /// <summary>Reward for clearing a dungeon level (exit room cleared).</summary>
        public void AwardLevelClear()
        {
            _saveData.Gold += GoldPerLevelCleared;
            _saveData.Essence += EssencePerLevelCleared;
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Consolation reward on party death, scaled by how far the run reached.
        /// Turns a wipe from "lost everything" into permanent progress.
        /// </summary>
        public void AwardRunProgressOnDeath(int levelIndexReached)
        {
            int levelsReached = Mathf.Max(0, levelIndexReached) + 1;
            _saveData.Gold += GoldPerLevelOnDeath * levelsReached;
            Save();
            OnChanged?.Invoke();
        }

        // --- Card upgrades (per card key) ---

        public int GetCardUpgradeLevel(string cardKey)
        {
            if (string.IsNullOrEmpty(cardKey))
            {
                return 0;
            }

            foreach (var entry in _saveData.CardUpgrades)
            {
                if (entry.CardKey == cardKey)
                {
                    return entry.Level;
                }
            }
            return 0;
        }

        /// <summary>Flat power bonus applied to this card's Damage/Heal effects.</summary>
        public int GetCardPowerBonus(string cardKey)
        {
            return CardPowerBonusForLevel(GetCardUpgradeLevel(cardKey));
        }

        /// <summary>Essence cost of the next upgrade, or 0 if already at max level.</summary>
        public int GetCardUpgradeCost(string cardKey)
        {
            int level = GetCardUpgradeLevel(cardKey);
            if (level >= MaxCardUpgradeLevel)
            {
                return 0;
            }
            return CardUpgradeCostForNextLevel(level);
        }

        public bool CanUpgradeCard(string cardKey)
        {
            if (string.IsNullOrEmpty(cardKey))
            {
                return false;
            }

            int level = GetCardUpgradeLevel(cardKey);
            if (level >= MaxCardUpgradeLevel)
            {
                return false;
            }
            return _saveData.Essence >= CardUpgradeCostForNextLevel(level);
        }

        /// <summary>Spends Essence to raise a card's upgrade level by one. Returns false if unaffordable or maxed.</summary>
        public bool TryUpgradeCard(string cardKey)
        {
            if (!CanUpgradeCard(cardKey))
            {
                return false;
            }

            int level = GetCardUpgradeLevel(cardKey);
            int cost = CardUpgradeCostForNextLevel(level);
            _saveData.Essence -= cost;

            var entry = _saveData.CardUpgrades.Find(e => e.CardKey == cardKey);
            if (entry == null)
            {
                entry = new CardUpgradeEntry { CardKey = cardKey, Level = 0 };
                _saveData.CardUpgrades.Add(entry);
            }
            entry.Level += 1;

            Save();
            OnChanged?.Invoke();
            return true;
        }

        // --- Persistence ---

        public void Save()
        {
            _fileHandler.Save(_saveData);
        }

        public void Load()
        {
            _saveData = _fileHandler.Load<MetaProgressSaveData>();
        }
    }
}
