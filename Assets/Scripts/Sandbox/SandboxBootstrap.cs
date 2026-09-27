using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.Sandbox
{
    /// <summary>
    /// Carries a sandbox launch across the domain reload that entering play mode performs. The
    /// launcher stores the config's asset path in <c>SessionState</c> (which survives the reload,
    /// unlike a static field); this reads it back before the first scene loads, so every manager's
    /// <c>Awake</c> already sees the sandbox save folder. The key is consumed on read, so the next
    /// ordinary Play is ordinary.
    ///
    /// <para>Editor-only by construction: in a player build there is no <c>SessionState</c> and
    /// this does nothing, so the sandbox cannot ship.</para>
    /// </summary>
    public static class SandboxBootstrap
    {
        public const string PendingConfigKey = "CardDungeon.Sandbox.PendingConfigPath";

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeFirstScene()
        {
            var path = SessionState.GetString(PendingConfigKey, "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            SessionState.EraseString(PendingConfigKey);

            var config = AssetDatabase.LoadAssetAtPath<SandboxConfigSO>(path);
            if (config == null)
            {
                Debug.LogError($"[Sandbox] Config '{path}' not found; starting normally.");
                return;
            }

            SandboxSession.Begin(config);

            // Domain reload happens on *entering* play mode, not leaving it, so the save-folder
            // override would otherwise outlive the session and point edit-mode tools at the sandbox.
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                SandboxSession.End();
            }
        }
#endif
    }
}
