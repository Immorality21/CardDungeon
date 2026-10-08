using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The living lights on the painted menu backdrops - the title's torches, the level entry's
    /// sconces, the story map's lanterns - keyed by the backdrop's USS class (<c>cd-bg--title</c> ...).
    /// <c>Resources/BackdropAmbience.asset</c>, loaded like <see cref="HubSO"/>, so a screen needs no
    /// scene wiring to get its fires. Presentation only: <c>BackdropAmbienceView</c> draws it.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Hub/Backdrop Ambience", fileName = "BackdropAmbience")]
    public class BackdropAmbienceSO : ScriptableObject
    {
        public const string ResourcePath = "BackdropAmbience";

        [Serializable]
        public class Entry
        {
            [Tooltip("The USS class that paints this backdrop, e.g. cd-bg--title.")]
            public string BackdropClass = "";

            [Tooltip("The backdrop image's size in its own pixels. The overlay is laid out at 4 design " +
                     "pixels to each and cover-scaled exactly as -unity-background-scale-mode: " +
                     "scale-and-crop scales the image, so a light stays on its torch at any window size.")]
            public Vector2 ImageSize = new Vector2(384f, 216f);

            [Tooltip("Points in design pixels: 4 per backdrop pixel, from the image's top-left.")]
            public List<HubAmbientLight> Lights = new List<HubAmbientLight>();

            [Min(0)] public int TwinklingStars;

            [Tooltip("Where the stars may sit, in design pixels.")]
            public Rect StarField;
        }

        [Tooltip("The soft light every glow paints with (fx_glow).")]
        public Sprite GlowSprite;

        public List<Entry> Backdrops = new List<Entry>();

        /// <summary>The entry for a backdrop class, or null.</summary>
        public Entry Find(string backdropClass)
        {
            return string.IsNullOrEmpty(backdropClass)
                ? null
                : Backdrops.Find(e => e != null && e.BackdropClass == backdropClass);
        }

        private static BackdropAmbienceSO _loaded;

        /// <summary>The project's asset, or null when none is authored (the backdrops then stay still).</summary>
        public static BackdropAmbienceSO Load()
        {
            if (_loaded == null)
            {
                _loaded = UnityEngine.Resources.Load<BackdropAmbienceSO>(ResourcePath);
            }
            return _loaded;
        }
    }
}
