using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
using Assets.Scripts.Hub;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// What the tutorial hands over in the dungeon. Kept here rather than in <c>DungeonManager</c> so
    /// the dungeon only says "a level was cleared" and every tutorial rule stays in this folder.
    /// </summary>
    public static class TutorialRewards
    {
        /// <summary>
        /// The gear floor pays one promised piece of equipment (<see cref="TutorialSO.GearReward"/>),
        /// which the hub's gear step then has the player equip. Only while the tutorial runs and only
        /// on its own run and level, so a revisit never pays it again. Call it on level clear while
        /// inventory saves are still deferred, so it rides the same write as the floor's other gains,
        /// is forfeited by the same rule, and shows on the level-clear summary.
        /// </summary>
        public static void OnLevelCleared(RunDefinitionSO run, int levelIndex)
        {
            var meta = MetaProgressManager.Instance;
            if (!meta.TutorialStarted || meta.TutorialFinished || run == null)
            {
                return;
            }
            var tutorial = UnityEngine.Resources.Load<TutorialSO>(TutorialSO.ResourcePath);
            if (tutorial == null)
            {
                return;
            }
            if (tutorial.GearReward != null && tutorial.GearRunOrFirst == run && tutorial.GearLevelIndex == levelIndex)
            {
                InventoryManager.Instance.AddItem(tutorial.GearReward, 1, EconomySource.LevelClear);
            }
            if (tutorial.BeltRun == run && levelIndex == run.Levels.Count - 1)
            {
                PayForTheBelt(tutorial);
            }
        }

        /// <summary>
        /// The belt step's promise: whatever is still missing for the Merchant and the first healing
        /// capacity step, so the player can pay and nothing they saved is wasted, and the recipe of
        /// <see cref="TutorialSO.BeltPotionKey"/>. A belt recipe is not an item (the Merchant sells it),
        /// so it is learned outright. The clear's own gold counts towards the price.
        /// </summary>
        private static void PayForTheBelt(TutorialSO tutorial)
        {
            var belt = PotionBeltSO.Load();
            if (belt == null)
            {
                return;
            }

            var bill = new Dictionary<ItemSO, int>();
            var merchant = HubState.Town()?.Find(tutorial.BeltBuildingKey);
            if (merchant != null && !BuildingOps.IsBuilt(merchant, HubState.Progress()))
            {
                AddToBill(bill, merchant.PlacementCost);
            }

            var healing = belt.Find(PotionBeltState.HealingKey);
            var step = PotionBeltOps.NextCapacity(healing, PotionBeltState.Capacity(healing));
            if (step != null)
            {
                AddToBill(bill, step.Materials);
            }

            var inventory = InventoryManager.Instance;
            foreach (var line in bill)
            {
                int missing = TutorialOps.Shortfall(line.Value, inventory.GetMaterialQuantity(line.Key.Key));
                if (missing > 0)
                {
                    inventory.AddItem(line.Key, missing, EconomySource.LevelClear);
                }
            }

            var meta = MetaProgressManager.Instance;
            int gold = TutorialOps.Shortfall(step != null ? step.Gold : 0, meta.Gold + meta.PendingRunGold);
            if (gold > 0)
            {
                meta.AddGold(gold, EconomySource.LevelClear);
            }

            var potion = belt.Find(tutorial.BeltPotionKey);
            if (potion != null)
            {
                PotionBeltState.Unlock(potion);
            }
        }

        private static void AddToBill(Dictionary<ItemSO, int> bill, IEnumerable<MaterialCost> costs)
        {
            foreach (var cost in costs)
            {
                if (cost != null && cost.Material != null)
                {
                    bill.TryGetValue(cost.Material, out int amount);
                    bill[cost.Material] = amount + cost.Amount;
                }
            }
        }
    }
}
