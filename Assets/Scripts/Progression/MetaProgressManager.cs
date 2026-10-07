using System;
using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Owns the persistent meta-progression currencies (Gold, Essence) and permanent
    /// combo upgrades (per combo key). Persists immediately on every change, so awards
    /// survive party death even though dungeon/run saves are wiped. (Extra magic slots
    /// moved to the sphere grid - per-hero MagicSlot nodes bought with XP - and so, on
    /// 2026-10-06, did growing an ability: the Forge no longer upgrades abilities.)
    /// </summary>
    public class MetaProgressManager : SingletonBehaviour<MetaProgressManager>
    {
        // --- Combo upgrade tuning ---
        public const int PowerPerUpgradeLevel = 2;

        /// <summary>
        /// The highest a combo can ever be upgraded, at a fully raised Forge. Nothing
        /// past this is buyable at any hub state, so the <i>endgame</i> power ceiling is this
        /// constant and the Forge's level decides only how soon it is reached.
        /// </summary>
        public const int MaxComboUpgradeLevel = 5;

        private const int BaseComboUpgradeCost = 15;
        private const int ComboUpgradeCostIncrement = 15;

        /// <summary>
        /// Upgrade ceiling per Forge building level: no Forge buys nothing, and levels 1-3 open 1,
        /// 3 and 5. Indexed by level, so element 0 is "no Forge standing".
        /// </summary>
        private static readonly int[] CeilingPerForgeLevel = { 0, 1, 3, 5 };

        // --- Award tuning ---
        public const int GoldPerLevelCleared = 25;
        /// <summary>Essence per floor of a <b>revisit</b>, before its reward multiplier. A first clear
        /// pays none: Essence is the revisit currency (docs/plans/HUB.md §3c).</summary>
        public const int EssencePerRevisitLevel = 12;
        public const int GoldPerLevelOnDeath = 10;

        private FileHandler _fileHandler;
        private MetaProgressSaveData _saveData;

        public int Gold => _saveData.Gold;
        public int Essence => _saveData.Essence;

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
            int refund = 40 * n + 40 * n * (n - 1) / 2;
            _saveData.Essence += refund;
            _saveData.BonusSlots = 0;
            Save();
            Publish(Currency.Essence, refund, EconomySource.Migration);
        }

        // --- Pure helpers (no state / disk) so economy math is unit-testable ---

        /// <summary>Flat power added to a combo's Damage/Heal bonus effects at the given upgrade level.</summary>
        public static int ComboPowerBonusForLevel(int level)
        {
            if (level <= 0)
            {
                return 0;
            }
            return level * PowerPerUpgradeLevel;
        }

        /// <summary>Essence cost to go from currentLevel to currentLevel + 1.</summary>
        public static int ComboUpgradeCostForNextLevel(int currentLevel)
        {
            if (currentLevel < 0)
            {
                currentLevel = 0;
            }
            return BaseComboUpgradeCost + (currentLevel * ComboUpgradeCostIncrement);
        }

        /// <summary>
        /// How far a combo can be upgraded at a Forge of this building level — the first
        /// thing a hub building level ever <i>granted</i> (<c>docs/plans/HUB.md</c> §7 phase 6).
        ///
        /// <para><b>It gates buying, not what has been bought.</b> A level already paid for keeps
        /// its power forever; raising the ceiling only puts more rungs on sale. That is what makes
        /// the dial safe to author — no hub change can ever reach back into a combo the player has
        /// already raised — and it is why <see cref="GetComboPowerBonus"/> does not consult it.</para>
        ///
        /// <para><b>It is access, not power.</b> A fully raised Forge lands on
        /// <see cref="MaxComboUpgradeLevel"/>, exactly where a flat constant used to sit, so the
        /// power available at the end of the game is unchanged and only the <i>ramp</i> is gated.
        /// That keeps a building a hard precondition on the investment frontier rather than a second
        /// route from gold to power, which is the rule <c>docs/plans/HUB.md</c> sets for every
        /// building level.</para>
        /// </summary>
        public static int UpgradeCeilingForForgeLevel(int forgeLevel)
        {
            if (forgeLevel < 0)
            {
                forgeLevel = 0;
            }
            if (forgeLevel >= CeilingPerForgeLevel.Length)
            {
                forgeLevel = CeilingPerForgeLevel.Length - 1;
            }
            return CeilingPerForgeLevel[forgeLevel];
        }

        /// <summary>
        /// The upgrade ceiling this save is playing under, read from the Forge lot it has built.
        ///
        /// <para>Falls back to <see cref="MaxComboUpgradeLevel"/> when there is no authored town at
        /// all, matching <c>HubState.LevelOf</c>'s rule: a scene with no hub asset degrades to the
        /// old fixed behaviour rather than to a screen that offers nothing.</para>
        /// </summary>
        public int ComboUpgradeCeiling
        {
            get
            {
                if (Hub.HubState.Town() == null)
                {
                    return MaxComboUpgradeLevel;
                }
                return UpgradeCeilingForForgeLevel(Hub.HubState.LevelOf(Hub.HubService.Forge));
            }
        }

        // --- Currency ---
        //
        // Every change says where it came from or went (EconomySource) and is told to the game's event
        // stream as a CurrencyChanged - the one place gold and Essence move, so nothing listening (an
        // achievement, a hub quest) can miss a route.

        private static void Publish(Currency currency, int delta, EconomySource source, bool pending = false)
        {
            if (delta != 0)
            {
                GameEvents.Publish(new CurrencyChanged { Currency = currency, Delta = delta, Source = source, Pending = pending });
            }
        }

        public void AddGold(int amount, EconomySource source)
        {
            if (amount <= 0)
            {
                return;
            }
            _saveData.Gold += amount;
            Save();
            Publish(Currency.Gold, amount, source);
        }

        public void AddEssence(int amount, EconomySource source)
        {
            if (amount <= 0)
            {
                return;
            }
            _saveData.Essence += amount;
            Save();
            Publish(Currency.Essence, amount, source);
        }

        public bool TrySpendGold(int amount, EconomySource source)
        {
            if (amount <= 0 || _saveData.Gold < amount)
            {
                return false;
            }
            _saveData.Gold -= amount;
            Save();
            Publish(Currency.Gold, -amount, source);
            return true;
        }

        public bool TrySpendEssence(int amount, EconomySource source)
        {
            if (amount <= 0 || _saveData.Essence < amount)
            {
                return false;
            }
            _saveData.Essence -= amount;
            Save();
            Publish(Currency.Essence, -amount, source);
            return true;
        }

        // --- Awards (called from the run chokepoints) ---

        // Gold earned from kills during the current level. Shown per combat, but only banked
        // into persistent Gold on level-clear — a wipe forfeits it (keeps the meta-economy honest).
        private int _pendingRunGold;
        public int PendingRunGold => _pendingRunGold;

        /// <summary>Accumulate gold found on the current level - kills, caches, events - not persisted
        /// until level-clear. Told to the event stream as a <i>pending</i> change.</summary>
        public void AddPendingGold(int amount, EconomySource source)
        {
            if (amount > 0)
            {
                _pendingRunGold += amount;
                Publish(Currency.Gold, amount, source, pending: true);
            }
        }

        /// <summary>Forfeit the current level's accumulated gold (party death, leaving the dungeon).</summary>
        public void DiscardPendingGold()
        {
            int lost = _pendingRunGold;
            _pendingRunGold = 0;
            Publish(Currency.Gold, -lost, EconomySource.Forfeit, pending: true);
        }

        /// <summary>
        /// Reward for taking the stairs: banks the level's pending gold plus the flat bonus and the
        /// Essence the caller decided (a revisit scales both by its reward multiplier,
        /// docs/plans/REVISITS.md). Told to the stream as one <b>kept</b> gold change of the whole
        /// amount - the pending gold included - so a listener counting gold earned counts kept changes.
        /// </summary>
        public void AwardLevelClear(int goldBonus, int essence)
        {
            int gold = Mathf.Max(0, goldBonus) + _pendingRunGold;
            _saveData.Gold += gold;
            _saveData.Essence += Mathf.Max(0, essence);
            _pendingRunGold = 0;
            Save();
            Publish(Currency.Gold, gold, EconomySource.LevelClear);
            Publish(Currency.Essence, Mathf.Max(0, essence), EconomySource.LevelClear);
        }

        // --- Revisits ---

        /// <summary>The highest fear this save has cleared <paramref name="runKey"/> at as a revisit, or -1 if never.</summary>
        public int GetBestRevisitFear(string runKey)
        {
            var record = _saveData.RevisitRecords?.Find(r => r != null && r.RunKey == runKey);
            return record != null ? record.BestFear : -1;
        }

        /// <summary>Records a cleared revisit; keeps the best fear. Persists immediately.</summary>
        public void RecordRevisitFear(string runKey, int fear)
        {
            if (string.IsNullOrEmpty(runKey))
            {
                return;
            }
            if (_saveData.RevisitRecords == null)
            {
                _saveData.RevisitRecords = new List<RevisitRecord>();
            }

            var record = _saveData.RevisitRecords.Find(r => r != null && r.RunKey == runKey);
            if (record == null)
            {
                _saveData.RevisitRecords.Add(new RevisitRecord { RunKey = runKey, BestFear = Mathf.Max(0, fear), Clears = 1 });
            }
            else
            {
                record.BestFear = Mathf.Max(record.BestFear, fear);
                record.Clears++;
            }
            Save();
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
            Publish(Currency.Gold, GoldPerLevelOnDeath * levelsReached, EconomySource.RunDeath);
        }

        // --- Combo upgrades (per combo key) ---
        // The Forge's only upgrade since 2026-10-06: per-ability upgrades were retired when Essence
        // became the revisit currency and abilities started growing on the sphere grid instead
        // (docs/plans/HUB.md §3c).

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
            return ComboPowerBonusForLevel(GetComboUpgradeLevel(comboKey));
        }

        /// <summary>Essence cost of the next combo upgrade, or 0 when the Forge offers no rung
        /// above the current level.</summary>
        public int GetComboUpgradeCost(string comboKey)
        {
            int level = GetComboUpgradeLevel(comboKey);
            if (level >= ComboUpgradeCeiling)
            {
                return 0;
            }
            return ComboUpgradeCostForNextLevel(level);
        }

        public bool CanUpgradeCombo(string comboKey)
        {
            if (string.IsNullOrEmpty(comboKey))
            {
                return false;
            }

            int level = GetComboUpgradeLevel(comboKey);
            if (level >= ComboUpgradeCeiling)
            {
                return false;
            }
            return _saveData.Essence >= ComboUpgradeCostForNextLevel(level);
        }

        /// <summary>Spends Essence to raise a combo's upgrade level by one. Returns false if unaffordable or maxed.</summary>
        public bool TryUpgradeCombo(string comboKey)
        {
            if (!CanUpgradeCombo(comboKey))
            {
                return false;
            }

            int level = GetComboUpgradeLevel(comboKey);
            int cost = ComboUpgradeCostForNextLevel(level);
            _saveData.Essence -= cost;
            Publish(Currency.Essence, -cost, EconomySource.Forge);

            var entry = _saveData.ComboUpgrades.Find(e => e.ComboKey == comboKey);
            if (entry == null)
            {
                entry = new ComboUpgradeEntry { ComboKey = comboKey, Level = 0 };
                _saveData.ComboUpgrades.Add(entry);
            }
            entry.Level += 1;

            Save();
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
        }

        // --- Bestiary (permanent enemy knowledge; survives death) ---
        //
        // Every mutator delegates to the pure BestiaryOps and persists *only* when the record
        // actually changed. That matters: these are called from the damage path, so a hit that
        // teaches nothing new must not write Meta.json.

        /// <summary>Every enemy the player has met. Never null; the live list, so do not mutate it.</summary>
        public int BestiaryViewedCount => _saveData.BestiaryViewedCount;

        /// <summary>The bestiary was opened with <paramref name="seenCount"/> enemies recorded.</summary>
        public void MarkBestiaryViewed(int seenCount)
        {
            if (_saveData.BestiaryViewedCount == seenCount)
            {
                return;
            }
            _saveData.BestiaryViewedCount = seenCount;
            Save();
        }

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
        }

        // --- The tutorial (only its two ends; the step is derived, see TutorialOps) ---

        public bool TutorialStarted => _saveData.TutorialStarted;
        public bool TutorialFinished => _saveData.TutorialFinished;

        /// <summary>Begins the guided first hour. Called once, by a New Game. Persists immediately.</summary>
        public void StartTutorial()
        {
            if (_saveData.TutorialStarted)
            {
                return;
            }
            _saveData.TutorialStarted = true;
            Save();
        }

        /// <summary>Ends it for good — the loop was walked, or it was skipped. Persists immediately.</summary>
        public void FinishTutorial()
        {
            if (_saveData.TutorialFinished)
            {
                return;
            }
            _saveData.TutorialFinished = true;
            Save();
        }

        // --- Hub buildings (which lots are placed, and at what level) ---

        /// <summary>Every lot the player has placed. Never null.</summary>
        public List<BuildingProgress> GetBuildings()
        {
            return _saveData.Buildings ?? (_saveData.Buildings = new List<BuildingProgress>());
        }

        /// <summary>
        /// Records a lot as built at <paramref name="level"/>, or raises an existing entry to it.
        /// Never lowers a level - there is no un-building - and persists immediately, the same
        /// rule every other meta award follows so hub progress survives whatever happens next.
        /// Returns whether anything actually changed.
        /// </summary>
        public bool SetBuildingLevel(string buildingKey, int level)
        {
            if (string.IsNullOrEmpty(buildingKey) || level <= 0)
            {
                return false;
            }

            var buildings = GetBuildings();
            var entry = buildings.Find(b => b != null && b.Key == buildingKey);
            if (entry == null)
            {
                buildings.Add(new BuildingProgress { Key = buildingKey, Level = level });
            }
            else if (entry.Level < level)
            {
                entry.Level = level;
            }
            else
            {
                return false;
            }

            Save();
            GameEvents.Publish(new BuildingLevelChanged { BuildingKey = buildingKey, Level = level });
            return true;
        }

        // --- Party width (how many heroes can be fielded at once) ---

        /// <summary>
        /// Heroes this save can take into a dungeon at once: always <see cref="PartySlots.MaxCap"/>.
        ///
        /// <para><b>No longer a purchase</b> (2026-09-17). Width is paced by the <i>roster</i> - a
        /// hero is rescued and unlocked, never bought - and the cost of going wide is paid every run
        /// in diluted XP (<see cref="XpSplit"/>) rather than once at a shop. Kept as a method rather
        /// than inlined because the fielded cap is a rule, and a rule the whole game reads from one
        /// place is one that can change again without a hunt.</para>
        /// </summary>
        public int GetPartyCap()
        {
            return PartySlots.MaxCap;
        }

        // --- The campfire's XP split (what a campfire level grants) ---

        /// <summary>
        /// The split the player has chosen, filtered through what their campfire actually offers.
        /// Always read this rather than the raw save field — a save can name a mode the town cannot
        /// grant, and the wrong failure is a party quietly running a mode it never unlocked.
        /// </summary>
        public XpSplitMode GetXpSplitMode()
        {
            return Hub.CampfireOps.EffectiveMode(
                Hub.HubState.LevelOf(Hub.HubService.Party), _saveData.XpSplitMode);
        }

        /// <summary>The raw stored choice, for the campfire screen that is about to change it.</summary>
        public XpSplitMode GetStoredXpSplitMode()
        {
            return _saveData.XpSplitMode;
        }

        /// <summary>The <c>HeroSO.Key</c> the Mentor mode favours, or empty when nobody is named.</summary>
        public string GetXpFocusHeroKey()
        {
            return _saveData.XpFocusHeroKey ?? "";
        }

        /// <summary>
        /// Records how the party wants its XP divided. Persisted immediately, like every other hub
        /// choice, so it survives a wipe the way the rest of the meta save does.
        /// </summary>
        public void SetXpSplit(XpSplitMode mode, string focusHeroKey)
        {
            _saveData.XpSplitMode = mode;
            _saveData.XpFocusHeroKey = focusHeroKey ?? "";
            Save();
        }

        // --- Merchant gear stock (item keys) ---

        /// <summary>The merchant's current gear stock (item keys). Never null.</summary>
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
