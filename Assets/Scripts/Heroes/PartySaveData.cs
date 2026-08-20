using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class PartySaveData : IWriteable
    {
        /// <summary>Per-hero XP, one entry per hero that has been in a party.</summary>
        public List<HeroSaveData> Heroes = new List<HeroSaveData>();

        /// <summary>
        /// Save keys of every hero the player owns, in acquisition order. Read and written through
        /// <see cref="HeroRoster"/>, which migrates saves written before this field existed by
        /// treating the <see cref="Heroes"/> list as the owned roster. Kept separate from
        /// <see cref="Heroes"/> because owning a hero and having earned XP with them are different
        /// facts — a freshly recruited hero owns no XP entry yet.
        /// </summary>
        public List<string> OwnedHeroKeys = new List<string>();

        public string GetFileName()
        {
            return "Party";
        }
    }
}
