namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// How a campaign node's prerequisites combine.
    /// </summary>
    public enum CampaignUnlockMode
    {
        /// <summary>Every prerequisite must be completed. The default - a straight chain.</summary>
        All = 0,

        /// <summary>Any one prerequisite is enough. This is what lets two branches rejoin.</summary>
        Any = 1
    }
}
