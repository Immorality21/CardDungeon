using System;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// One hero's current health inside the level being played. Dungeon-scoped, unlike
    /// <c>HeroSaveData</c>: health is a level-scoped resource (<c>Party.HealAll()</c> only fires on
    /// entering a fresh dungeon), so it belongs to the dungeon save that is deleted on level clear
    /// and on death, not to the persistent party save.
    ///
    /// <para>Keyed by <c>HeroSO.SaveKey</c> like every other save record, so a hero can be renamed
    /// without stranding the value, and a hero who is not in the file at all - a captive freed after
    /// the save was written - simply resumes at full health.</para>
    /// </summary>
    [Serializable]
    public class HeroHealthSaveData
    {
        public string HeroKey;
        public int Health;
    }
}
