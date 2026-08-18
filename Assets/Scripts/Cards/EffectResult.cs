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

        // Damage dealt by this entry (0 for non-damage). Drives the hit flash / camera shake
        // in the presenter without coupling the (unit-tested) executors to the feedback layer.
        public int Impact;

        // How resistance affected this hit (Weak/Resisted/Immune/…) — the presenter turns a
        // non-Normal value into a coloured popup. Default Normal for non-damage entries.
        public DamageEffectiveness Effectiveness = DamageEffectiveness.Normal;
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
