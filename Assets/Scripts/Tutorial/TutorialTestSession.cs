using System.IO;
using Assets.Scripts.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// A hand-test of the tutorial against a throwaway save: <c>Tools ▸ Tutorial ▸ Play From New Game</c>
    /// stores a flag in <c>SessionState</c> (it survives the play-mode domain reload; a static would
    /// not), and this reads it before the first scene loads, wipes <see cref="Directory"/> and points
    /// every save at it - so the title screen offers New Game and the player's own save is never touched.
    /// The same pattern as <c>SandboxBootstrap</c>.
    ///
    /// <para>Editor-only by construction: a player build has no <c>SessionState</c>, so this does nothing.
    /// A script recompile mid-play reloads the domain and resets the override to the real save folder
    /// (docs/TUTORIAL.md, learning 4) - stop play before editing scripts.</para>
    /// </summary>
    public static class TutorialTestSession
    {
        public const string PendingKey = "CardDungeon.Tutorial.PlayFromNewGame";

        /// <summary>The throwaway save folder: <c>persistentDataPath/savedata_tutorial</c>, wiped on every launch.</summary>
        public static string Directory => $"{Application.persistentDataPath}/savedata_tutorial";

        public static bool IsActive { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeFirstScene()
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                return;
            }
            SessionState.EraseBool(PendingKey);

            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, true);
            }
            System.IO.Directory.CreateDirectory(Directory);
            // Audio only, so the test plays at the volume the player set.
            var audio = $"{FileHandler.DefaultDirectory}/Audio.json";
            if (File.Exists(audio))
            {
                File.Copy(audio, $"{Directory}/Audio.json", true);
            }

            FileHandler.DirectoryOverride = Directory;
            IsActive = true;
            Debug.Log($"[Tutorial] Playing from New Game. Saves go to {Directory}.");

            // The domain reloads on entering play mode, not on leaving it: hand the folder back on exit.
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                FileHandler.DirectoryOverride = null;
                IsActive = false;
            }
        }
#endif
    }
}
