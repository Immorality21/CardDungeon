using System;
using System.Collections.Generic;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Owns the persistent meta-progression currencies (Gold, Essence) and permanent
    /// magic upgrades (per magic key) plus purchased extra magic slots. Persists
    /// immediately on every change, so awards survive party death even though
    /// dungeon/run saves are wiped.
    /// </summary>
    public class MetaProgressManager : SingletonBehaviour<MetaProgressManager>
    {
        // --- Magic upgrade tuning ---
        public const int PowerPerUpgradeLevel = 2;
        public const int MaxMagicUpgradeLevel = 5;
        private const int BaseMagicUpgradeCost = 15;
        private const int MagicUpgradeCostIncrement = 15;

        // --- Slot upgrade tuning ---
        public const int MaxBonusSlots = 2;
        private const int BaseSlotUpgradeCost = 40;
        private const int SlotUpgradeCostIncrement = 40;

        // --- Award tuning ---
        public const int GoldPerLevelCleared = 25;
        public const int EssencePerLevelCleared = 5;
        public const int GoldPerLevelOnDeath = 10;

        private FileHandler _fileHandler;
        private MetaProgressSaveData _saveData;

        /// <summary>Raised whenever gold, essence, a magic upgrade, or slot count changes.</summary>
        public event Action OnChanged;

        public int Gold => _saveData.Gold;
        public int Essence => _saveData.Essence;
        public int BonusSlots => _saveData.BonusSlots;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            Load();
        }

        // --- Pure helpers (no state / disk) so economy math is unit-testable ---

        /// <summary>Flat power added to Damage/Heal effects for a magic at the given upgrade level.</summary>
        public static int MagicPowerBonusForLevel(int level)
        {
            if (level <= 0)
            {
                return 0;
            }
            return level * PowerPerUpgradeLevel;
        }

        /// <summary>Essence cost to go from currentLevel to currentLevel + 1.</summary>
        public static int MagicUpgradeCostForNextLevel(int currentLevel)
        {
            if (currentLevel < 0)
            {
                currentLevel = 0;
            }
            return BaseMagicUpgradeCost + (currentLevel * MagicUpgradeCostIncrement);
        }

        /// <summary>Essence cost to buy the next extra magic slot (from currentBonus to currentBonus + 1).</summary>
        public static int SlotUpgradeCostForNext(int currentBonus)
        {
            if (currentBonus < 0)
            {
                currentBonus = 0;
            }
            return BaseSlotUpgradeCost + (currentBonus * SlotUpgradeCostIncrement);
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

        // Gold earned from kills during the current level. Shown per combat, but only banked
        // into persistent Gold on level-clear — a wipe forfeits it (keeps the meta-economy honest).
        private int _pendingRunGold;
        public int PendingRunGold => _pendingRunGold;

        /// <summary>Accumulate kill-gold for the current level (not persisted until level-clear).</summary>
        public void AddPendingGold(int amount)
        {
            if (amount > 0)
            {
                _pendingRunGold += amount;
            }
        }

        /// <summary>Forfeit the current level's accumulated kill-gold (party death / new level).</summary>
        public void DiscardPendingGold()
        {
            _pendingRunGold = 0;
        }

        /// <summary>Reward for clearing a dungeon level (exit room cleared): banks the level's
        /// accumulated kill-gold plus the flat level-clear bonus.</summary>
        public void AwardLevelClear()
        {
            _saveData.Gold += GoldPerLevelCleared + _pendingRunGold;
            _saveData.Essence += EssencePerLevelCleared;
            _pendingRunGold = 0;
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

        // --- Magic upgrades (per magic key) ---

        public int GetMagicUpgradeLevel(string magicKey)
        {
            if (string.IsNullOrEmpty(magicKey))
            {
                return 0;
            }

            foreach (var entry in _saveData.MagicUpgrades)
            {
                if (entry.MagicKey == magicKey)
                {
                    return entry.Level;
                }
            }
            return 0;
        }

        /// <summary>Flat power bonus applied to this magic's Damage/Heal effects.</summary>
        public int GetMagicPowerBonus(string magicKey)
        {
            return MagicPowerBonusForLevel(GetMagicUpgradeLevel(magicKey));
        }

        /// <summary>Essence cost of the next upgrade, or 0 if already at max level.</summary>
        public int GetMagicUpgradeCost(string magicKey)
        {
            int level = GetMagicUpgradeLevel(magicKey);
            if (level >= MaxMagicUpgradeLevel)
            {
                return 0;
            }
            return MagicUpgradeCostForNextLevel(level);
        }

        public bool CanUpgradeMagic(string magicKey)
        {
            if (string.IsNullOrEmpty(magicKey))
            {
                return false;
            }

            int level = GetMagicUpgradeLevel(magicKey);
            if (level >= MaxMagicUpgradeLevel)
            {
                return false;
            }
            return _saveData.Essence >= MagicUpgradeCostForNextLevel(level);
        }

        /// <summary>Spends Essence to raise a magic's upgrade level by one. Returns false if unaffordable or maxed.</summary>
        public bool TryUpgradeMagic(string magicKey)
        {
            if (!CanUpgradeMagic(magicKey))
            {
                return false;
            }

            int level = GetMagicUpgradeLevel(magicKey);
            int cost = MagicUpgradeCostForNextLevel(level);
            _saveData.Essence -= cost;

            var entry = _saveData.MagicUpgrades.Find(e => e.MagicKey == magicKey);
            if (entry == null)
            {
                entry = new MagicUpgradeEntry { MagicKey = magicKey, Level = 0 };
                _saveData.MagicUpgrades.Add(entry);
            }
            entry.Level += 1;

            Save();
            OnChanged?.Invoke();
            return true;
        }

        // --- Combo upgrades (per combo key) ---
        // Reuse the magic upgrade curves so combos and magic share one progression feel.

        public int GetComboUpgradeLevel(string comboKey)
        {
            if (string.IsNullOrEmpty(comboKey))
            {
                return 0;
            }

            foreach (var entry in _saveData.ComboUpgrades)
            {
                if (entry.ComboKey == comboKey)
                {
                    return entry.Level;
                }
            }
            return 0;
        }

        /// <summary>Flat power bonus applied to this combo's Damage/Heal bonus effects.</summary>
        public int GetComboPowerBonus(string comboKey)
        {
            return MagicPowerBonusForLevel(GetComboUpgradeLevel(comboKey));
        }

        /// <summary>Essence cost of the next combo upgrade, or 0 if already at max level.</summary>
        public int GetComboUpgradeCost(string comboKey)
        {
            int level = GetComboUpgradeLevel(comboKey);
            if (level >= MaxMagicUpgradeLevel)
            {
                return 0;
            }
            return MagicUpgradeCostForNextLevel(level);
        }

        public bool CanUpgradeCombo(string comboKey)
        {
            if (string.IsNullOrEmpty(comboKey))
            {
                return false;
            }

            int level = GetComboUpgradeLevel(comboKey);
            if (level >= MaxMagicUpgradeLevel)
            {
                return false;
            }
            return _saveData.Essence >= MagicUpgradeCostForNextLevel(level);
        }

        /// <summary>Spends Essence to raise a combo's upgrade level by one. Returns false if unaffordable or maxed.</summary>
        public bool TryUpgradeCombo(string comboKey)
        {
            if (!CanUpgradeCombo(comboKey))
            {
                return false;
            }

            int level = GetComboUpgradeLevel(comboKey);
            int cost = MagicUpgradeCostForNextLevel(level);
            _saveData.Essence -= cost;

            var entry = _saveData.ComboUpgrades.Find(e => e.ComboKey == comboKey);
            if (entry == null)
            {
                entry = new ComboUpgradeEntry { ComboKey = comboKey, Level = 0 };
                _saveData.ComboUpgrades.Add(entry);
            }
            entry.Level += 1;

            Save();
            OnChanged?.Invoke();
            return true;
        }

        // --- Discovery (permanent; survives death) ---

        public bool IsMagicDiscovered(string magicKey)
        {
            return !string.IsNullOrEmpty(magicKey) && _saveData.DiscoveredMagicKeys.Contains(magicKey);
        }

        /// <summary>Records a magic as discovered (first drawn). Idempotent; persists immediately.</summary>
        public void MarkMagicDiscovered(string magicKey)
        {
            if (string.IsNullOrEmpty(magicKey) || _saveData.DiscoveredMagicKeys.Contains(magicKey))
            {
                return;
            }
            _saveData.DiscoveredMagicKeys.Add(magicKey);
            Save();
            OnChanged?.Invoke();
        }

        public bool IsComboDiscovered(string comboKey)
        {
            return !string.IsNullOrEmpty(comboKey) && _saveData.DiscoveredComboKeys.Contains(comboKey);
        }

        /// <summary>Records a combo as discovered (first triggered). Idempotent; persists immediately.</summary>
        public void MarkComboDiscovered(string comboKey)
        {
            if (string.IsNullOrEmpty(comboKey) || _saveData.DiscoveredComboKeys.Contains(comboKey))
            {
                return;
            }
            _saveData.DiscoveredComboKeys.Add(comboKey);
            Save();
            OnChanged?.Invoke();
        }

        // --- Magic slot upgrades ---

        /// <summary>Extra magic slots purchased on top of the base slot count.</summary>
        public int GetBonusSlotCount()
        {
            return Mathf.Clamp(_saveData.BonusSlots, 0, MaxBonusSlots);
        }

        /// <summary>Essence cost of the next extra slot, or 0 if already at max.</summary>
        public int GetSlotUpgradeCost()
        {
            if (_saveData.BonusSlots >= MaxBonusSlots)
            {
                return 0;
            }
            return SlotUpgradeCostForNext(_saveData.BonusSlots);
        }

        public bool CanUpgradeSlots()
        {
            return _saveData.BonusSlots < MaxBonusSlots &&
                   _saveData.Essence >= SlotUpgradeCostForNext(_saveData.BonusSlots);
        }

        /// <summary>Spends Essence to buy one extra magic slot. Returns false if unaffordable or maxed.</summary>
        public bool TryUpgradeSlots()
        {
            if (!CanUpgradeSlots())
            {
                return false;
            }

            _saveData.Essence -= SlotUpgradeCostForNext(_saveData.BonusSlots);
            _saveData.BonusSlots += 1;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        // --- Merchant gear stock (item keys) ---

        /// <summary>The merchant's current gear stock (item keys). Never null.</summary>
        // --- Tavern stock (hero save keys) ------------------------------------

        public List<string> GetTavernStock()
        {
            return _saveData.TavernStock ?? (_saveData.TavernStock = new List<string>());
        }

        public void SetTavernStock(List<string> heroKeys)
        {
            _saveData.TavernStock = heroKeys ?? new List<string>();
            Save();
        }

        public void RemoveFromTavernStock(string heroKey)
        {
            if (_saveData.TavernStock == null || string.IsNullOrEmpty(heroKey))
            {
                return;
            }
            if (_saveData.TavernStock.Remove(heroKey))
            {
                Save();
            }
        }

        public List<string> GetShopStock()
        {
            return _saveData.ShopStock ?? (_saveData.ShopStock = new List<string>());
        }

        /// <summary>Replace the whole gear stock (a restock) and persist.</summary>
        public void SetShopStock(List<string> itemKeys)
        {
            _saveData.ShopStock = itemKeys ?? new List<string>();
            Save();
        }

        /// <summary>Remove one item from the stock after it's bought, and persist.</summary>
        public void RemoveFromShopStock(string itemKey)
        {
            if (_saveData.ShopStock != null && _saveData.ShopStock.Remove(itemKey))
            {
                Save();
            }
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
