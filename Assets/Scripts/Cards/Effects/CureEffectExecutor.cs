using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// <see cref="SpellEffectType.Cure"/>: clears debuffs off each standing target - curable statuses and
    /// stat cuts (<see cref="CombatBuffTracker.CureDebuffs"/>), at most <c>Power</c> of them, or every one
    /// when <c>Power</c> is 0. Miracle of Light, and the awakened Benediction's "one debuff each".
    /// </summary>
    public class CureEffectExecutor : IEffectExecutor
    {
        private static readonly Color CureColor = new Color(0.75f, 1f, 0.85f);
        private const float EffectDelay = 0.25f;

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            if (targets == null || buffTracker == null)
            {
                return;
            }
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                int cleared = buffTracker.CureDebuffs(target, effect.Power);
                if (cleared <= 0)
                {
                    continue;   // nothing on them: a grey line per clean ally says nothing
                }
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = cleared == 1 ? Loc.Get("combat.float.cleansed") : Loc.Format("combat.float.cleansed-many", ("count", cleared)),
                    Color = CureColor,
                    Delay = EffectDelay,
                    PositionOffset = Vector3.up * 0.2f
                });
            }
        }
    }
}
