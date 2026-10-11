using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// The Rogue's Steal (<see cref="SpellEffectType.Steal"/>). Rolls <see cref="StealOps.Chance"/> per
    /// living enemy target and, on a success, asks the fight to hand over the goods
    /// (<see cref="EffectResolver.StealFrom"/>), which also enforces once-per-enemy and answers with the
    /// text to float. With no fight (tests, the balance model) a success floats "Stolen!" and takes nothing.
    /// </summary>
    public class StealEffectExecutor : IEffectExecutor
    {
        private static readonly Color StolenColor = new Color(1f, 0.85f, 0.3f);
        private const float EffectDelay = 0.2f;

        private readonly Func<Func<ICombatUnit, ICombatUnit, string>> _stealFrom;
        private readonly Func<Func<float>> _roll;

        public StealEffectExecutor(Func<Func<ICombatUnit, ICombatUnit, string>> stealFrom, Func<Func<float>> roll)
        {
            _stealFrom = stealFrom;
            _roll = roll;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var roll = _roll?.Invoke() ?? (() => UnityEngine.Random.Range(0f, 1f));
            var stealFrom = _stealFrom?.Invoke();
            int luck = caster == null ? 0
                : caster.GetEffectiveStat(StatType.Luck) + (buffTracker != null ? buffTracker.GetBuffAmount(caster, StatType.Luck) : 0);

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive || target == caster)
                {
                    continue;
                }

                float chance = StealOps.Chance(luck, UnitTraitOps.IsBoss(target), effect.Power);
                if (roll() >= chance)
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"Steal failed ({Mathf.RoundToInt(chance * 100f)}%)",
                        Color = Color.gray,
                        Delay = EffectDelay
                    });
                    continue;
                }

                string text = stealFrom != null ? stealFrom(caster, target) : "Stolen!";
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = text,
                    Color = StolenColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
