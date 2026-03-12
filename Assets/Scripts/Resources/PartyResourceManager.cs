using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class PartyResourceManager : SingletonBehaviour<PartyResourceManager>
    {
        private const int DEFAULT_HEALING_POTION_MAX = 2;
        private const int HEALING_POTION_AMOUNT = 5;

        private FileHandler _fileHandler;
        private ResourceMaxSaveData _maxData;
        private Dictionary<PartyResourceType, int> _current = new Dictionary<PartyResourceType, int>();

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            LoadMaximums();
        }

        private void Update()
        {
            // To test
            if (Input.GetKeyDown(KeyCode.H))
            {
                var hero = DungeonManager.Instance.Party.Heroes.OrderBy(x => x.Stats.Health).First();

                UseHealingPotion(hero);
            }
        }

        /// <summary>
        /// Returns the current amount of a resource.
        /// </summary>
        public int GetCurrent(PartyResourceType type)
        {
            if (_current.TryGetValue(type, out int value))
            {
                return value;
            }
            return 0;
        }

        /// <summary>
        /// Returns the global maximum for a resource.
        /// </summary>
        public int GetMax(PartyResourceType type)
        {
            foreach (var entry in _maxData.Entries)
            {
                if (entry.ResourceType == type)
                {
                    return entry.MaxAmount;
                }
            }
            return GetDefaultMax(type);
        }

        /// <summary>
        /// Sets the global maximum for a resource and persists it.
        /// </summary>
        public void SetMax(PartyResourceType type, int max)
        {
            foreach (var entry in _maxData.Entries)
            {
                if (entry.ResourceType == type)
                {
                    entry.MaxAmount = max;
                    _fileHandler.Save(_maxData);
                    return;
                }
            }

            _maxData.Entries.Add(new ResourceMaxEntry
            {
                ResourceType = type,
                MaxAmount = max
            });
            _fileHandler.Save(_maxData);
        }

        /// <summary>
        /// Replenishes all resources to their maximums. Call when entering a new dungeon.
        /// </summary>
        public void ReplenishAll()
        {
            foreach (PartyResourceType type in Enum.GetValues(typeof(PartyResourceType)))
            {
                _current[type] = GetMax(type);
            }
        }

        /// <summary>
        /// Restores current resource amounts from dungeon save data.
        /// </summary>
        public void RestoreFromSave(List<ResourceSaveData> saved)
        {
            _current.Clear();

            if (saved == null)
            {
                return;
            }

            foreach (var entry in saved)
            {
                _current[entry.ResourceType] = entry.Current;
            }
        }

        /// <summary>
        /// Builds a list of current resource states for saving in dungeon data.
        /// </summary>
        public List<ResourceSaveData> GetSaveData()
        {
            var list = new List<ResourceSaveData>();
            foreach (var kvp in _current)
            {
                list.Add(new ResourceSaveData
                {
                    ResourceType = kvp.Key,
                    Current = kvp.Value
                });
            }
            return list;
        }

        /// <summary>
        /// Uses a healing potion on the target hero. Returns true if successful.
        /// </summary>
        public bool UseHealingPotion(Hero target)
        {
            if (GetCurrent(PartyResourceType.HealingPotion) <= 0)
            {
                return false;
            }

            int effectiveMax = target.GetEffectiveMaxHealth();
            if (target.Stats.Health >= effectiveMax)
            {
                return false;
            }

            _current[PartyResourceType.HealingPotion]--;
            target.Stats.Health = Mathf.Min(target.Stats.Health + HEALING_POTION_AMOUNT, effectiveMax);

            Debug.Log($"Used healing potion on {target.HeroKey}. HP: {target.Stats.Health}/{effectiveMax}");
            return true;
        }

        private void LoadMaximums()
        {
            _maxData = _fileHandler.Load<ResourceMaxSaveData>();

            // Ensure defaults exist for all resource types
            foreach (PartyResourceType type in Enum.GetValues(typeof(PartyResourceType)))
            {
                bool found = false;
                foreach (var entry in _maxData.Entries)
                {
                    if (entry.ResourceType == type)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _maxData.Entries.Add(new ResourceMaxEntry
                    {
                        ResourceType = type,
                        MaxAmount = GetDefaultMax(type)
                    });
                }
            }

            _fileHandler.Save(_maxData);
        }

        private int GetDefaultMax(PartyResourceType type)
        {
            switch (type)
            {
                case PartyResourceType.HealingPotion:
                    return DEFAULT_HEALING_POTION_MAX;
                default:
                    return 0;
            }
        }
    }
}
