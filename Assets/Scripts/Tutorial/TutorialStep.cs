namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Where a save is in the guided first hour. <b>Derived, never stored</b>: <see cref="TutorialOps.CurrentStep"/>
    /// reads it off the save every time (is the hall standing, has the hero spent anything), so a step
    /// can never disagree with the game it is describing and nothing needs migrating when the order
    /// changes. Only the two ends of the tutorial — started, finished — are written down.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>No tutorial: never started (a save that predates it) or finished.</summary>
        None = 0,

        /// <summary>The opening floor has not been cleared yet. A New Game is sent straight into it.</summary>
        ClearFirstFloor = 1,

        /// <summary>Home with the floor's timber, and the guide building still a foundation.</summary>
        BuildHall = 2,

        /// <summary>The hall stands; the starting hero has banked XP and bought nothing with it.</summary>
        SpendXp = 3,

        /// <summary>
        /// The first loop is closed: the town is unlocked and the road points at the gear floor (the
        /// run's second). Going down no longer finishes the tutorial - the gear step is still ahead.
        /// </summary>
        TakeTheRoad = 4,

        /// <summary>
        /// Home from the gear floor with a piece of equipment in the bag and nobody wearing anything:
        /// the town is locked to the Storehouse, and its screen to equipping it.
        /// </summary>
        EquipGear = 5,

        /// <summary>Something is equipped. The town is open; going down finishes the tutorial.</summary>
        GoOn = 6,

        /// <summary>
        /// After the gear step, back to the Hall of Progression (playtest 3 finding 2): the second floor
        /// paid both heroes XP, and nothing told the player the Paladin can spend too. The town is locked
        /// to the hall, its grid opens on the Paladin, and nothing has to be bought - leaving the grid
        /// ends the step (<c>MetaProgressSaveData.TutorialHallRevisited</c>). Comes before <see cref="GoOn"/>.
        /// </summary>
        RevisitHall = 7,
    }

    /// <summary>Which screen the tutorial is being asked about — the same step says different things on each.</summary>
    public enum TutorialScreen
    {
        Other = 0,
        Town = 1,

        /// <summary>The lot panel of the building the tutorial is guiding to.</summary>
        GuideLot = 2,

        Grid = 3,

        /// <summary>The Storehouse's inventory screen.</summary>
        Inventory = 4,

        /// <summary>The story map (run selection).</summary>
        CampaignMap = 5,
    }

    /// <summary>
    /// One line the tutorial can say. The words are authored on <see cref="TutorialSO"/>; which one is
    /// said where is <see cref="TutorialOps.CueFor"/>. Serialized by value — append, never reorder.
    /// </summary>
    public enum TutorialCue
    {
        None = 0,
        RetryFirstFloor = 1,
        BuildHallInTown = 2,
        BuildHallOnPanel = 3,
        OpenHallInTown = 4,
        EnterHallOnPanel = 5,
        PickNode = 6,
        ActivateNode = 7,
        NodeLearned = 8,
        TakeTheRoad = 9,
        FirstDeath = 10, // the death screen, the first time the party falls (not a step: any save sees it)
        OpenStorehouseInTown = 11,
        EnterStorehouseOnPanel = 12,
        EquipGear = 13,
        GearEquipped = 14,
        GoOn = 15,
        ChallengeAhead = 16, // the story map, once, when a challenge run first opens (not a step: any save sees it)
        RevisitHallInTown = 17,
        RevisitHallOnGrid = 18,
    }
}
