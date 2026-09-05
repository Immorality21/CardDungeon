namespace Assets.Scripts.Hub
{
    /// <summary>
    /// What a building opens when the player clicks it. One member per hub screen, so a lot is
    /// wired to a service by an enum rather than by a string that could be misspelled into a lot
    /// that opens nothing.
    ///
    /// <para><b>The campaign map is deliberately not a member.</b> The story is the way *out* of
    /// town, not a service the town provides, and a building must never be able to lock the player
    /// out of running — the rule <c>CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay</c>
    /// encodes and <c>docs/plans/HUB.md</c> §7 open question 4 settled. The road is a fixed element
    /// of the hub view, not a lot.</para>
    ///
    /// <para>Serialized by ordinal into <c>BuildingSO</c> assets — append only.</para>
    /// </summary>
    public enum HubService
    {
        /// <summary>The campfire: who marches out, and buying the next party slot.</summary>
        Party = 0,

        Merchant = 1,
        Forge = 2,

        /// <summary>Gear, spells, consumables and materials — the player's own bag.</summary>
        Inventory = 3,

        Bestiary = 4,
        SphereGrid = 5
    }
}
