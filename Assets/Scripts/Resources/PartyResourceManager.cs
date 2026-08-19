using System.Collections.Generic;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;

namespace Assets.Scripts.Resources
{
    /// <summary>
    /// Owns the persistent per-resource carry caps (e.g. the healing-potion "belt" size the
    /// Merchant enlarges). Live consumable quantities now live in the item inventory
    /// (<c>InventoryManager</c>); this class only tracks how many of each the party may carry.
    /// </summary>
    public class PartyResourceManager : SingletonBehaviour<PartyResourceManager>
    {
        // Public so the balance model can size the party's healing pool from the real default
        // rather than guessing at it.
        public const int DEFAULT_HEALING_POTION_MAX = 2;

        private FileHandler _fileHandler;
        private ResourceMaxSaveData _maxData;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            LoadMaximums();
        }

        /// <summary>
        /// Returns the carry cap ("belt" size) for a resource.
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
        /// Sets the carry cap for a resource and persists it.
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

        private void LoadMaximums()
        {
            _maxData = _fileHandler.Load<ResourceMaxSaveData>();

            // Ensure defaults exist for all resource types
            foreach (PartyResourceType type in System.Enum.GetValues(typeof(PartyResourceType)))
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
