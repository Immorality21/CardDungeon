using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>
    /// The tavern's writes: filling the board, taking a quest, rerolling one for gold and collecting one.
    /// The rules are <see cref="QuestOps"/>; this class reads and writes <c>Quests.json</c> around them
    /// and pays out - the item into the bag (a recipe already known is skipped, it would teach nothing),
    /// the gold into the purse and the XP onto every owned hero's bank.
    /// </summary>
    public static class QuestRewards
    {
        public static List<QuestSO> Pool()
        {
            var catalog = QuestCatalogSO.Load();
            return catalog != null && catalog.Quests != null ? catalog.Quests : new List<QuestSO>();
        }

        private static List<string> CompletedRuns()
        {
            return MetaProgressManager.Instance != null ? MetaProgressManager.Instance.GetCompletedRunKeys() : new List<string>();
        }

        /// <summary>
        /// Every run of the campaign (<c>Resources/Campaign.asset</c>), so the board only offers quests
        /// that can still be done (<see cref="QuestOps.IsDoable"/>, playtest 4 finding 14). Null when there
        /// is no campaign, which skips that check.
        /// </summary>
        public static List<RunDefinitionSO> CampaignRuns()
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            if (campaign == null || campaign.Nodes == null)
            {
                return null;
            }
            var runs = new List<RunDefinitionSO>();
            foreach (var node in campaign.Nodes)
            {
                if (node != null && node.Run != null)
                {
                    runs.Add(node.Run);
                }
            }
            return runs;
        }

        /// <summary>Whether <see cref="Reroll"/> would find a quest to swap in (gold aside).</summary>
        public static bool CanReroll(QuestSO quest)
        {
            var save = new FileHandler().Load<QuestSaveData>();
            return QuestOps.CanReroll(save, Pool(), CompletedRuns(), quest, CampaignRuns());
        }

        private static int Pick(int count)
        {
            return UnityEngine.Random.Range(0, count);
        }

        /// <summary>The save with its board filled to three, written if that changed it.</summary>
        public static QuestSaveData LoadBoard()
        {
            var files = new FileHandler();
            var save = files.Load<QuestSaveData>();
            if (QuestOps.FillBoard(save, Pool(), CompletedRuns(), Pick, CampaignRuns()))
            {
                files.Save(save);
            }
            return save;
        }

        /// <summary>Takes a quest on the board and writes it.</summary>
        public static bool Accept(QuestSO quest)
        {
            var files = new FileHandler();
            var save = files.Load<QuestSaveData>();
            if (!QuestOps.Accept(save, quest))
            {
                return false;
            }
            files.Save(save);
            return true;
        }

        /// <summary>Pays <see cref="QuestOps.RerollGold"/> and swaps the quest for another. Nothing is spent
        /// when nothing could be drawn, or the purse is short.</summary>
        public static bool Reroll(QuestSO quest)
        {
            var files = new FileHandler();
            var save = files.Load<QuestSaveData>();
            if (!QuestOps.CanReroll(save, Pool(), CompletedRuns(), quest, CampaignRuns())
                || MetaProgressManager.Instance == null
                || !MetaProgressManager.Instance.TrySpendGold(QuestOps.RerollGold, EconomySource.Quest))
            {
                return false;
            }
            QuestOps.Reroll(save, Pool(), CompletedRuns(), quest, Pick, CampaignRuns());
            files.Save(save);
            return true;
        }

        /// <summary>Returns false, and pays nothing, unless the quest was ready to collect. The board is
        /// refilled after, so the slot comes back with a new quest.</summary>
        public static bool Collect(QuestSO quest, PartyRosterSO roster)
        {
            var files = new FileHandler();
            var save = files.Load<QuestSaveData>();
            if (!QuestOps.Claim(save, quest))
            {
                return false;
            }
            QuestOps.FillBoard(save, Pool(), CompletedRuns(), Pick, CampaignRuns());
            // Written first: a reward paid with the claim unsaved could be collected twice.
            files.Save(save);

            if (quest.RewardItem != null && InventoryManager.Instance != null
                && !InventoryManager.Instance.AlreadyKnown(quest.RewardItem))
            {
                InventoryManager.Instance.AddItem(quest.RewardItem, quest.RewardItemCount, EconomySource.Quest);
            }
            if (quest.RewardGold > 0 && MetaProgressManager.Instance != null)
            {
                MetaProgressManager.Instance.AddGold(quest.RewardGold, EconomySource.Quest);
            }
            HeroRoster.GrantXpToOwned(roster, quest.RewardXp);
            return true;
        }
    }
}
