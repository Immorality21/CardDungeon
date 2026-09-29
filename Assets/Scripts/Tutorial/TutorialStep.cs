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

        /// <summary>The loop is closed. The town is unlocked and the road is pointed at once.</summary>
        TakeTheRoad = 4,
    }

    /// <summary>Which screen the tutorial is being asked about — the same step says different things on each.</summary>
    public enum TutorialScreen
    {
        Other = 0,
        Town = 1,

        /// <summary>The lot panel of the building the tutorial is guiding to.</summary>
        GuideLot = 2,

        Grid = 3,
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
    }
}
