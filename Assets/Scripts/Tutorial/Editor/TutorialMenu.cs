using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
using Assets.Scripts.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Scripts.Tutorial.Editor
{
    /// <summary>
    /// <c>Tools ▸ Tutorial</c>: walk the tutorial by hand without touching the real save.
    /// <list type="bullet">
    /// <item><b>Play From New Game</b> - play mode from the title screen against a fresh throwaway
    /// save (<see cref="TutorialTestSession"/>), so the title offers New Game.</item>
    /// <item><b>Clear This Floor</b> - in such a session, finishes the floor the party is on through
    /// the real level-clear path (docs/TUTORIAL.md, learning 5), so each step can be reached in seconds.</item>
    /// </list>
    /// </summary>
    public static class TutorialMenu
    {
        public const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

        // Floor 1's sure XP: two EyeBalls x 10 XP x the level's multiplier 2 (docs/TUTORIAL.md).
        private const int FloorXp = 40;

        [MenuItem("Tools/Tutorial/Play From New Game (throwaway save)", false, 30)]
        public static void PlayFromNewGame()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                // Opening the title scene would discard unsaved edits: ask first, and stop on Cancel.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }
            SessionState.SetBool(TutorialTestSession.PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Tutorial/Play From New Game (throwaway save)", true)]
        private static bool CanPlayFromNewGame()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// Frees any captive on the floor, pays the floor's XP and clears it. The level-clear screen
        /// then opens as usual; press its button to go on. Only inside a Play From New Game session,
        /// so it can never hand the real save a floor it did not play.
        /// </summary>
        [MenuItem("Tools/Tutorial/Clear This Floor", false, 31)]
        public static void ClearThisFloor()
        {
            var dungeon = DungeonManager.Instance;
            foreach (var room in Object.FindObjectsByType<Room>(FindObjectsSortMode.None))
            {
                if (room.CaptiveHero != null)
                {
                    dungeon.TryRescueCaptive(room);
                }
            }
            dungeon.Party.DistributeXp(FloorXp, EconomySource.Kill);
            CombatManager.Instance.NotifyDungeonCleared();
        }

        [MenuItem("Tools/Tutorial/Clear This Floor", true)]
        private static bool CanClearThisFloor()
        {
            return EditorApplication.isPlaying && TutorialTestSession.IsActive
                && DungeonManager.HasInstance && DungeonManager.Instance.Party != null
                && CombatManager.HasInstance && !CombatManager.Instance.InCombat;
        }
    }
}
