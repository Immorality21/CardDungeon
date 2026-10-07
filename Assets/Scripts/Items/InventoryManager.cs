using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Events;
using Assets.Scripts.IO;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Items
{
    public class InventoryManager : SingletonBehaviour<InventoryManager>
    {
        [SerializeField]
        [Tooltip("Optional scene-wired item list. The Resources ItemCatalog is merged in on top, so " +
                 "the manager resolves every item even when auto-created in a scene with no wiring (the hub).")]
        private List<ItemSO> _allItems;

        // Key -> ItemSO lookup, built once from _allItems plus the Resources ItemCatalog.
        private Dictionary<string, ItemSO> _itemsByKey;

        private FileHandler _fileHandler;
        private ItemCollectionSaveData _saveData;
        private Dictionary<string, Dictionary<SlotType, ItemSaveData>> _equipped =
            new Dictionary<string, Dictionary<SlotType, ItemSaveData>>();

        private bool _deferSaves;

        /// <summary>
        /// What the level currently being played has spent, per consumable key. Health is not the
        /// only level-scoped resource - the potion belt is the other half of the pool the balance
        /// model's attrition curve divides by - so this is saved into the dungeon file and reconciled
        /// on resume. Without it, quitting to the menu handed the potions back.
        ///
        /// <para>A ledger of deltas rather than a snapshot of quantities: the hub is reachable while a
        /// run is paused, so restoring absolute counts would undo a merchant purchase. See
        /// <see cref="ConsumableSpend"/>.</para>
        /// </summary>
        private List<ConsumableSpend> _dungeonConsumption = new List<ConsumableSpend>();

        // Load in Awake (like MetaProgressManager / PartyResourceManager) so an auto-created
        // instance — e.g. the hub MenuScene has no wired Managers prefab — is immediately usable.
        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            Load();
            if (ReferenceEquals(Instance, this))
            {
                // Items that grow with use count off the game's events; combat never calls in here.
                GameEvents.Subscribe<EnemyDefeated>(CountKill);
                GameEvents.Subscribe<CombatFinished>(CountVictory);
            }
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<EnemyDefeated>(CountKill);
            GameEvents.Unsubscribe<CombatFinished>(CountVictory);
        }

        private void CountKill(EnemyDefeated kill)
        {
            if (string.IsNullOrEmpty(kill?.KillerHeroKey))
            {
                return;
            }
            AddToWornCounters(kill.KillerHeroKey, ItemCounterKind.Kills, 1);
            if (kill.IsBoss)
            {
                AddToWornCounters(kill.KillerHeroKey, ItemCounterKind.BossKills, 1);
            }
        }

        private void CountVictory(CombatFinished fight)
        {
            if (fight == null || !fight.Won)
            {
                return;
            }
            foreach (var heroKey in fight.HeroKeys)
            {
                AddToWornCounters(heroKey, ItemCounterKind.Victories, 1);
            }
        }

        /// <summary>
        /// Adds to <paramref name="kind"/> on every item <paramref name="heroKey"/> wears that counts it.
        /// Respects deferred saves like any other change, so a level's counting is kept on the stairs
        /// and forfeited with the level (<see cref="Load"/> reverts the entries).
        /// </summary>
        public void AddToWornCounters(string heroKey, ItemCounterKind kind, int amount)
        {
            if (string.IsNullOrEmpty(heroKey) || amount == 0 || !_equipped.TryGetValue(heroKey, out var slots))
            {
                return;
            }
            bool changed = false;
            foreach (var entry in slots.Values)
            {
                if (ItemGrowth.Counts(GetItemSO(entry.ItemKey), kind))
                {
                    ItemGrowth.Add(entry, ItemGrowth.KeyOf(kind), amount);
                    changed = true;
                }
            }
            if (changed && !_deferSaves)
            {
                Save();
            }
        }

        public void SetDeferSaves(bool defer)
        {
            _deferSaves = defer;
        }

        public void CommitInventory()
        {
            Save();
        }

        /// <summary>
        /// The inventory as it stands on disk - during a level, where saves are deferred, that is
        /// the bags as the level found them. Read by the level-clear summary before it commits.
        /// </summary>
        public List<ItemSaveData> GetCommittedItems()
        {
            var onDisk = _fileHandler.Load<ItemCollectionSaveData>();
            return onDisk != null && onDisk.Items != null ? onDisk.Items : new List<ItemSaveData>();
        }

        /// <summary>
        /// Adds <paramref name="count"/> of an item. Stacking items (consumables, materials) pile
        /// into one entry; equipment becomes <paramref name="count"/> separate ones.
        /// </summary>
        /// <param name="source">Where it came from, told to the game's event stream
        /// (<see cref="ItemAcquired"/>). Required, so every new way of handing out items has to say.</param>
        public void AddItem(ItemSO item, int count, EconomySource source)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            InventoryOperations.AddItem(_saveData.Items, item, count);

            if (!_deferSaves)
            {
                Save();
            }
            GameEvents.Publish(new ItemAcquired { Item = item, Quantity = count, Source = source });
        }

        /// <summary>Adds a rolled drop-table award (see <see cref="LootRoller.Roll"/>).</summary>
        public void AddItem(LootAward award, EconomySource source)
        {
            if (!award.IsEmpty)
            {
                AddItem(award.Item, award.Quantity, source);
            }
        }

        /// <summary>
        /// Removes one <b>un-equipped</b> equipment entry with this key (a "bag" copy), never an
        /// item a hero has equipped. Used by selling so a hero can't be stripped by selling a
        /// duplicate. Of several bag copies the least grown goes (<see cref="ItemGrowth"/>), so selling
        /// "a spare" never sells the one that has been counting. Returns true if a bag copy was found
        /// and removed.
        /// </summary>
        public bool RemoveBagEquipment(string itemKey, ItemRemovalReason reason)
        {
            int index = -1;
            int leastGrown = int.MaxValue;
            for (int i = 0; i < _saveData.Items.Count; i++)
            {
                var entry = _saveData.Items[i];
                if (entry.ItemKey != itemKey || !string.IsNullOrEmpty(entry.EquippedSlot)
                    || !IsCategory(entry, ItemCategory.Equipment))
                {
                    continue;
                }
                int growth = 0;
                if (entry.Counters != null)
                {
                    foreach (var counter in entry.Counters)
                    {
                        growth += counter != null ? counter.Value : 0;
                    }
                }
                if (growth < leastGrown)
                {
                    leastGrown = growth;
                    index = i;
                }
            }

            if (index < 0)
            {
                return false;
            }

            _saveData.Items.RemoveAt(index);
            if (!_deferSaves)
            {
                Save();
            }
            GameEvents.Publish(new ItemRemoved { Item = GetItemSO(itemKey), Quantity = 1, Reason = reason });
            return true;
        }

        public List<ItemSaveData> GetItems()
        {
            return _saveData.Items;
        }

        public List<ItemSaveData> GetBagItems()
        {
            return _saveData.Items.Where(i => string.IsNullOrEmpty(i.EquippedSlot)).ToList();
        }

        /// <summary>Un-equipped equipment items (the equipment "bag").</summary>
        public List<ItemSaveData> GetBagEquipment()
        {
            return _saveData.Items
                .Where(i => string.IsNullOrEmpty(i.EquippedSlot) && IsCategory(i, ItemCategory.Equipment))
                .ToList();
        }

        /// <summary>
        /// Every piece of equipment the party owns, worn or not - what the equipment screen offers
        /// for a slot, so a sword on the Paladin can be handed to the Warrior without a detour.
        /// </summary>
        public List<ItemSaveData> GetAllEquipment()
        {
            return _saveData.Items
                .Where(i => IsCategory(i, ItemCategory.Equipment))
                .ToList();
        }

        /// <summary>All consumable stacks the party is carrying.</summary>
        public List<ItemSaveData> GetConsumables()
        {
            return _saveData.Items.Where(i => IsCategory(i, ItemCategory.Consumable)).ToList();
        }

        /// <summary>Every material stack the player has brought home.</summary>
        public List<ItemSaveData> GetMaterials()
        {
            return _saveData.Items.Where(i => IsCategory(i, ItemCategory.Material)).ToList();
        }

        /// <summary>Total carried quantity of one material across stacks (0 if none).</summary>
        public int GetMaterialQuantity(string itemKey)
        {
            return InventoryOperations.GetMaterialQuantity(_saveData.Items, itemKey, GetItemSO);
        }

        /// <summary>Whether a whole material price is covered by what is carried. Never spends.</summary>
        public bool CanAfford(IList<MaterialCost> cost)
        {
            return InventoryOperations.CanAfford(_saveData.Items, cost, GetItemSO);
        }

        /// <summary>
        /// Pays a material price, all or nothing. Saves immediately unless a dungeon has deferred
        /// saves — spending happens at the hub, where nothing is deferred.
        /// </summary>
        public bool SpendMaterials(IList<MaterialCost> cost)
        {
            if (!InventoryOperations.SpendMaterials(_saveData.Items, cost, GetItemSO))
            {
                return false;
            }

            if (!_deferSaves)
            {
                Save();
            }
            foreach (var price in cost)
            {
                if (price != null && price.IsValid)
                {
                    GameEvents.Publish(new ItemRemoved
                    {
                        Item = price.Material, Quantity = price.Amount, Reason = ItemRemovalReason.Spent
                    });
                }
            }
            return true;
        }

        /// <summary>Total carried quantity of a consumable across stacks (0 if none).</summary>
        public int GetConsumableQuantity(string itemKey)
        {
            return InventoryOperations.GetConsumableQuantity(_saveData.Items, itemKey, GetItemSO);
        }

        /// <summary>Whether the party carries at least one usable consumable.</summary>
        public bool HasAnyConsumable()
        {
            return _saveData.Items.Any(i => IsCategory(i, ItemCategory.Consumable) && i.Quantity > 0);
        }

        /// <summary>
        /// Spends one unit of a consumable, removing the stack when it hits zero. Returns false if
        /// none are carried. Respects deferred saves (spending happens in-dungeon).
        /// </summary>
        public bool TryConsume(string itemKey, ItemRemovalReason reason = ItemRemovalReason.Used)
        {
            if (!InventoryOperations.TryConsume(_saveData.Items, itemKey, GetItemSO))
            {
                return false;
            }

            InventoryOperations.RecordSpend(_dungeonConsumption, itemKey);

            if (!_deferSaves)
            {
                Save();
            }
            GameEvents.Publish(new ItemRemoved { Item = GetItemSO(itemKey), Quantity = 1, Reason = reason });
            return true;
        }

        /// <summary>
        /// What this dungeon has spent so far, for <c>DungeonSaveData.ConsumablesSpent</c>.
        /// </summary>
        public List<ConsumableSpend> GetDungeonConsumption()
        {
            return InventoryOperations.MergeSpends(_dungeonConsumption, null);
        }

        /// <summary>Starts a fresh level's ledger. Called from <c>DungeonManager.SpawnFreshDungeon</c>.</summary>
        public void BeginDungeonConsumption()
        {
            _dungeonConsumption = new List<ConsumableSpend>();
        }

        /// <summary>
        /// Brings this dungeon's consumption up to what its save file recorded, spending only the
        /// difference. Idempotent by construction, which it must be: whether the resumed inventory
        /// still has the potions depends on whether this singleton was destroyed with the scene, and
        /// neither caller should have to know. See <see cref="InventoryOperations.SpendShortfall"/>.
        /// </summary>
        public void ReconcileDungeonConsumption(List<ConsumableSpend> saved)
        {
            var shortfall = InventoryOperations.SpendShortfall(_dungeonConsumption, saved);

            foreach (var entry in shortfall)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    // Best effort: an item the player no longer carries was already spent as far as
                    // the level is concerned, so a miss here must not stall the reconcile.
                    InventoryOperations.TryConsume(_saveData.Items, entry.ItemKey, GetItemSO);
                }
            }

            // Catching the bags up to what the save already recorded is not a new spend: no event.
            _dungeonConsumption = InventoryOperations.MergeSpends(_dungeonConsumption, saved);
        }

        /// <summary>
        /// Refills a consumable up to <paramref name="cap"/> total quantity (the "belt" top-up run
        /// on fresh dungeon entry). Never reduces an existing surplus.
        /// </summary>
        public void TopUpConsumableToCap(ItemSO item, int cap)
        {
            int before = item != null ? GetConsumableQuantity(item.Key) : 0;
            InventoryOperations.TopUpConsumableToCap(_saveData.Items, item, cap, GetItemSO);
            int added = item != null ? GetConsumableQuantity(item.Key) - before : 0;
            if (added <= 0)
            {
                return;
            }

            if (!_deferSaves)
            {
                Save();
            }
            GameEvents.Publish(new ItemAcquired { Item = item, Quantity = added, Source = EconomySource.BeltRefill });
        }

        private bool IsCategory(ItemSaveData item, ItemCategory category)
        {
            var so = GetItemSO(item.ItemKey);
            return so != null && so.Category == category;
        }

        public ItemSO GetItemSO(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            EnsureItemLookup();
            _itemsByKey.TryGetValue(key, out var so);
            return so;
        }

        /// <summary>
        /// Builds the key→ItemSO lookup from the scene-wired list plus the Resources catalog.
        /// Idempotent; the catalog lets the hub (no wired manager) still resolve every item.
        /// </summary>
        private void EnsureItemLookup()
        {
            if (_itemsByKey != null)
            {
                return;
            }

            _itemsByKey = new Dictionary<string, ItemSO>();

            if (_allItems != null)
            {
                foreach (var so in _allItems)
                {
                    AddToLookup(so);
                }
            }

            // Qualify UnityEngine.Resources — the project has its own Assets.Scripts.Resources namespace.
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            if (catalog != null)
            {
                foreach (var so in catalog.Items)
                {
                    AddToLookup(so);
                }
            }
        }

        private void AddToLookup(ItemSO so)
        {
            if (so != null && !string.IsNullOrEmpty(so.Key) && !_itemsByKey.ContainsKey(so.Key))
            {
                _itemsByKey[so.Key] = so;
            }
        }

        /// <summary>Every item known to the catalog (used by the hub inventory to list acquirable gear).</summary>
        public IEnumerable<ItemSO> AllItems()
        {
            EnsureItemLookup();
            return _itemsByKey.Values;
        }

        public ItemSaveData GetEquipped(SlotType slot, string heroKey)
        {
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                slots.TryGetValue(slot, out var item);
                return item;
            }
            return null;
        }

        public void Equip(ItemSaveData item, SlotType slot, string heroKey)
        {
            var so = GetItemSO(item.ItemKey);
            if (so == null || so.Category != ItemCategory.Equipment || so.SlotType != slot)
            {
                return;
            }

            // Unequip existing item in that slot for this hero
            Unequip(slot, heroKey);

            item.EquippedSlot = slot.ToString();
            item.EquippedHeroKey = heroKey;

            if (!_equipped.ContainsKey(heroKey))
            {
                _equipped[heroKey] = new Dictionary<SlotType, ItemSaveData>();
            }
            _equipped[heroKey][slot] = item;

            if (!_deferSaves)
            {
                Save();
            }
            GameEvents.Publish(new ItemEquipChanged { Item = so, HeroKey = heroKey, Equipped = true });
        }

        public void Unequip(SlotType slot, string heroKey)
        {
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                if (slots.TryGetValue(slot, out var existing))
                {
                    existing.EquippedSlot = null;
                    existing.EquippedHeroKey = null;
                    slots.Remove(slot);
                    if (!_deferSaves)
                    {
                        Save();
                    }
                    GameEvents.Publish(new ItemEquipChanged
                    {
                        Item = GetItemSO(existing.ItemKey), HeroKey = heroKey, Equipped = false
                    });
                }
            }
        }

        public Dictionary<StatType, float> ComputeRawBonuses(string heroKey)
        {
            return ComputeBonuses(heroKey, BonusType.Raw);
        }

        public Dictionary<StatType, float> ComputePercentageBonuses(string heroKey)
        {
            return ComputeBonuses(heroKey, BonusType.Percentage);
        }

        /// <summary>Every equipment ScriptableObject a hero currently has equipped.</summary>
        public List<ItemSO> GetEquippedItems(string heroKey)
        {
            var equipped = new List<ItemSO>();
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                foreach (var kvp in slots)
                {
                    var so = GetItemSO(kvp.Value.ItemKey);
                    if (so != null)
                    {
                        equipped.Add(so);
                    }
                }
            }
            return equipped;
        }

        /// <summary>Elemental resistance a hero's equipped gear grants, summed per damage type.</summary>
        public List<Combat.Resistance> ComputeResistances(string heroKey)
        {
            return InventoryOperations.ComputeResistances(GetEquippedItems(heroKey));
        }

        /// <summary>The hero's gear bonuses of one type, each item with the milestones its own entry
        /// has reached (<see cref="ItemGrowth"/>).</summary>
        private Dictionary<StatType, float> ComputeBonuses(string heroKey, BonusType bonusType)
        {
            var perItem = new List<IEnumerable<ItemBonus>>();
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                foreach (var entry in slots.Values)
                {
                    perItem.Add(ItemGrowth.BonusesOf(GetItemSO(entry.ItemKey), entry));
                }
            }
            return InventoryOperations.SumBonuses(perItem, bonusType);
        }

        public void Save()
        {
            _fileHandler.Save(_saveData);
        }

        public void Load()
        {
            // The ledger describes a level this freshly-read collection has not been through.
            _dungeonConsumption = new List<ConsumableSpend>();

            _saveData = _fileHandler.Load<ItemCollectionSaveData>();
            InventoryOperations.NormalizeQuantities(_saveData.Items);
            RebuildEquippedCache();
        }


        private void RebuildEquippedCache()
        {
            _equipped.Clear();
            foreach (var item in _saveData.Items)
            {
                if (!string.IsNullOrEmpty(item.EquippedSlot) &&
                    Enum.TryParse<SlotType>(item.EquippedSlot, out var slot))
                {
                    var heroKey = item.EquippedHeroKey ?? "";
                    if (!_equipped.ContainsKey(heroKey))
                    {
                        _equipped[heroKey] = new Dictionary<SlotType, ItemSaveData>();
                    }
                    _equipped[heroKey][slot] = item;
                }
            }
        }
    }
}
