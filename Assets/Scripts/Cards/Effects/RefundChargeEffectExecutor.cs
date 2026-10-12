using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// The Arcane Circlet (<see cref="SpellEffectType.RefundCharge"/>): the cast its target is making
    /// costs no charge. Fired as an on-ability-used reaction, before the cast's charge is spent - which
    /// is why it marks the cast free rather than restoring a charge (a restore would land before the
    /// spend and miss). The fight keeps the mark (<see cref="EffectResolver.FreeCast"/>); inert without
    /// one, the balance model included.
    /// </summary>
    public class RefundChargeEffectExecutor : IEffectExecutor
    {
        private static readonly Color RefundColor = new Color(0.55f, 0.75f, 1f);

        private readonly Func<Func<ICombatUnit, bool>> _freeCast;

        public RefundChargeEffectExecutor(Func<Func<ICombatUnit, bool>> freeCast)
        {
            _freeCast = freeCast;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var freeCast = _freeCast?.Invoke();
            if (freeCast == null || targets == null)
            {
                return;
            }
            foreach (var target in targets)
            {
                if (target != null && freeCast(target))
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = "Charge kept",
                        Color = RefundColor,
                        Delay = 0.2f
                    });
                }
            }
        }
    }
}
