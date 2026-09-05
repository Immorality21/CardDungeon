namespace Assets.Scripts.Hub
{
    /// <summary>
    /// How one lot currently reads on the hub screen. <see cref="BuildingOps.StateOf"/> decides it;
    /// the view only picks a sprite, which is what keeps the town renderer free of rules.
    ///
    /// <para><see cref="Available"/> is the load-bearing one and the reason this is not a bool: a
    /// bare lot the player <i>could</i> build on is the affordance that makes a material worth
    /// wanting. Without it, an unbuilt hub is indistinguishable from an empty field and nothing on
    /// screen ever explains why the player is carrying ember iron.</para>
    /// </summary>
    public enum BuildingState
    {
        /// <summary>Not built and not yet offered — an empty lot, or nothing at all.</summary>
        Absent = 0,

        /// <summary>Not built, but the player has met whatever the hub asks before offering it.</summary>
        Available = 1,

        /// <summary>Placed, at some level ≥ 1.</summary>
        Built = 2
    }
}
