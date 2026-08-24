namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// What a campaign node looks like to a particular save.
    /// </summary>
    public enum CampaignNodeStatus
    {
        /// <summary>Secret and still locked - drawn nowhere, so the player does not know it exists.</summary>
        Hidden = 0,

        /// <summary>Prerequisites unmet. Drawn, so the player can see what the next step leads to.</summary>
        Locked = 1,

        /// <summary>Unlocked and never cleared - startable.</summary>
        Available = 2,

        /// <summary>The run currently in <c>Run.json</c>. Continued, not restarted.</summary>
        InProgress = 3,

        /// <summary>Cleared at least once. Startable again only when the run is repeatable.</summary>
        Completed = 4
    }
}
