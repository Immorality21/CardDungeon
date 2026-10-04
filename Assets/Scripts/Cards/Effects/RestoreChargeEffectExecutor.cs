using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// Gives <c>Power</c> charges back to one ability each target carries - Life Tap. Which ability
    /// is the cast's <see cref="SpellcastAction.ChargeSlot"/> (the player's pick) when it lands on one
    /// unit, or each target's most-spent ability otherwise. The slot the cast comes out of
    /// (<see cref="SpellcastAction.CastSlot"/>) is never refilled: its charge is spent after the
    /// effects resolve, so refilling it would make the cast free.
    ///
    /// <para>Charges are a <b>run</b> resource that only a refuge refills, so this is a strong effect
    /// and is priced in health where it is authored. The bank is the fight's, handed in by the
    /// resolver that owns it; through a resolver with none (a room event, the balance model) it is
    /// inert and says nothing. A target that carries no abilities (a summon) is passed over quietly.</para>
    /// </summary>
    public class RestoreChargeEffectExecutor : ICastAwareEffectExecutor
    {
        private static readonly Color RestoreColor = new Color(0.55f, 0.75f, 1f);
        private const float EffectDelay = 0.3f;

        private readonly Func<IChargeBank> _bank;

        public RestoreChargeEffectExecutor(Func<IChargeBank> bank)
        {
            _bank = bank;
        }

        public void Execute(
            SpellEffect effect,
            SpellcastAction action,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result)
        {
            Restore(effect, action != null ? action.Caster : null, targets, result,
                action != null ? action.ChargeSlot : SpellcastAction.AnyChargeSlot,
                action != null ? action.CastSlot : -1);
        }

        /// <summary>Without a cast (a combo's bonus effect): the most-spent slot, nothing excluded.</summary>
        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            Restore(effect, caster, targets, result, SpellcastAction.AnyChargeSlot, -1);
        }

        private void Restore(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets, EffectResult result,
            int chosenSlot, int castSlot)
        {
            var bank = _bank != null ? _bank() : null;
            if (bank == null || effect.Power <= 0 || targets == null)
            {
                return;
            }

            // A pick names one unit's slot; spread over several targets it would mean a different
            // ability on each, so a party-wide restore always takes each target's most-spent one.
            bool single = targets.Count == 1;
            int slot = single ? chosenSlot : SpellcastAction.AnyChargeSlot;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive || !bank.Carries(target))
                {
                    continue;
                }

                int exclude = ReferenceEquals(target, caster) ? castSlot : -1;
                var restored = bank.Restore(target, slot, effect.Power, exclude);
                if (restored.Added <= 0 && !single)
                {
                    continue;   // one grey line per full ally across the party says nothing useful
                }
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = restored.Added > 0 && restored.Magic != null
                        ? $"{restored.Magic.DisplayName} +{restored.Added}"
                        : "Nothing to restore",
                    Color = restored.Added > 0 ? RestoreColor : Color.gray,
                    Delay = EffectDelay,
                    PositionOffset = Vector3.up * 0.2f
                });
            }
        }
    }
}
