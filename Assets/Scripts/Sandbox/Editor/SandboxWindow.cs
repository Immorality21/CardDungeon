using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Sandbox.Editor
{
    /// <summary>
    /// <c>Tools ▸ Sandbox</c>: edit a <see cref="SandboxConfigSO"/> and launch it. The config is an
    /// ordinary asset, so several can exist side by side (one per hero being built, say) and the
    /// window simply remembers the last one used.
    /// </summary>
    public class SandboxWindow : EditorWindow
    {
        private const string LastConfigKey = "CardDungeon.Sandbox.LastConfigPath";

        private SandboxConfigSO _config;
        private UnityEditor.Editor _configEditor;
        private Vector2 _scroll;
        private readonly Dictionary<int, int> _nodePick = new Dictionary<int, int>();

        [MenuItem("Tools/Sandbox")]
        public static void Open()
        {
            GetWindow<SandboxWindow>("Sandbox").Show();
        }

        private void OnEnable()
        {
            var path = EditorPrefs.GetString(LastConfigKey, SandboxLauncher.DefaultConfigPath);
            _config = AssetDatabase.LoadAssetAtPath<SandboxConfigSO>(path);
        }

        private void OnDisable()
        {
            if (_configEditor != null)
            {
                DestroyImmediate(_configEditor);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Runs MainGameScene with this party, floor and encounter against a throwaway save " +
                "folder (savedata_sandbox). Your real save is only read, never written.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                var picked = (SandboxConfigSO)EditorGUILayout.ObjectField("Config", _config, typeof(SandboxConfigSO), false);
                if (picked != _config)
                {
                    SetConfig(picked);
                }
                if (GUILayout.Button("Default", GUILayout.Width(70)))
                {
                    SetConfig(SandboxLauncher.GetOrCreateDefault());
                }
            }

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Pick a config, or press Default to create one.", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("▶  Play Sandbox", GUILayout.Height(30)))
                {
                    AssetDatabase.SaveAssetIfDirty(_config);
                    if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        SandboxLauncher.Launch(_config);
                    }
                }
            }
            if (EditorApplication.isPlaying && SandboxSession.IsActive)
            {
                EditorGUILayout.HelpBox($"Sandbox running: {SandboxSession.Config?.name}", MessageType.None);
            }
            if (GUILayout.Button("Open sandbox save folder"))
            {
                System.IO.Directory.CreateDirectory(SandboxSession.Directory);
                EditorUtility.RevealInFinder(SandboxSession.Directory);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            UnityEditor.Editor.CreateCachedEditor(_config, null, ref _configEditor);
            _configEditor.OnInspectorGUI();
            DrawNodeHelper();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Node keys are save identifiers nobody remembers, so offer each hero's grid as a list:
        /// pick a node, press Add, and its key lands in that hero's Unlock Path To.
        /// </summary>
        private void DrawNodeHelper()
        {
            var heroes = _config.Heroes;
            if (heroes == null || heroes.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unlock a branch", EditorStyles.boldLabel);
            for (int i = 0; i < heroes.Count; i++)
            {
                var setup = heroes[i];
                var grid = setup != null && setup.Hero != null ? setup.Hero.SphereGrid : null;
                if (grid == null)
                {
                    continue;
                }

                var depths = SphereGridOps.DepthsFrom(grid);
                var nodes = grid.Nodes
                    .Where(n => n != null && !string.IsNullOrEmpty(n.Key))
                    .OrderBy(n => depths.TryGetValue(n.Key, out var d) ? d : int.MaxValue)
                    .ToList();
                var labels = nodes.Select(n =>
                    $"{(depths.TryGetValue(n.Key, out var d) ? d.ToString() : "?")}  {n.DisplayName}  ({n.Key})").ToArray();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(setup.Hero.DisplayName, GUILayout.Width(90));
                    _nodePick.TryGetValue(i, out var index);
                    _nodePick[i] = EditorGUILayout.Popup(Mathf.Clamp(index, 0, Mathf.Max(0, labels.Length - 1)), labels);
                    if (GUILayout.Button("Add", GUILayout.Width(45)) && nodes.Count > 0)
                    {
                        Undo.RecordObject(_config, "Add sandbox unlock target");
                        var key = nodes[_nodePick[i]].Key;
                        if (!setup.UnlockPathTo.Contains(key))
                        {
                            setup.UnlockPathTo.Add(key);
                        }
                        EditorUtility.SetDirty(_config);
                    }
                }
            }
            EditorGUILayout.LabelField("Number = depth from the start node.", EditorStyles.miniLabel);
        }

        private void SetConfig(SandboxConfigSO config)
        {
            _config = config;
            if (config != null)
            {
                EditorPrefs.SetString(LastConfigKey, AssetDatabase.GetAssetPath(config));
            }
        }
    }
}
