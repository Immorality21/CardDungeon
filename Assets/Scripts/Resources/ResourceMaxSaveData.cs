using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Resources
{
    [Serializable]
    public class ResourceMaxEntry
    {
        public PartyResourceType ResourceType;
        public int MaxAmount;
    }

    [Serializable]
    public class ResourceMaxSaveData : IWriteable
    {
        public List<ResourceMaxEntry> Entries = new List<ResourceMaxEntry>();

        public string GetFileName()
        {
            return "ResourceMaximums";
        }
    }
}
