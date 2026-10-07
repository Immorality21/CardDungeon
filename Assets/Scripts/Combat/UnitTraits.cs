using System;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// What a unit <i>is</i>, as opposed to what it does: a clockwork sentry is Mechanical, a lich is
    /// Undead. Authored on <c>EnemySO.Traits</c>; abilities read it to decide whether they apply at all
    /// (the Tinkerer's Disassemble takes apart only Mechanical units). Several at once are fine - a
    /// gargoyle is a Flying Construct.
    ///
    /// <para>Serialized as a bit mask: <b>append</b> a new trait with the next free bit, never reuse
    /// or reorder one, or every authored enemy changes what it is.</para>
    /// </summary>
    [Flags]
    public enum UnitTraits
    {
        None = 0,

        /// <summary>Gears, boilers, rivets: built, not born. Disassemble takes these apart.</summary>
        Mechanical = 1 << 0,

        /// <summary>Animated stone or clay - built, but by magic rather than by hand. Not Mechanical.</summary>
        Construct = 1 << 1,

        Undead = 1 << 2,
        Flying = 1 << 3,
        Beast = 1 << 4,
        Demon = 1 << 5,
        Elemental = 1 << 6
    }

    /// <summary>A unit whose <see cref="UnitTraits"/> can be read: <c>Enemy</c> and the balance model's
    /// <c>SimUnit</c>, both from <c>EnemySO.Traits</c>, so the game and the model agree by construction.
    /// A unit that does not implement it has no traits at all.</summary>
    public interface IHasTraits
    {
        UnitTraits Traits { get; }

        /// <summary>A boss is harder to affect by trait (half the Disassemble odds).</summary>
        bool IsBoss { get; }
    }

    public static class UnitTraitOps
    {
        /// <summary>Whether <paramref name="unit"/> has every trait in <paramref name="traits"/>.</summary>
        public static bool Has(ICombatUnit unit, UnitTraits traits)
        {
            return traits != UnitTraits.None && unit is IHasTraits bearer && (bearer.Traits & traits) == traits;
        }

        /// <summary>Whether <paramref name="unit"/> is a boss; a unit with no traits never is.</summary>
        public static bool IsBoss(ICombatUnit unit)
        {
            return unit is IHasTraits bearer && bearer.IsBoss;
        }
    }
}
