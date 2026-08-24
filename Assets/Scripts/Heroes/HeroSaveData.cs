using System;
using System.Collections.Generic;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class HeroSaveData
    {
        public string HeroKey;

        /// <summary>
        /// Unspent XP bank, drawn down by sphere-grid activations at the hub. Pre-grid saves stored
        /// lifetime XP here; that rename-in-meaning is the whole migration — old lifetime XP arrives
        /// as a full refund into the bank, with no activated nodes.
        /// </summary>
        public int CurrentXp;

        /// <summary>Keys of activated sphere-grid nodes, in activation order. A pre-grid save
        /// deserializes with this empty (FromJsonOverwrite keeps the field initializer).</summary>
        public List<string> ActivatedNodes = new List<string>();
    }
}
