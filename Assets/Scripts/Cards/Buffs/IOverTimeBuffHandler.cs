using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
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
    }
}
