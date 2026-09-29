using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// When in the victim's own turn an over-time effect fires (2026-09-29; the rule is written up in the Cards guide).
    /// <b>Harm at the start, help at the end</b> — the split Slay the Spire, Darkest Dungeon and
    /// D&amp;D 5e all make. A lethal damage tick denies the action it would otherwise have got; a
    /// regeneration restores what this turn cost, so the unit starts its next turn topped up.
    /// Durations tick down at the end either way, so "3 turns" is still three ticks.
    /// </summary>
    public enum TickTiming
    {
        StartOfTurn = 0,
        EndOfTurn = 1,
    }

    /// <summary>
    /// A status effect that <i>acts on its own</i> each turn rather than only changing a stat or
    /// gating a command — a damage-over-time, or a regeneration.
    ///
    /// <para>Deliberately a <b>second</b> interface rather than four more members on
    /// <see cref="IBuffHandler"/>: <see cref="CombatBuffTracker.ResolveOverTime"/> asks for it with a
    /// cast, so every handler that does not tick needs no change at all.</para>
    ///
    /// <para>That resolver owns the arithmetic — the live turn loop and <c>EncounterSimulator</c>
    /// both call it rather than re-deriving a tick, which is the rule the temporary resistance bonus
    /// already learned the hard way (every damage path has to pass it, or the popup contradicts the
    /// number).</para>
    /// </summary>
    public interface IOverTimeBuffHandler
    {
        /// <summary>True to restore health rather than remove it.</summary>
        bool Heals { get; }

        /// <summary>
        /// Element the tick is dealt as, so innate, gear and buffed resistances all apply to it.
        /// Meaningless when <see cref="Heals"/> is true.
        /// </summary>
        DamageType TickDamageType { get; }

        /// <summary>
        /// True when the tick bypasses the target's Endurance.
        ///
        /// <para>This is the property that makes one damage-over-time mechanically different from
        /// another rather than a reskin. <see cref="DamageCalculator"/>'s diminishing curve makes a
        /// high-Endurance target progressively immune to flat damage, so a tick that ignores defense
        /// is the answer to a Stone Sentinel — and the reason to cast something other than the
        /// biggest number in the kit.</para>
        /// </summary>
        bool IgnoresDefense { get; }

        /// <summary>Short label for the floating tick number, e.g. "Burn".</summary>
        string TickLabel { get; }

        /// <summary>When in the victim's turn this fires. See <see cref="TickTiming"/>.</summary>
        TickTiming Timing { get; }
    }
}
