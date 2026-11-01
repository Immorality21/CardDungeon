using System;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// One hero's current health inside a run. Unlike <c>HeroSaveData</c> it does not outlive the
    /// run: <c>Party.HealAll()</c> fires only on a run's first floor, so this belongs to the dungeon
    /// save while a floor is played and to the run save between floors (<c>HealthCarry</c>), not to
    /// the persistent party save.
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
