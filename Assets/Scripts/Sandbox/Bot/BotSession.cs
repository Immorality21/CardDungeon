using System.IO;
using Assets.Scripts.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>
    /// Points every save at a throwaway folder and starts the bot before the first scene loads: the
    /// launcher leaves the batch in <c>SessionState</c>, which survives the play-mode domain reload. The
    /// same pattern as <c>SandboxBootstrap</c>, and editor-only by construction, so it cannot ship.
    /// </summary>
    public static class BotSession
    {
        public const string PendingKey = "CardDungeon.Bot.PendingSettings";

        /// <summary>The throwaway save folder: <c>persistentDataPath/savedata_bot</c>.</summary>
        public static string Directory => $"{Application.persistentDataPath}/savedata_bot";

        /// <summary>A new, empty save, at the start of every game.</summary>
        public static void ClearSave()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, true);
            }
            System.IO.Directory.CreateDirectory(Directory);
        }

        /// <summary>The batch is over: stop play, which also hands the save folder back.</summary>
        public static void Finish()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeFirstScene()
        {
            var json = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            SessionState.EraseString(PendingKey);

            ClearSave();
            FileHandler.DirectoryOverride = Directory;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            var settings = JsonUtility.FromJson<BotSettings>(json);
            Debug.Log($"[Bot] Starting '{settings.ReportName}'. Saves go to {Directory}.");
            BotPlayer.Begin(settings);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                FileHandler.DirectoryOverride = null;
            }
        }
#endif
    }
}
