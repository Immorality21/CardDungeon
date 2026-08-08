using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectEntry
    {
        public ICombatUnit Target;
        public string Text;
        public Color Color;
        public float Delay;
        public Vector3 PositionOffset;
    }

    public class EffectResult
    {
        public List<EffectEntry> Entries = new List<EffectEntry>();
        public string ComboName;

        // Keys of every combo that triggered this cast — CombatManager records these as
        // discovered. Distinct from ComboName (display-only, last combo).
        public List<string> TriggeredComboKeys = new List<string>();

        public string BuildLog(SpellcastAction action)
        {
            var log = $"{action.Caster.DisplayName} casts {action.Magic.DisplayName}!";
            if (!string.IsNullOrEmpty(ComboName))
            {
                log += $" COMBO: {ComboName}!";
            }
            return log;
        }
    }
}
