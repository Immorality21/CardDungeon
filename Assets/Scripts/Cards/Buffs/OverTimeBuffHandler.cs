using System;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// Applies a timed effect that acts every turn — burn, poison, bleed, regeneration.
    ///
    /// <para>One parameterised class registered several times, the same shape
    /// <see cref="ResistanceBuffHandler"/> and <see cref="StatBuffHandler"/> already use: the four
    /// over-time effects differ only in their element, whether they bypass defense and which way
    /// they move health, so four near-identical classes would be four places to fix a bug.</para>
    ///
    /// <para><b>The power arrives signed and is used as a magnitude.</b>
    /// <see cref="Effects.DebuffEffectExecutor"/> negates before calling, so a poison authored as a
    /// Debuff arrives negative and a regeneration authored as a Buff arrives positive — both mean
    /// "this much per turn". Direction is <see cref="Heals"/>, never the sign, so neither authoring
    /// choice can produce a poison that heals.</para>
    /// </summary>
    public class OverTimeBuffHandler : IBuffHandler, IOverTimeBuffHandler
    {
        private readonly BuffType _type;
        private readonly string _displayName;
        private readonly bool _hasRemover;
        private readonly DamageType _removedBy;

        public OverTimeBuffHandler(
            BuffType type,
            string displayName,
            bool heals,
            DamageType tickDamageType,
            bool ignoresDefense,
            DamageType? removedBy = null)
        {
            _type = type;
            _displayName = displayName;
            Heals = heals;
            TickDamageType = tickDamageType;
            IgnoresDefense = ignoresDefense;
            TickLabel = displayName;
            _hasRemover = removedBy.HasValue;
            _removedBy = removedBy ?? DamageType.Normal;
        }

        public bool Heals { get; private set; }

        public DamageType TickDamageType { get; private set; }

        public bool IgnoresDefense { get; private set; }

        public string TickLabel { get; private set; }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            if (target == null || buffTracker == null)
            {
                return;
            }

            int perTurn = Math.Abs(power);
            if (perTurn <= 0 || duration <= 0)
            {
                // A zero-power or zero-duration over-time effect is authoring noise, not an effect.
                // Applying it would put an inert row on the status strip that never does anything.
                return;
            }

            buffTracker.ApplyOverTime(target, _type, perTurn, duration);
        }

        public string GetDisplayText(int power)
        {
            return $"{_displayName} {Math.Abs(power)}/turn";
        }

        /// <summary>
        /// False for every over-time effect. Losing the turn <i>and</i> taking damage every turn is
        /// two effects; <see cref="FrozenBuffHandler"/> is the one that stops a unit acting.
        /// </summary>
        public bool SkipsTurn => false;

        public string GetSkipTurnMessage(ICombatUnit unit)
        {
            return null;
        }

        /// <summary>
        /// Mirrors <see cref="FrozenBuffHandler"/>, which fire thaws: a burn is doused by Ice. Only
        /// Burning declares a remover — poison and bleeding are not elemental states, and a
        /// regeneration the enemy could cancel by hitting you would be worse than no regeneration.
        /// </summary>
        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return _hasRemover && damageType == _removedBy;
        }
    }
}
