using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// <see cref="SpellEffectType.Execute"/>: each standing target at or under <c>Power</c> percent of
    /// its max health is destroyed outright, if it has the effect's <see cref="SpellEffect.BonusVsTrait"/>
    /// (any unit when that is None). Bosses never are. Credited to the caster as an ordinary kill.
    /// Miracle of Righteousness: Undead under 30% are destroyed after the holy blow lands.
    /// </summary>
    public class ExecuteEffectExecutor : IEffectExecutor
    {
        private static readonly Color DestroyedColor = new Color(1f, 0.95f, 0.6f);
        private const float EffectDelay = 0.3f;

        private readonly Func<CombatEvents> _events;

        public ExecuteEffectExecutor(Func<CombatEvents> events)
        {
            _events = events;
        }

        /// <summary>Whether <paramref name="target"/> would be destroyed by <paramref name="effect"/> right now.</summary>
        public static bool Destroys(SpellEffect effect, ICombatUnit target)
        {
            if (effect == null || target == null || !target.IsAlive || UnitTraitOps.IsBoss(target))
            {
                return false;
            }
            if (effect.BonusVsTrait != UnitTraits.None && !UnitTraitOps.Has(target, effect.BonusVsTrait))
            {
                return false;
            }
            int max = target.GetEffectiveStat(StatType.MaxHealth);
            return max > 0 && target.Stats.Health * 100 <= max * Mathf.Max(0, effect.Power);
        }

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            if (targets == null)
            {
                return;
            }
            foreach (var target in targets)
            {
                if (!Destroys(effect, target))
                {
                    continue;
                }
                int before = target.Stats.Health;
                HealthOps.Set(target, 0, new HealthSource(caster, HealthCause.Ability, effect.DamageType), _events?.Invoke());
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = Loc.Get("combat.float.destroyed"),
                    Color = DestroyedColor,
                    Delay = EffectDelay,
                    Impact = before,
                    Landed = before
                });
            }
        }
    }
}
