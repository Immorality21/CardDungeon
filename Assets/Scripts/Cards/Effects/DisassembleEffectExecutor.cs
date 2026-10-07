using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// The Tinkerer's Disassemble (<see cref="SpellEffectType.Disassemble"/>): each Mechanical target
    /// rolls once against <see cref="DisassembleOps.Chance"/> and, on a success, falls outright -
    /// through <see cref="HealthOps.Set"/>, so the kill is the caster's and the fight's death path
    /// pays it out (and its salvage, keyed off <see cref="HealthCause.Disassemble"/>). Anything that is
    /// not a machine is untouched and says so.
    ///
    /// <para>The kill count behind the odds comes from the resolver (<see cref="EffectResolver.KillsOf"/>,
    /// the Bestiary in the live fight); with none, every machine reads as never defeated. The roll is
    /// <see cref="EffectResolver.Roll"/>, swappable for tests.</para>
    /// </summary>
    public class DisassembleEffectExecutor : IEffectExecutor
    {
        private static readonly Color TakenApartColor = new Color(0.95f, 0.75f, 0.3f);
        private const float EffectDelay = 0.2f;

        private readonly Func<Func<ICombatUnit, int>> _killsOf;
        private readonly Func<Func<float>> _roll;
        private readonly Func<CombatEvents> _events;

        public DisassembleEffectExecutor(Func<Func<ICombatUnit, int>> killsOf, Func<Func<float>> roll, Func<CombatEvents> events)
        {
            _killsOf = killsOf;
            _roll = roll;
            _events = events;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var killsOf = _killsOf?.Invoke();
            var roll = _roll?.Invoke() ?? (() => UnityEngine.Random.Range(0f, 1f));

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                if (!DisassembleOps.CanTarget(target))
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = "Not a machine",
                        Color = Color.gray,
                        Delay = EffectDelay
                    });
                    continue;
                }

                int kills = killsOf != null ? killsOf(target) : 0;
                float chance = DisassembleOps.Chance(kills, UnitTraitOps.IsBoss(target));
                if (roll() >= chance)
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"Failed ({DisassembleOps.Percent(chance)})",
                        Color = Color.gray,
                        Delay = EffectDelay
                    });
                    continue;
                }

                int before = target.Stats.Health;
                HealthOps.Set(target, 0, new HealthSource(caster, HealthCause.Disassemble), _events?.Invoke());
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = "Disassembled!",
                    Color = TakenApartColor,
                    Delay = EffectDelay,
                    Impact = before,
                    Landed = before
                });
            }
        }
    }
}
