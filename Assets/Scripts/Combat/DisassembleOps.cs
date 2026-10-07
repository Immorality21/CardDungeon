using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The rule behind the Tinkerer's Disassemble (docs/plans/SPECIALIZATION.md, "The Tinkerer"): only a
    /// <see cref="UnitTraits.Mechanical"/> unit can be taken apart, and the odds grow with how many of
    /// that enemy the player has already defeated - the Bestiary's kill count, which survives death, so
    /// knowing a machine is what makes taking it apart reliable. Pure: the live fight and the
    /// simulator read the same numbers.
    /// </summary>
    public static class DisassembleOps
    {
        /// <summary>The chance against an enemy never defeated before.</summary>
        public const float BaseChance = 0.25f;

        /// <summary>Added per recorded kill of that enemy.</summary>
        public const float ChancePerKill = 0.05f;

        /// <summary>Never certain: the ceiling, reached at 13 kills.</summary>
        public const float MaxChance = 0.9f;

        /// <summary>A boss is twice as hard to take apart, at every kill count.</summary>
        public const float BossFactor = 0.5f;

        /// <summary>The chance to take apart a unit with <paramref name="kills"/> recorded kills.</summary>
        public static float Chance(int kills, bool isBoss)
        {
            float chance = Mathf.Min(MaxChance, BaseChance + ChancePerKill * Mathf.Max(0, kills));
            return isBoss ? chance * BossFactor : chance;
        }

        /// <summary>Whether Disassemble applies to <paramref name="unit"/> at all.</summary>
        public static bool CanTarget(ICombatUnit unit)
        {
            return unit != null && UnitTraitOps.Has(unit, UnitTraits.Mechanical);
        }

        /// <summary>The chance as the player reads it: "45%".</summary>
        public static string Percent(float chance)
        {
            return Mathf.RoundToInt(chance * 100f) + "%";
        }
    }
}
