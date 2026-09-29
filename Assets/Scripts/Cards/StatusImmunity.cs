using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// A unit that shrugs off some status effects outright — a stone golem does not bleed. Implemented
    /// by <c>Enemy</c> and the balance model's <c>SimUnit</c>, both reading <c>EnemySO.StatusImmunities</c>,
    /// so the game and the model agree by construction.
    /// </summary>
    public interface IStatusImmune
    {
        bool IsImmuneTo(BuffType type);
    }

    /// <summary>
    /// The one immunity check. <see cref="CombatBuffTracker"/> consults it before recording a status or
    /// an over-time effect, so every path that lands one — casts, summons, items, the simulator — is
    /// covered by a single line; the effect executors consult it only to say "Immune" on screen.
    ///
    /// <para><b>Only status effects can be immune.</b> Stat changes (Strength down, Endurance up) and
    /// elemental resistance changes go through other tracker methods and are untouched: those are
    /// answered by numbers (Endurance, resistances), not by a yes/no.</para>
    /// </summary>
    public static class StatusImmunity
    {
        public static bool IsImmune(ICombatUnit unit, BuffType type)
        {
            return IsStatus(type) && unit is IStatusImmune immune && immune.IsImmuneTo(type);
        }

        /// <summary>
        /// The status half of <see cref="BuffType"/> — the only half an immunity can name. A stat or
        /// resistance type listed by mistake is ignored everywhere rather than half-honoured.
        /// </summary>
        public static bool IsStatus(BuffType type)
        {
            switch (type)
            {
                case BuffType.Frozen:
                case BuffType.Slow:
                case BuffType.Haste:
                case BuffType.Burning:
                case BuffType.Poisoned:
                case BuffType.Bleeding:
                case BuffType.Regenerating:
                case BuffType.Silenced:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Shared by the implementers: whether an authored list names <paramref name="type"/>.</summary>
        public static bool ListContains(List<BuffType> immunities, BuffType type)
        {
            return immunities != null && type != BuffType.None && immunities.Contains(type);
        }
    }
}
