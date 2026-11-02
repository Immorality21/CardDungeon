using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>Where a quest on the board stands. A quest with no progress entry is on offer.</summary>
    public enum QuestState
    {
        Offered = 0,
        Accepted = 1,
        /// <summary>Done; the reward waits at the tavern.</summary>
        Ready = 2,
    }

    [Serializable]
    public class QuestProgress
    {
        public string Key;
        public QuestState State;
        public int Count;
    }

    /// <summary>A collected quest cannot be drawn again until <see cref="QuestSaveData.Completed"/>
    /// reaches <see cref="FreeAt"/>.</summary>
    [Serializable]
    public class QuestCooldown
    {
        public string Key;
        public int FreeAt;
    }

    /// <summary>
    /// <c>Quests.json</c>: the board (up to <see cref="QuestOps.BoardSize"/> quest keys, slot order) and
    /// the progress of the ones taken. Written the moment it changes - a kill counts as it happens,
    /// whatever becomes of the floor (owner, 2026-11-02). A collected quest leaves both lists and goes
    /// back into the pool.
    /// </summary>
    [Serializable]
    public class QuestSaveData : IWriteable
    {
        public List<string> Board = new List<string>();
        public List<QuestProgress> Quests = new List<QuestProgress>();

        /// <summary>How many quests have been collected, all told. The clock cooldowns count on.</summary>
        public int Completed;

        /// <summary>Quests just collected, sitting out until <see cref="QuestCooldown.FreeAt"/>.</summary>
        public List<QuestCooldown> Cooldowns = new List<QuestCooldown>();

        public string GetFileName()
        {
            return "Quests";
        }
    }
}
