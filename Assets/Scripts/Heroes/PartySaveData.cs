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

        /// <summary>
        /// Save keys of the heroes the player has chosen to <em>field</em> - the subset of
        /// <see cref="OwnedHeroKeys"/> that actually enters the dungeon, in the order they were
        /// picked (index 0 is the leader). Owning a hero and fielding them are different facts now
        /// that party width has a cost: even-split XP means a fourth hero quarters everyone's
        /// progress, so benching one has to be possible.
        ///
        /// An empty list means "not chosen yet" rather than "field nobody" - <see cref="HeroRoster"/>
        /// falls back to the owned roster clamped to the party cap, which is also how a save written
        /// before selection existed migrates.
        /// </summary>
        public List<string> SelectedHeroKeys = new List<string>();

        public string GetFileName()
        {
            return "Party";
        }
    }
}
