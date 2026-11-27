using System.Collections.Generic;
using Assets.Scripts.IO;
using ImmoralityGaming.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// How big the interface is drawn: the player's <b>Interface size</b> in Options, saved to
    /// <c>savedata/Display.json</c> (a device preference, like <c>Audio.json</c>).
    ///
    /// <para>Every screen shares one <c>PanelSettings</c> that scales a 1600×900 layout to the
    /// screen. That is right on a monitor and far too small on a phone held at arm's length, so a
    /// size above 100% shrinks the <i>reference</i> resolution instead: at 130% the layout is drawn as
    /// if the screen were 1231×692, which makes every text and button 1.3× larger. Nothing in the UXML
    /// changes; the screens just have less room, which is why the steps stop at 150%.</para>
    ///
    /// <para><b>A window that no longer fits shrinks back</b> (<see cref="global::ImmoralityGaming.Menu.FitToScreen"/>, attached to
    /// every <c>.cd-window</c> when a scene loads): the dense ones laid out in fixed pixels, like the
    /// inventory, are drawn as large as the screen allows, while the rest keep the full size. That is
    /// also the safety net for a wide phone at 100%, which is shorter in layout units than 900.</para>
    ///
    /// <para>The first launch on a phone or tablet (<c>Application.isMobilePlatform</c>, also true in
    /// a mobile browser) starts at <see cref="MobileDefault"/>; everywhere else at 100%.</para>
    ///
    /// <para>The reference resolution is changed on the <c>PanelSettings</c> asset itself, so in the
    /// editor it is put back when play mode ends. The original size is remembered in
    /// <c>SessionState</c>, which survives a script recompile in play mode; without that, a recompile
    /// lost the original, left the asset shrunk, and the next play shrank it again from there. Every
    /// domain reload outside play mode also puts back anything a lost play session left behind.</para>
    /// </summary>
    public static class InterfaceScale
    {
        /// <summary>The sizes on offer, in percent, in the order the Options button cycles them.</summary>
        public static readonly int[] Steps = { 100, 115, 130, 150 };

        public const int DesktopDefault = 100;
        public const int MobileDefault = 130;

        private static DisplayOptionsSaveData _data;
        private static readonly Dictionary<PanelSettings, Vector2Int> BaseResolutions = new Dictionary<PanelSettings, Vector2Int>();

        private static DisplayOptionsSaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = new FileHandler().Load<DisplayOptionsSaveData>();
                    _data.InterfaceSize = Clamp(_data.InterfaceSize, Application.isMobilePlatform);
                }
                return _data;
            }
        }

        /// <summary>The size the interface is drawn at, in percent.</summary>
        public static int Current => Data.InterfaceSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            _data = null;
            BaseResolutions.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
            FitWindows(scene);
        }

        /// <summary>Lets every window in the scene's documents shrink to fit the screen.</summary>
        private static void FitWindows(Scene scene)
        {
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document == null || document.gameObject.scene != scene || document.rootVisualElement == null)
                {
                    continue;
                }
                document.rootVisualElement.Query<VisualElement>(className: "cd-window").ForEach(global::ImmoralityGaming.Menu.FitToScreen.Attach);
            }
        }

        /// <summary>Moves one size smaller (<paramref name="direction"/> -1) or larger (+1), stopping
        /// at either end, saves it and redraws at once. No wrap: cycling 130 -> 150 -> 100 put a phone
        /// through 150%, where the setting itself ran off the screen (owner, 2026-11-27).</summary>
        public static int Step(int direction)
        {
            Data.InterfaceSize = Stepped(Data.InterfaceSize, direction);
            new FileHandler().Save(Data);
            Apply();
            return Data.InterfaceSize;
        }

        /// <summary>Drops the cached setting so the next read comes off disk (after a save import).</summary>
        public static void Invalidate()
        {
            _data = null;
        }

        /// <summary>Sets every loaded panel's reference resolution for the current size.</summary>
        public static void Apply()
        {
            float factor = Current / 100f;
            foreach (var settings in UnityEngine.Resources.FindObjectsOfTypeAll<PanelSettings>())
            {
                if (settings == null || settings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
                {
                    continue;
                }
                if (!BaseResolutions.TryGetValue(settings, out var baseResolution))
                {
                    baseResolution = OriginalResolution(settings);
                    BaseResolutions[settings] = baseResolution;
                }
                settings.referenceResolution = Scaled(baseResolution, factor);
            }
        }

        /// <summary>The reference resolution that draws <paramref name="baseResolution"/>'s layout
        /// <paramref name="factor"/> times larger.</summary>
        public static Vector2Int Scaled(Vector2Int baseResolution, float factor)
        {
            if (factor <= 0f)
            {
                factor = 1f;
            }
            return new Vector2Int(Mathf.RoundToInt(baseResolution.x / factor), Mathf.RoundToInt(baseResolution.y / factor));
        }

        /// <summary>The size one step from <paramref name="size"/> towards <paramref name="direction"/>,
        /// held at the smallest and largest; anything unknown starts from the smallest.</summary>
        public static int Stepped(int size, int direction)
        {
            int index = System.Array.IndexOf(Steps, size);
            if (index < 0)
            {
                return Steps[0];
            }
            int next = Mathf.Clamp(index + System.Math.Sign(direction), 0, Steps.Length - 1);
            return Steps[next];
        }

        /// <summary>Whether a step towards <paramref name="direction"/> would change the size.</summary>
        public static bool CanStep(int size, int direction)
        {
            return Stepped(size, direction) != size;
        }

        /// <summary>A stored size outside the steps (no file yet, or a hand-edited one) becomes the
        /// device's default.</summary>
        public static int Clamp(int size, bool mobile)
        {
            foreach (int step in Steps)
            {
                if (step == size)
                {
                    return size;
                }
            }
            return mobile ? MobileDefault : DesktopDefault;
        }

        /// <summary>What the Options button says: "130%".</summary>
        public static string Label(int size)
        {
            return Loc.Format("menu.options.interface-size-value", ("size", size));
        }

        /// <summary>The panel's authored reference resolution: the live value the first time, and in
        /// the editor the value remembered from earlier in the editor session, since a recompile in
        /// play mode forgets <see cref="BaseResolutions"/> while the asset stays changed.</summary>
        private static Vector2Int OriginalResolution(PanelSettings settings)
        {
#if UNITY_EDITOR
            var key = SessionKey(settings);
            if (key != null)
            {
                var stored = UnityEditor.SessionState.GetString(key, string.Empty);
                if (TryParse(stored, out var remembered))
                {
                    return remembered;
                }
                var current = settings.referenceResolution;
                UnityEditor.SessionState.SetString(key, $"{current.x},{current.y}");
                return current;
            }
#endif
            return settings.referenceResolution;
        }

        private static bool TryParse(string text, out Vector2Int value)
        {
            value = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            var parts = text.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) || x <= 0 || y <= 0)
            {
                return false;
            }
            value = new Vector2Int(x, y);
            return true;
        }

