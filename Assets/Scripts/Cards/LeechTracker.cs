using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The Warlock's Leeching Curse, off one fight's <see cref="CombatEvents"/>: whenever a blow or an
    /// ability from the curser's side takes health off a <see cref="BuffType.Leeched"/> unit, the curser
    /// heals the curse's percent of what landed (at least 1). Ticks, prices and reactions do not feed it,
    /// and a fallen curser gets nothing. The heal is a reaction (<see cref="HealthOps.ReactionScope"/>),
    /// announced as a <see cref="ReactionResolved"/> for the log and the floating text.
    ///
    /// <para>Built per fight by its owner - <c>CombatManager.RunCombat</c> and the simulator's encounter
    /// loop - beside the <see cref="RetaliationTracker"/>.</para>
    /// </summary>
    public sealed class LeechTracker
    {
        private static readonly Color LeechColor = new Color(0.75f, 0.2f, 0.35f);

        private readonly CombatEvents _events;
        private readonly CombatBuffTracker _buffs;
        private readonly TriggeredEffect _label = new TriggeredEffect { LabelKey = "abilities.reaction.leeching-curse" };

        public LeechTracker(CombatEvents events, CombatBuffTracker buffs)
        {
            _events = events;
            _buffs = buffs;
            if (_events != null)
            {
                _events.Subscribe<HealthChange>(OnHealthChange, TriggerRegistry.Order);
            }
        }

        /// <summary>What a curse of <paramref name="percent"/> gives back for a hit that took
        /// <paramref name="landed"/>: rounded down, at least 1 when anything landed.</summary>
        public static int Leeched(int landed, int percent)
        {
            if (landed <= 0 || percent <= 0)
            {
                return 0;
            }
            return Mathf.Max(1, Mathf.FloorToInt(landed * percent / 100f));
        }

        private void OnHealthChange(HealthChange change)
        {
            var target = change.Target;
            var attacker = change.Attacker;
            if (target == null || attacker == null || change.Source.Reaction || change.Landed <= 0)
            {
                return;
            }
            if (change.Cause != HealthCause.Attack && change.Cause != HealthCause.Ability)
            {
                return;
            }
            var curse = _buffs?.GetStatus(target, BuffType.Leeched);
            var curser = curse?.Source;
            if (curser == null || !curser.IsAlive || attacker.IsHero != curser.IsHero)
            {
                return;
            }

            int heal = RunFear.Current.ScaleHealing(curser, Leeched(change.Landed, curse.Amount));
            HealthChange healed;
            using (HealthOps.ReactionScope())
            {
                healed = HealthOps.Heal(curser, heal, new HealthSource(curser, HealthCause.Ability), _events);
            }
            if (healed.Healed <= 0)
            {
                return;
            }

            var result = new EffectResult();
            result.Entries.Add(new EffectEntry
            {
                Target = curser,
                Text = $"+{healed.Healed}",
                Healed = healed.Healed,
                Color = LeechColor
            });
            _events.Publish(new ReactionResolved
            {
                Bearer = curser,
                Trigger = _label,
                From = _label.Label,
                Targets = new System.Collections.Generic.List<ICombatUnit> { curser },
                Result = result
            });
        }
    }
}
