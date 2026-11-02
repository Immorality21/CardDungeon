using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>
    /// The quest board's rules, pure (owner, 2026-11-02): three quests at a time drawn from the pool of
    /// unlocked ones; one not yet taken can be rerolled for gold; a collected one goes back into the
    /// pool and the slot is drawn again. Counting toward a taken quest is here too. Rewards and gold are
    /// paid by <see cref="QuestRewards"/>; nothing here touches a file or a singleton.
    /// </summary>
    public static class QuestOps
    {
        public const int BoardSize = 3;

        /// <summary>What a reroll costs. Flat for now; a free daily one waits on the player's days.</summary>
        public const int RerollGold = 100;

        /// <summary>
        /// A collected quest sits out until this many other quests have been collected (owner,
        /// 2026-11-02: the bot found the quests that complete themselves - kill anything, take the
        /// stairs - coming straight back and making up half of what it collected).
        /// </summary>
        public const int CooldownCollections = 3;

        public static bool OnCooldown(QuestSaveData save, QuestSO quest)
        {
            if (save == null || save.Cooldowns == null || quest == null)
            {
                return false;
            }
            var cooldown = save.Cooldowns.Find(c => c != null && c.Key == quest.Key);
            return cooldown != null && save.Completed < cooldown.FreeAt;
        }

        public static QuestProgress Find(QuestSaveData save, QuestSO quest)
        {
            if (save == null || save.Quests == null || quest == null || string.IsNullOrEmpty(quest.Key))
            {
                return null;
            }
            return save.Quests.Find(q => q != null && q.Key == quest.Key);
        }

        public static QuestState StateOf(QuestSaveData save, QuestSO quest)
        {
            var progress = Find(save, quest);
            return progress != null ? progress.State : QuestState.Offered;
        }

        /// <summary>How far along it is, capped at its count.</summary>
        public static int ProgressOf(QuestSaveData save, QuestSO quest)
        {
            var progress = Find(save, quest);
            if (progress == null || quest == null)
            {
                return 0;
            }
            return progress.State == QuestState.Accepted ? progress.Count : quest.Count;
        }

        public static bool IsOnBoard(QuestSaveData save, QuestSO quest)
        {
            return save != null && quest != null && save.Board.Contains(quest.Key);
        }

        /// <summary>The board's quests, in slot order (keys the catalog no longer has are skipped).</summary>
        public static List<QuestSO> BoardQuests(QuestSaveData save, IList<QuestSO> pool)
        {
            var result = new List<QuestSO>();
            if (save == null || pool == null)
            {
                return result;
            }
            foreach (var key in save.Board)
            {
                var quest = FindInPool(pool, key);
                if (quest != null)
                {
                    result.Add(quest);
                }
            }
            return result;
        }

        /// <summary>
        /// Drops keys the catalog no longer has and fills every empty slot with a random unlocked quest
        /// not already on the board. Returns whether the board changed. <paramref name="pick"/> takes a
        /// count and returns an index below it.
        /// </summary>
        public static bool FillBoard(QuestSaveData save, IList<QuestSO> pool, ICollection<string> completedRuns,
            Func<int, int> pick)
        {
            if (save == null || pool == null)
            {
                return false;
            }
            int before = save.Board.Count;
            bool changed = save.Board.RemoveAll(k => FindInPool(pool, k) == null) > 0;
            while (save.Board.Count < BoardSize)
            {
                var candidates = Drawable(save, pool, completedRuns, null);
                if (candidates.Count == 0)
                {
                    break;
                }
                save.Board.Add(candidates[pick(candidates.Count)].Key);
            }
            return changed || save.Board.Count != before;
        }

        /// <summary>
        /// Swaps a quest on the board that is not yet taken for another drawable one, in the same slot.
        /// False, and nothing changes, when it is taken or nothing else can be drawn.
        /// </summary>
        public static bool Reroll(QuestSaveData save, IList<QuestSO> pool, ICollection<string> completedRuns,
            QuestSO quest, Func<int, int> pick)
        {
            if (!IsOnBoard(save, quest) || StateOf(save, quest) != QuestState.Offered)
            {
                return false;
            }
            var candidates = Drawable(save, pool, completedRuns, quest);
            if (candidates.Count == 0)
            {
                return false;
            }
            save.Board[save.Board.IndexOf(quest.Key)] = candidates[pick(candidates.Count)].Key;
            return true;
        }

        /// <summary>Whether a reroll would find anything to swap in.</summary>
        public static bool CanReroll(QuestSaveData save, IList<QuestSO> pool, ICollection<string> completedRuns, QuestSO quest)
        {
            return IsOnBoard(save, quest) && StateOf(save, quest) == QuestState.Offered
                && Drawable(save, pool, completedRuns, quest).Count > 0;
        }

        /// <summary>Takes a quest on the board. Only what happens after this counts.</summary>
        public static bool Accept(QuestSaveData save, QuestSO quest)
        {
            if (!IsOnBoard(save, quest) || StateOf(save, quest) != QuestState.Offered)
            {
                return false;
            }
            save.Quests.Add(new QuestProgress { Key = quest.Key, State = QuestState.Accepted, Count = 0 });
            return true;
        }

        /// <summary>One enemy fell. Returns whether anything changed, so a caller saves only then.</summary>
        public static bool RecordKill(QuestSaveData save, IEnumerable<QuestSO> quests, string enemyKey,
            UnitTraits traits = UnitTraits.None, bool onRevisit = false)
        {
            return Advance(save, quests, q => q.Counts(enemyKey, traits, onRevisit));
        }

        /// <summary>The party fell, or the stairs were taken: counts toward every taken quest of that kind.</summary>
        public static bool RecordEvent(QuestSaveData save, IEnumerable<QuestSO> quests, QuestKind kind)
        {
            return Advance(save, quests, q => q.Kind == kind);
        }

        /// <summary>
        /// Marks a finished quest collected: it leaves the board and its progress is dropped, so it can be
        /// drawn again later. The caller pays the reward (and refills the board) when this returns true.
        /// </summary>
        public static bool Claim(QuestSaveData save, QuestSO quest)
        {
            var progress = Find(save, quest);
            if (progress == null || progress.State != QuestState.Ready)
            {
                return false;
            }
            save.Quests.Remove(progress);
            save.Board.Remove(quest.Key);
            save.Completed++;
            save.Cooldowns.RemoveAll(c => c == null || c.Key == quest.Key || save.Completed >= c.FreeAt);
            save.Cooldowns.Add(new QuestCooldown { Key = quest.Key, FreeAt = save.Completed + CooldownCollections });
            return true;
        }

        /// <summary>Something at the tavern wants the player: a quest not yet taken, or one to collect.</summary>
        public static bool HasNews(QuestSaveData save, IList<QuestSO> pool)
        {
            foreach (var quest in BoardQuests(save, pool))
            {
                var state = StateOf(save, quest);
                if (state == QuestState.Offered || state == QuestState.Ready)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Advance(QuestSaveData save, IEnumerable<QuestSO> quests, Func<QuestSO, bool> counts)
        {
            if (save == null || quests == null)
            {
                return false;
            }
            bool changed = false;
            foreach (var quest in quests)
            {
                if (quest == null || !counts(quest))
                {
                    continue;
                }
                var progress = Find(save, quest);
                if (progress == null || progress.State != QuestState.Accepted)
                {
                    continue;
                }
                progress.Count++;
                if (progress.Count >= quest.Count)
                {
                    progress.Count = quest.Count;
                    progress.State = QuestState.Ready;
                }
                changed = true;
            }
            return changed;
        }

        /// <summary>Unlocked quests not on the board and not cooling down (nor <paramref name="leaving"/>,
        /// which is never drawn back).</summary>
        private static List<QuestSO> Drawable(QuestSaveData save, IList<QuestSO> pool, ICollection<string> completedRuns,
            QuestSO leaving)
        {
            var result = new List<QuestSO>();
            foreach (var quest in pool)
            {
                if (quest != null && !string.IsNullOrEmpty(quest.Key) && quest != leaving
                    && !save.Board.Contains(quest.Key) && !OnCooldown(save, quest) && quest.Unlocked(completedRuns))
                {
                    result.Add(quest);
                }
            }
            return result;
        }

        private static QuestSO FindInPool(IList<QuestSO> pool, string key)
        {
            foreach (var quest in pool)
            {
                if (quest != null && quest.Key == key)
                {
                    return quest;
                }
            }
            return null;
        }
    }
}
