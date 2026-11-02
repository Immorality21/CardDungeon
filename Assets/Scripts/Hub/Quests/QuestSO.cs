using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using ImmoralityGaming.Localization;
using UnityEngine;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>What a quest asks for. Appended only: assets store it by ordinal.</summary>
    public enum QuestKind
    {
        /// <summary>Kill <see cref="QuestSO.Count"/> of <see cref="QuestSO.Target"/>, of a kind
        /// (<see cref="QuestSO.TargetTraits"/>), or of anything when both are empty.</summary>
        KillCount = 0,

        /// <summary>The party falls <see cref="QuestSO.Count"/> times (a wipe, not a quit).</summary>
        Wipes = 1,

        /// <summary>Take the stairs <see cref="QuestSO.Count"/> times.</summary>
        ClearFloors = 2,
    }

    /// <summary>
    /// One quest in the tavern's pool. The board shows three at a time, drawn from the quests whose
    /// conditions hold (<see cref="RequiredRunKeys"/>); a collected one goes back into the pool. Taken at
    /// the tavern, counted as it happens (<see cref="QuestTracker"/>), collected back at the tavern.
    /// Listed in <see cref="QuestCatalogSO"/>; the board and progress live in <c>Quests.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest", menuName = "SO/Quest")]
    public class QuestSO : ScriptableObject
    {
        [Tooltip("Write-once save identifier: progress is filed under it.")]
        [TextKeys("quest", "name", "description")]
        public string Key;

        /// <summary>From the text table (<c>quest.{key}.name</c>).</summary>
        public string DisplayName => Loc.AssetText("quest", Key, this, "name");

        /// <summary>From the text table (<c>quest.{key}.description</c>): the tavern's flavour line.</summary>
        public string Description => Loc.AssetText("quest", Key, this, "description");

        public QuestKind Kind = QuestKind.KillCount;

        [Tooltip("KillCount: the enemy to kill. Matched on its SaveKey, so called-in bodies count too. " +
                 "Leave empty and set Target Traits to count a kind of enemy, or leave both empty for any enemy.")]
        public EnemySO Target;

        [Tooltip("KillCount with no Target: any enemy with one of these traits counts (\"kill 15 undead\").")]
        public UnitTraits TargetTraits;

        [Tooltip("KillCount: only kills on a revisit count (the harder version of a cleared run).")]
        public bool RevisitOnly;

        [Min(1)] public int Count = 5;

        [Tooltip("The board's picture. Empty: the Target's sprite.")]
        public Sprite Icon;

        [Header("When it can be drawn")]
        [Tooltip("Every one of these runs cleared before this quest can reach the board.")]
        public List<string> RequiredRunKeys = new List<string>();

        [Header("Reward")]
        [Tooltip("Handed over on collecting - a recipe or a material. Skipped when it is a recipe already known.")]
        public ItemSO RewardItem;

        [Min(1)] public int RewardItemCount = 1;

        [Min(0)] public int RewardGold;

        [Tooltip("Banked XP for every hero the player owns, benched ones included.")]
        [Min(0)] public int RewardXp;

        /// <summary>The board's picture: <see cref="Icon"/>, else the target's sprite.</summary>
        public Sprite BoardIcon => Icon != null ? Icon : (Target != null ? Target.Sprite : null);

        /// <summary>Whether killing this enemy counts toward the quest.</summary>
        public bool Counts(string enemyKey, UnitTraits traits, bool onRevisit)
        {
            if (Kind != QuestKind.KillCount || (RevisitOnly && !onRevisit))
            {
                return false;
            }
            if (Target != null)
            {
                return !string.IsNullOrEmpty(enemyKey) && Target.SaveKey == enemyKey;
            }
            return TargetTraits == UnitTraits.None || (traits & TargetTraits) != 0;
        }

        /// <summary>Whether the quest's conditions hold for a save with these runs cleared.</summary>
        public bool Unlocked(ICollection<string> completedRunKeys)
        {
            if (RequiredRunKeys == null)
            {
                return true;
            }
            foreach (var key in RequiredRunKeys)
            {
                if (!string.IsNullOrEmpty(key) && (completedRunKeys == null || !completedRunKeys.Contains(key)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
