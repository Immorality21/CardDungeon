using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Owns the persistent meta-progression currencies (Gold, Essence) and permanent
    /// magic upgrades (per magic key) plus purchased party slots. Persists
    /// immediately on every change, so awards survive party death even though
    /// dungeon/run saves are wiped. (Extra magic slots moved to the sphere grid —
    /// per-hero MagicSlot nodes bought with XP.)
    /// </summary>
    public class MetaProgressManager : SingletonBehaviour<MetaProgressManager>
    {
        // --- Magic upgrade tuning ---
        public const int PowerPerUpgradeLevel = 2;
        public const int MaxMagicUpgradeLevel = 5;
        private const int BaseMagicUpgradeCost = 15;
        private const int MagicUpgradeCostIncrement = 15;

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
        public int BonusPartySlots => _saveData.BonusPartySlots;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            Load();
            RefundLegacyBonusSlots();
        }

        /// <summary>
        /// The Essence-bought global magic-slot upgrade was retired when the sphere grid took slot
        /// growth over (slots are per hero now, bought with XP as MagicSlot nodes). A save that had
        /// paid for slots gets its Essence back in full, at the historical prices — slot i cost
        /// 40 + 40*i — and the legacy counter is zeroed so a re-read never double-refunds.
        /// </summary>
        private void RefundLegacyBonusSlots()
        {
            if (_saveData.BonusSlots <= 0)
            {
                return;
            }

            int n = _saveData.BonusSlots;
            _saveData.Essence += 40 * n + 40 * n * (n - 1) / 2;
            _saveData.BonusSlots = 0;
            Save();
            OnChanged?.Invoke();
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

        /// <summary>
        /// Records a magic as discovered - i.e. some hero has learned it on their sphere grid, which
        /// is what unlocks it in the Forge. It used to mean "drawn from an enemy at least once"; with
        /// Draw gone the trigger moved to node activation, and what the <i>Bestiary</i> masks moved
        /// to <c>BestiaryEntry.ObservedSpellKeys</c> instead. Idempotent; persists immediately.
        /// </summary>
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

        // --- Bestiary (permanent enemy knowledge; survives death) ---
        //
        // Every mutator delegates to the pure BestiaryOps and persists *only* when the record
        // actually changed. That matters: these are called from the damage path, so a hit that
        // teaches nothing new must not write Meta.json.

        /// <summary>Every enemy the player has met. Never null; the live list, so do not mutate it.</summary>
        public List<BestiaryEntry> GetBestiary()
        {
            return _saveData.Bestiary ?? (_saveData.Bestiary = new List<BestiaryEntry>());
        }

        /// <summary>What is known about one enemy, or null if it has never been met.</summary>
        public BestiaryEntry GetBestiaryEntry(string enemyKey)
        {
            return BestiaryOps.Find(GetBestiary(), enemyKey);
        }

        /// <summary>Whether this enemy has ever been encountered.</summary>
        public bool IsEnemySeen(string enemyKey)
        {
            return GetBestiaryEntry(enemyKey) != null;
        }

        /// <summary>Records meeting an enemy (combat start). Idempotent.</summary>
        public void MarkEnemySeen(string enemyKey)
        {
            CommitBestiary(BestiaryOps.MarkSeen(GetBestiary(), enemyKey));
        }

        /// <summary>
        /// Records that a hit of this damage type landed on the enemy, which is what reveals its
        /// resistance to that element. Called for every typed hit; idempotent per type.
        /// </summary>
        public void MarkResistanceObserved(string enemyKey, Combat.DamageType type)
        {
            CommitBestiary(BestiaryOps.MarkDamageTypeObserved(GetBestiary(), enemyKey, type));
        }

        /// <summary>Records seeing the enemy attack, which reveals the element it swings with.</summary>
        public void MarkAttackTypeObserved(string enemyKey)
        {
            CommitBestiary(BestiaryOps.MarkAttackTypeObserved(GetBestiary(), enemyKey));
        }

        /// <summary>Adds one to this enemy's kill tally.</summary>
        public void MarkEnemyKilled(string enemyKey)
        {
            CommitBestiary(BestiaryOps.MarkKilled(GetBestiary(), enemyKey));
        }

        /// <summary>Records an item this enemy was actually seen to drop. Idempotent per item.</summary>
        public void MarkLootObserved(string enemyKey, string itemKey)
        {
            CommitBestiary(BestiaryOps.MarkLootObserved(GetBestiary(), enemyKey, itemKey));
        }

        /// <summary>Records a spell this enemy was actually seen to cast. Idempotent per spell.</summary>
        public void MarkEnemySpellObserved(string enemyKey, string magicKey)
        {
            CommitBestiary(BestiaryOps.MarkSpellObserved(GetBestiary(), enemyKey, magicKey));
        }

        private void CommitBestiary(bool changed)
        {
            if (!changed)
            {
                return;
            }
            Save();
            OnChanged?.Invoke();
        }

        // --- Run completion (which runs have been cleared to the end) ---

        /// <summary>Every run this save has cleared. Never null; the live list, so do not mutate it.</summary>
        public List<string> GetCompletedRunKeys()
        {
            return _saveData.CompletedRunKeys ?? (_saveData.CompletedRunKeys = new List<string>());
        }

        /// <summary>Whether the player has ever cleared this run's final level.</summary>
        public bool HasCompletedRun(string runKey)
        {
            return !string.IsNullOrEmpty(runKey)
                && _saveData.CompletedRunKeys != null
                && _saveData.CompletedRunKeys.Contains(runKey);
        }

        /// <summary>Records a run as completed. Idempotent; persists immediately.</summary>
        public void MarkRunCompleted(string runKey)
        {
            if (string.IsNullOrEmpty(runKey))
            {
                return;
            }
            if (_saveData.CompletedRunKeys == null)
            {
                _saveData.CompletedRunKeys = new List<string>();
            }
            if (_saveData.CompletedRunKeys.Contains(runKey))
            {
                return;
            }
            _saveData.CompletedRunKeys.Add(runKey);
            Save();
            OnChanged?.Invoke();
        }

        // --- Party slots (how many heroes can be fielded at once) ---

        /// <summary>
        /// Heroes this save can take into a dungeon at once. Starts at <see cref="PartySlots.BaseCap"/>
        /// and is bought up to <see cref="PartySlots.MaxCap"/> with Gold - party width is a
        /// progression axis, not a consequence of how many heroes you happen to have recruited.
        /// </summary>
        public int GetPartyCap()
        {
            return PartySlots.CapForBonus(_saveData.BonusPartySlots);
        }

        /// <summary>Gold cost of the next party slot, or 0 when already at the ceiling.</summary>
        public int GetPartySlotCost()
        {
            return PartySlots.CostForNext(_saveData.BonusPartySlots);
        }

        public bool CanBuyPartySlot()
        {
            int cost = GetPartySlotCost();
            return cost > 0 && _saveData.Gold >= cost;
        }

        /// <summary>Spends Gold to field one more hero. Returns false if unaffordable or maxed.</summary>
        public bool TryBuyPartySlot()
        {
            if (!CanBuyPartySlot())
            {
                return false;
            }

            _saveData.Gold -= GetPartySlotCost();
            _saveData.BonusPartySlots += 1;
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
