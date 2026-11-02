using Assets.Scripts.Combat;
using Assets.Scripts.Events;
using Assets.Scripts.IO;
using UnityEngine;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>
    /// Counts toward the quests the player has taken: kills, wipes and floors cleared. Static and
    /// self-subscribing like <c>RunHistoryRecorder</c>: <see cref="GameEvents"/> is cleared at
    /// <c>SubsystemRegistration</c> and this subscribes at <c>BeforeSceneLoad</c>, after it, so no scene
    /// carries it.
    ///
    /// <para>Everything counts the moment it happens and is written at once, wipe or no wipe (owner,
    /// 2026-11-02): quest progress is the town's knowledge, like the Bestiary, not a floor's loot. The
    /// file is read through a fresh <see cref="FileHandler"/> each time, so the sandbox's and the bot's
    /// save folders are honoured.</para>
    /// </summary>
    public static class QuestTracker
    {
        private static QuestCatalogSO _catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            _catalog = null;
            GameEvents.Subscribe<EnemyDefeated>(OnEnemyDefeated);
            GameEvents.Subscribe<LevelForfeited>(OnLevelForfeited);
            GameEvents.Subscribe<LevelCleared>(OnLevelCleared);
        }

        private static void OnEnemyDefeated(EnemyDefeated e)
        {
            if (e == null)
            {
                return;
            }
            var traits = e.Enemy != null ? e.Enemy.Traits : UnitTraits.None;
            Update((save, quests) => QuestOps.RecordKill(save, quests, e.EnemyKey, traits, RunFear.Current.IsRevisit));
        }

        private static void OnLevelForfeited(LevelForfeited e)
        {
            if (e != null && e.Reason == ForfeitReason.PartyDied)
            {
                Update((save, quests) => QuestOps.RecordEvent(save, quests, QuestKind.Wipes));
            }
        }

        private static void OnLevelCleared(LevelCleared e)
        {
            Update((save, quests) => QuestOps.RecordEvent(save, quests, QuestKind.ClearFloors));
        }

        private static void Update(System.Func<QuestSaveData, System.Collections.Generic.List<QuestSO>, bool> change)
        {
            if (_catalog == null)
            {
                _catalog = QuestCatalogSO.Load();
            }
            if (_catalog == null || _catalog.Quests == null || _catalog.Quests.Count == 0)
            {
                return;
            }

            var files = new FileHandler();
            var save = files.Load<QuestSaveData>();
            if (change(save, _catalog.Quests))
            {
                files.Save(save);
            }
        }
    }
}
