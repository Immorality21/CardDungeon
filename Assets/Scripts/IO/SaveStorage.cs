using UnityEngine;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// Picks the <see cref="ISaveStorage"/> and the root folder for the platform the game runs on.
    /// The only place that knows which platform keeps saves where.
    /// </summary>
    public static class SaveStorage
    {
        /// <summary>
        /// The browser build's fixed save root. Unity mounts all of <c>/idbfs</c> on IndexedDB and
        /// restores it before the first scene, so any folder under it survives a reload. The name is
        /// the game's own because itch.io serves every game from the same origin, which means the same
        /// IndexedDB.
        /// </summary>
        public const string WebGLRoot = "/idbfs/immoral-dungeon";

        private static ISaveStorage _current;

        /// <summary>The storage every <see cref="FileHandler"/> uses.</summary>
        public static ISaveStorage Current
        {
            get
            {
                if (_current == null)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    _current = new WebGLSaveStorage();
#else
                    _current = new LocalFileStorage();
#endif
                }
                return _current;
            }
        }

        /// <summary>The folder the save folders live in (<c>savedata</c>, <c>savedata_sandbox</c>, ...).</summary>
        public static string Root
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WebGLRoot;
#else
                return Application.persistentDataPath;
#endif
            }
        }
    }
}
