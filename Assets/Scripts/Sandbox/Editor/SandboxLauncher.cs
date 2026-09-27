using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Scripts.Sandbox.Editor
{
    /// <summary>
    /// Starts a sandbox session: remembers the config across the play-mode domain reload, opens
    /// <c>MainGameScene</c> and enters play mode. The <c>Tools ▸ Sandbox</c> window calls this, and
    /// so can a script or the Unity MCP (<c>SandboxLauncher.Launch("Assets/.../Sandbox.asset")</c>).
    /// </summary>
    public static class SandboxLauncher
    {
        public const string GameScenePath = "Assets/Scenes/MainGameScene.unity";
        public const string DefaultConfigPath = "Assets/ScriptableObjects/Sandbox/Sandbox.asset";

        public static bool Launch(string configPath)
        {
            return Launch(AssetDatabase.LoadAssetAtPath<SandboxConfigSO>(configPath));
        }

        /// <summary>
        /// Returns false (and logs why) when it could not start. It never discards unsaved scene
        /// edits: a dirty scene has to be saved first, because opening the game scene would throw
        /// them away and this may be called from a script with nobody there to answer a dialog.
        /// </summary>
        public static bool Launch(SandboxConfigSO config)
        {
            if (config == null)
            {
                Debug.LogError("[Sandbox] No config to launch.");
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Sandbox] Stop play mode first, then launch.");
                return false;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (active.path != GameScenePath)
            {
                if (HasUnsavedScenes())
                {
                    Debug.LogError("[Sandbox] Save your open scenes first; launching would discard their changes.");
                    return false;
                }
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            }

            SessionState.SetString(SandboxBootstrap.PendingConfigKey, AssetDatabase.GetAssetPath(config));
            EditorApplication.EnterPlaymode();
            return true;
        }

        /// <summary>The default config, created with a Warrior-vs-Floating-Eye starter if missing.</summary>
        public static SandboxConfigSO GetOrCreateDefault()
        {
            var config = AssetDatabase.LoadAssetAtPath<SandboxConfigSO>(DefaultConfigPath);
            if (config != null)
            {
                return config;
            }

            var folder = System.IO.Path.GetDirectoryName(DefaultConfigPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Sandbox");
            }

            config = ScriptableObject.CreateInstance<SandboxConfigSO>();
            var warrior = AssetDatabase.LoadAssetAtPath<Heroes.HeroSO>("Assets/ScriptableObjects/Heroes/Warrior.asset");
            if (warrior != null)
            {
                config.Heroes.Add(new SandboxHeroSetup { Hero = warrior });
            }
            var eye = AssetDatabase.LoadAssetAtPath<Enemies.EnemySO>("Assets/ScriptableObjects/Enemies/EyeBall.asset");
            if (eye != null)
            {
                config.Enemies.Add(eye);
            }
            AssetDatabase.CreateAsset(config, DefaultConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static bool HasUnsavedScenes()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
