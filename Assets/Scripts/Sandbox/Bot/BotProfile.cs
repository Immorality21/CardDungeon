using System;
using System.Collections.Generic;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>What a player does with a room event.</summary>
    public enum BotEventHabit
    {
        /// <summary>Takes every event, whatever the odds, and the gamble over the sure thing.</summary>
        Everything = 0,

        /// <summary>Gambles only at even odds or better; sure things unless they mean a fight.</summary>
        OddsOnly = 1,

        /// <summary>Leans safe: sure things with nothing to lose, or a gamble that is nearly certain.</summary>
        Safe = 2,
    }

    /// <summary>How much of a floor a player walks.</summary>
    public enum BotRoute
    {
        /// <summary>Every room, every chest, every event the habit allows, then the stairs.</summary>
        ClearEverything = 0,

        /// <summary>Presses on until it finds the stairs and takes them. What stands in a room it
        /// passes through is still used; it makes no detour. It does not know where the stairs are.</summary>
        ToTheExit = 1,
    }

    /// <summary>One kind of player. A batch plays every profile it is given.</summary>
    [Serializable]
    public class BotProfile
    {
        public BotEventHabit Events = BotEventHabit.Everything;
        public BotRoute Route = BotRoute.ClearEverything;

        /// <summary>Share of the gold in hand spent on gear at each visit to town; the rest goes to
        /// buildings. 0.5 is the balance model's own assumption (<c>StraightPartyGearShare</c>).</summary>
        public float GearGoldShare = 0.5f;

        public string Label => Events + "/" + Route;
    }

    /// <summary>A batch, handed from the launcher to the play session as JSON.</summary>
    [Serializable]
    public class BotSettings
    {
        public List<BotProfile> Profiles = new List<BotProfile>();

        /// <summary>Whole playthroughs per profile, each from a new save.</summary>
        public int Games = 1;

        /// <summary>The first game's random seed; each later game adds one.</summary>
        public int FirstSeed = 1000;

        /// <summary>Game speed. Waits that use real time (hit pauses, fades) do not scale.</summary>
        public float TimeScale = 15f;

        /// <summary>Run asset names in the order to play them. A run still locked is passed over.</summary>
        public List<string> Runs = new List<string>();

        /// <summary>Tries at one run before the playthrough is given up as stuck on it.</summary>
        public int MaxAttemptsPerRun = 4;

        /// <summary>A run out of tries is passed over rather than ending the playthrough, so a wall in
        /// the middle of the list does not hide everything behind it.</summary>
        public bool PassOverWalls;

        /// <summary>Real seconds with nothing happening before a game is reported as stuck.</summary>
        public float StuckSeconds = 30f;

        /// <summary>Written into the report's file name.</summary>
        public string ReportName = "bot";
    }
}
