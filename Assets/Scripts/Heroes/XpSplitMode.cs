namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// How a run's XP is divided at the campfire. **A redistribution, never a multiplier** — every
    /// mode hands out exactly the same total, so no campfire level can make the party earn more.
    /// That is what keeps the balance model's XP accounting true whatever the player picks, and it
    /// is the same rule every hub building level follows: access and control, never a raw stat.
    ///
    /// <para><see cref="Mentor"/> and <see cref="CatchUp"/> are the <i>same</i> mechanic — one hero
    /// takes a double share, the rest split what is left — and differ only in who is chosen. That is
    /// deliberate: one code path, one number to tune, and a player who understands either
    /// understands both.</para>
    ///
    /// <para>Serialized by ordinal into the save — append only.</para>
    /// </summary>
    public enum XpSplitMode
    {
        /// <summary>Everyone fielded takes an equal share. The default, and the only mode a level-1
        /// campfire offers.</summary>
        Even = 0,

        /// <summary>A hero the player nominates takes a double share. This is what lets a wide party
        /// still drive one hero deep enough to reach a branch tip, which is the whole depth-versus-
        /// breadth question the sphere grid asks.</summary>
        Mentor = 1,

        /// <summary>The hero with the least banked XP takes the double share, chosen fresh on every
        /// award. The mirror of <see cref="Mentor"/>: it keeps a party level instead of sharpening
        /// one of them, which is what a newly rescued hero needs.</summary>
        CatchUp = 2
    }
}
