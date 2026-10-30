using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
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
            if (tutorial == null || tutorial.GearReward == null || tutorial.GearRunOrFirst != run
                || tutorial.GearLevelIndex != levelIndex)
            {
                return;
            }
            InventoryManager.Instance.AddItem(tutorial.GearReward, 1, EconomySource.LevelClear);
        }
    }
}
