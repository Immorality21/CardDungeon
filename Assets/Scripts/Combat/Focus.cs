using System;
using ImmoralityGaming.Localization;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The Mage's signature (his vision, 2026-10-10): he builds Focus during a fight and spends it on
    /// <b>MultiCast</b> - his Ultra - to cast his next ability twice, three or four times. Focus is built
    /// one point for every turn he casts an ability (<see cref="PerCast"/>), up to <see cref="Max"/>, and
    /// is kept for the fight; MultiCast spends all of it, casting the ability once plus once per point.
    /// How Focus is built and what each tier costs were open questions in the vision - these are the
    /// first draft, authored on the hero (<c>HeroSO.Focus</c>) and worked by <c>Cards.FocusTracker</c>.
    /// </summary>
    [Serializable]
    public class FocusRule
    {
        [Tooltip("Focus gained on each of the hero's turns on which they cast an ability. 0 = this hero " +
                 "has no Focus.")]
        [Min(0)] public int PerCast;

        [Tooltip("The most Focus one fight can build. MultiCast casts the ability once plus once per " +
                 "point spent, so 3 is a quadruple-cast.")]
        [Min(0)] public int Max;

        public bool IsActive => PerCast > 0 && Max > 0;
    }

    /// <summary>A unit that may build Focus: a hero (from their definition) and the simulator's stand-in.</summary>
    public interface IHasFocus
    {
        /// <summary>The unit's rule, or null / inactive for a unit without one.</summary>
        FocusRule Focus { get; }
    }

    /// <summary>The pure arithmetic of <see cref="FocusRule"/>. Covered by <c>MageKitTests</c>.</summary>
    public static class FocusOps
    {
        /// <summary>The Focus after a turn on which the bearer did (or did not) cast an ability.</summary>
        public static int Next(int focus, bool cast, FocusRule rule)
        {
            if (rule == null || !rule.IsActive)
            {
                return 0;
            }
            return cast ? Mathf.Clamp(focus + rule.PerCast, 0, rule.Max) : Mathf.Clamp(focus, 0, rule.Max);
        }

        /// <summary>How many times MultiCast casts the ability with <paramref name="focus"/> to spend:
        /// once, and once more per point. Fewer than 2 means MultiCast is not ready.</summary>
        public static int Casts(int focus)
        {
            return 1 + Mathf.Max(0, focus);
        }

        /// <summary>"Double-cast", "Triple-cast", "Quadruple-cast" - what spending <paramref name="focus"/> buys.</summary>
        public static string CastName(int focus)
        {
            switch (Casts(focus))
            {
                case 2:
                    return Loc.Get("combat.focus.double-cast");
                case 3:
                    return Loc.Get("combat.focus.triple-cast");
                case 4:
                    return Loc.Get("combat.focus.quadruple-cast");
                default:
                    return Casts(focus) < 2
                        ? Loc.Get("combat.focus.single-cast")
                        : Loc.Format("combat.focus.multi-cast", ("casts", Casts(focus)));
            }
        }
    }
}