#if UNITY_EDITOR
        private const string SessionPrefix = "InterfaceScale.Original:";
        private const string SessionIndex = "InterfaceScale.Assets";

        private static string SessionKey(PanelSettings settings)
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            var index = UnityEditor.SessionState.GetString(SessionIndex, string.Empty);
            if (!("|" + index + "|").Contains("|" + path + "|"))
            {
                UnityEditor.SessionState.SetString(SessionIndex, string.IsNullOrEmpty(index) ? path : index + "|" + path);
            }
            return SessionPrefix + path;
        }

        /// <summary>Every domain reload: listen for play mode ending, and outside play mode put back
        /// any panel a play session changed and could not restore (it was recompiled mid-play).</summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInstall()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreAll();
            }
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode || change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                RestoreAll();
            }
        }

        private static void RestoreAll()
        {
            var index = UnityEditor.SessionState.GetString(SessionIndex, string.Empty);
            if (!string.IsNullOrEmpty(index))
            {
                foreach (var path in index.Split('|'))
                {
                    var key = SessionPrefix + path;
                    var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
                    if (settings != null && TryParse(UnityEditor.SessionState.GetString(key, string.Empty), out var original))
                    {
                        settings.referenceResolution = original;
                    }
                    UnityEditor.SessionState.EraseString(key);
                }
                UnityEditor.SessionState.EraseString(SessionIndex);
            }
            BaseResolutions.Clear();
        }
#endif
    }

    /// <summary>
    /// Display preferences on disk (<c>savedata/Display.json</c>). 0 means "not chosen yet", which
    /// <see cref="InterfaceScale.Clamp"/> turns into the device's default.
    /// </summary>
    [System.Serializable]
    public class DisplayOptionsSaveData : IWriteable
    {
        public int InterfaceSize;

        public string GetFileName()
        {
            return "Display";
        }
    }
}
