using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>One fight, as the real combat code played it.</summary>
    [Serializable]
    public class BotFight
    {
        public string Room;
        public bool Boss;
        public string Enemies;
        public int Heroes;
        public int HeroTurns;

        /// <summary>The party's health as a share of its maximum, before and after.</summary>
        public float HealthBefore;
        public float HealthAfter;
        public int HeroesDown;
        public int PotionsUsed;
        public int UltrasUsed;
        /// <summary>Summons called in this fight (once per level, so at most one per hero).</summary>
        public int SummonsUsed;
        /// <summary>Turns a summoned unit took, and how many of them were its Signature or one of its moves.</summary>
        public int SummonTurns;
        public int SummonSignatures;
        public int SummonMoves;
        /// <summary>A boss fight with no summon called: each hero's summons and charges on their first turn, so the
        /// report says why (none known, spent earlier in the level, could not pay).</summary>
        public string SummonNote = "";

        /// <summary>Not reported: per summoned unit, the turn (its <c>TurnsTaken</c>) each of its moves was last used.</summary>
        [NonSerialized] public Dictionary<object, Dictionary<string, int>> MoveTurns = new Dictionary<object, Dictionary<string, int>>();
        public int Casts;
        public string Outcome;
    }

    /// <summary>A room event and what the profile did with it.</summary>
    [Serializable]
    public class BotEvent
    {
        public string Event;
        public string Option;
        public float Chance;
        public bool Declined;
    }

    /// <summary>One attempt at one floor.</summary>
    [Serializable]
    public class BotFloor
    {
        public string Level;
        public int RoomsSeen;
        public int RoomsTotal;
        public float HealthAtEnd;
        public int PotionsBetweenRooms;
        public List<BotFight> Fights = new List<BotFight>();
        public List<BotEvent> Events = new List<BotEvent>();
    }

    /// <summary>One attempt at one run.</summary>
    [Serializable]
    public class BotRun
    {
        public string Run;
        public string Key;
        public int Attempt;
        public string Outcome = "unfinished";
        public string Party;
        public List<BotFloor> Floors = new List<BotFloor>();
    }

    /// <summary>What one visit to town did.</summary>
    [Serializable]
    public class BotHubVisit
    {
        public string Before;
        public int NodesBought;
        /// <summary>SummonFirst: nodes bought on the path to a summon, and the steps that waited on materials or Essence.</summary>
        public int SummonPathNodes;
        public string SummonPathBlocked = "";
        public int XpLeft;
        public int ItemsBought;
        public int GoldSpentOnGear;
        public int ItemsCrafted;
        public int GoldSpentOnCrafting;
        public int ItemsEquipped;
        public string Built;
        public int GoldLeft;

        /// <summary>The tavern: quests collected this visit, how many paid other than promised, the
        /// board as it was left (key progress/count), and one line per accept or collect.</summary>
        public int QuestsCollected;
        public int QuestMismatches;
        public string QuestBoard = "";
        public List<string> QuestLog = new List<string>();
    }

    /// <summary>One whole playthrough from a new save.</summary>
    [Serializable]
    public class BotGame
    {
        public string Profile;
        public int Seed;
        public string Finished = "";
        public string Stuck = "";
        public float RealSeconds;
        public List<BotRun> Runs = new List<BotRun>();
        public List<BotHubVisit> Hub = new List<BotHubVisit>();
        /// <summary>The class items (<c>ItemSO.UsableBy</c>) the party owned when the game ended - the
        /// rare boss drops (<c>RunLevelEntry.BossDrops</c>), which the fight report does not list.</summary>
        public List<string> ClassItems = new List<string>();
    }

    /// <summary>A batch. Rewritten after every attempt, so a batch that dies keeps what it had.</summary>
    [Serializable]
    public class BotReport
    {
        public string Status = "running";
        public string Current = "";
        public List<BotGame> Games = new List<BotGame>();

        /// <summary>Outside any save folder, so no new game can wipe it.</summary>
        public static string Folder => $"{Application.persistentDataPath}/bot_reports";

        public static string PathFor(string name) => $"{Folder}/{name}.json";

        public void Write(string name)
        {
            System.IO.Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(name), JsonUtility.ToJson(this, true));
        }
    }
}
