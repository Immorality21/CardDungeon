using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
using Assets.Scripts.IO;
using UnityEngine;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Writes <c>RunHistory.json</c> from the game's events - nothing in the dungeon calls in here
    /// (docs/plans/POLISH_CONTENT.md §15). Static and self-subscribing, so no scene has to carry it:
    /// <see cref="GameEvents"/> is cleared at <c>SubsystemRegistration</c> and this subscribes at
    /// <c>BeforeSceneLoad</c>, after it.
    ///
    /// <para>The file is read lazily and through a fresh <see cref="FileHandler"/> each time, so the
    /// sandbox's throwaway folder (set by <c>SandboxBootstrap</c>, also before the first scene) is
    /// honoured. Free play and the sandbox have no active run and record nothing.</para>
    /// </summary>
    public static class RunHistoryRecorder
    {
        private static RunHistorySaveData _data;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            _data = null;
            _lastFoes = new List<string>();
            GameEvents.Subscribe<LevelStarted>(OnLevelStarted);
            GameEvents.Subscribe<EnemyDefeated>(OnEnemyDefeated);
            GameEvents.Subscribe<CombatFinished>(OnCombatFinished);
            GameEvents.Subscribe<RoomEntered>(OnRoomEntered);
            GameEvents.Subscribe<XpAwarded>(OnXpAwarded);
            GameEvents.Subscribe<ItemAcquired>(OnItemAcquired);
            GameEvents.Subscribe<CurrencyChanged>(OnCurrencyChanged);
            GameEvents.Subscribe<LevelCleared>(OnLevelCleared);
            GameEvents.Subscribe<LevelForfeited>(OnLevelForfeited);
        }

        /// <summary>The history as it stands on disk (plus anything this session has recorded).</summary>
        public static RunHistorySaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = new FileHandler().Load<RunHistorySaveData>();
                }
                return _data;
            }
        }

        /// <summary>The attempt underway, or null.</summary>
        public static RunRecord Current => Data.HasCurrent ? Data.Current : null;

        /// <summary>The most recently finished attempt, or null.</summary>
        public static RunRecord Latest => Data.Records.Count > 0 ? Data.Records[0] : null;

        /// <summary>Drops the cached copy so the next read comes off disk (a save folder switch).</summary>
        public static void Invalidate()
        {
            _data = null;
        }

        private static string Now => DateTime.UtcNow.ToString("o");

        private static void Save()
        {
            new FileHandler().Save(Data);
        }

        private static void OnLevelStarted(LevelStarted e)
        {
            var run = DungeonManager.ActiveRun;
            if (e == null || string.IsNullOrEmpty(e.RunKey) || run == null)
            {
                return;
            }
            var level = e.LevelIndex >= 0 && e.LevelIndex < run.Levels.Count ? run.Levels[e.LevelIndex] : null;
            RunHistoryOps.FloorStarted(Data, e.RunKey, CampaignOps.DisplayNameOf(run), run.Levels.Count,
                e.LevelIndex, level != null ? level.LevelName : null, e.Fresh,
                RunFear.Current.IsRevisit, RunFear.Current.Level, null, Now);
            Save();
        }

        private static void OnEnemyDefeated(EnemyDefeated e)
        {
            RunHistoryOps.EnemyDefeated(Data, e != null && e.IsBoss);
        }

        private static void OnCombatFinished(CombatFinished e)
        {
            if (e == null || !Data.HasCurrent)
            {
                return;
            }
            RunHistoryOps.FightFinished(Data, e.Won, e.HeroKeys);
            if (!e.Won)
            {
                // The wipe itself is resolved on the death screen a moment later (LevelForfeited).
                _lastFoes = e.FoesStanding != null ? new List<string>(e.FoesStanding) : new List<string>();
            }
            // Saved per fight so a closed game loses at most the fight in progress.
            Save();
        }

        private static void OnRoomEntered(RoomEntered e)
        {
            RunHistoryOps.RoomEntered(Data);
        }

        private static void OnXpAwarded(XpAwarded e)
        {
            if (e != null)
            {
                RunHistoryOps.XpEarned(Data, e.Amount);
            }
        }

        private static void OnItemAcquired(ItemAcquired e)
        {
            if (e == null)
            {
                return;
            }
            // Found in the dungeon only: the belt's free top-up and hub purchases are not finds.
            switch (e.Source)
            {
                case EconomySource.Kill:
                case EconomySource.Cache:
                case EconomySource.RoomEvent:
                case EconomySource.LevelClear:
                    RunHistoryOps.ItemsFound(Data, e.Quantity);
                    break;
            }
        }

        private static void OnCurrencyChanged(CurrencyChanged e)
        {
            if (e == null || e.Pending)
            {
                return;
            }
            switch (e.Source)
            {
                case EconomySource.Kill:
                case EconomySource.Cache:
                case EconomySource.RoomEvent:
                case EconomySource.LevelClear:
                case EconomySource.RunDeath:
                    RunHistoryOps.CurrencyKept(Data, e.Currency == Currency.Essence, e.Delta);
                    break;
            }
        }

        private static void OnLevelCleared(LevelCleared e)
        {
            if (!Data.HasCurrent)
            {
                return;
            }
            RunHistoryOps.FloorCleared(Data, e != null && e.RunCompleted, Now);
            Save();
        }

        private static void OnLevelForfeited(LevelForfeited e)
        {
            if (e == null || !Data.HasCurrent)
            {
                return;
            }
            if (e.Reason == ForfeitReason.PartyDied)
            {
                RunHistoryOps.Fell(Data, _lastFoes, Now);
            }
            else
            {
                RunHistoryOps.Retreated(Data);
            }
            Save();
        }

        /// <summary>The foes standing when the last fight was lost, held for the wipe that follows.</summary>
        private static List<string> _lastFoes = new List<string>();

        /// <summary>Who the party fell to in the fight just lost - for the death screen.</summary>
        public static IList<string> LastFoes => _lastFoes;
    }
}
