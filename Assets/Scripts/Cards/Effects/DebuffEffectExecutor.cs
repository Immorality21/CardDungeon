using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DebuffEffectExecutor : IEffectExecutor
    {
        private static readonly Color DebuffColor = new Color(0.8f, 0.2f, 0.8f);
        private const float EffectDelay = 0.2f;

        /// <summary>Floated when a debuff fails its <see cref="SpellEffect.ApplyChance"/>.</summary>
        public const string UnaffectedText = "Unaffected";

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var handler = BuffHandlerRegistry.Get(effect.BuffType);
            if (handler == null)
            {
                // No handler for this BuffType - inert rather than a crash mid-combat.
                // BuffHandlerRegistry.Unhandled() is what surfaces these to the analyzer.
                return;
            }

            // Flat-power debuffs stay as authored; a cast one adds a fraction of the caster's
            // stat, so a high-Spirit caster's shields are better without dwarfing the stat changed.
            // A revisit's Fear level scales the base of a damage-over-time effect an enemy lays on
            // (a poison, a burn) the way it scales the enemy's spells. Only the base: the caster's
            // stat part below already rides the scaled stats. Stat debuffs are left alone - a -3
            // Endurance is not damage.
            int basePower = effect.Power;
            if (caster != null && !caster.IsHero && handler is IOverTimeBuffHandler overTime && !overTime.Heals)
            {
                basePower = RunFear.Current.ScaleEnemyOverTimePower(basePower);
            }

            int magnitude = flatPower
                ? basePower
                : basePower + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);

            // A debuff authored at 0% is dormant until an upgrade raises its odds (the Auto Turret's
            // blinding rounds): it does nothing and says nothing.
            if (effect.ApplyChance <= 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                // Said on screen, or an immune target reads as a cast that silently did nothing.
                if (StatusImmunity.IsImmune(target, effect.BuffType))
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = "Immune",
                        Color = Color.gray,
                        Delay = EffectDelay
                    });
                    continue;
                }

                // Every debuff is a chance, not a certainty (AfflictionRules). A failed rider on a hit
                // that already landed (Slash's bleed, Sunder's Endurance cut) passes quietly - the
                // damage number is the news, and the missing icon says the rest. A cast that was
                // only the debuff says so, or it reads as having done nothing.
                if (!AfflictionRules.Lands(effect.ApplyChance))
                {
                    if (!WasStruck(result, target))
                    {
                        result.Entries.Add(new EffectEntry
                        {
                            Target = target,
                            Text = UnaffectedText,
                            Color = Color.gray,
                            Delay = EffectDelay
                        });
                    }
                    continue;
                }

                handler.Apply(target, -magnitude, effect.Duration, buffTracker, caster);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = handler.GetDisplayText(-magnitude),
                    Color = DebuffColor,
                    Delay = EffectDelay
                });
            }
        }

        /// <summary>Whether this cast has already hit <paramref name="target"/> for damage.</summary>
        private static bool WasStruck(EffectResult result, ICombatUnit target)
        {
            foreach (var entry in result.Entries)
            {
                if (ReferenceEquals(entry.Target, target) && entry.Impact > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
