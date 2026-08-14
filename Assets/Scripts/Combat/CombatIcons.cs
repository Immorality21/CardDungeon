using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Loads and caches the neutral (white) combat glyphs from Resources/CombatIcons.
    /// They are tinted/flipped at use to represent status effects and enemy intent.
    /// </summary>
    public static class CombatIcons
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (_cache.TryGetValue(name, out var sprite))
            {
                return sprite;
            }
            sprite = UnityEngine.Resources.Load<Sprite>("CombatIcons/" + name);
            _cache[name] = sprite;
            return sprite;
        }
    }
}
