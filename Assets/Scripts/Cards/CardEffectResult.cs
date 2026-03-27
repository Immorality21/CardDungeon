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

    public class CardEffectResult
    {
        public List<EffectEntry> Entries = new List<EffectEntry>();
        public string ComboName;

        public string BuildLog(CardAction action)
        {
            var log = $"{action.Caster.DisplayName} plays {action.Card.DisplayName}!";
            if (!string.IsNullOrEmpty(ComboName))
            {
                log += $" COMBO: {ComboName}!";
            }
            return log;
        }
    }
}
