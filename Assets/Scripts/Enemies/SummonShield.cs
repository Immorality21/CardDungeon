using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies
{
    /// <summary>An enemy body that knows what it is and whether something called it in: the live
    /// <c>Enemy</c> and the balance model's <c>SimUnit</c>, so both read the shield the same way.</summary>
    public interface ISummonedBody
    {
        EnemySO Definition { get; }
        bool IsSummoned { get; }
    }

    /// <summary>
    /// A boss with <see cref="EnemySO.ShieldedBySummons"/> cannot be targeted while anything called into
    /// the fight still stands (the Barrow King behind his guard). Pure, so the live fight and the
    /// simulator filter the same way.
    /// </summary>
    public static class SummonShield
    {
        /// <summary>Whether <paramref name="unit"/> is shielded among <paramref name="side"/>, its own side.</summary>
        public static bool IsShielded(ICombatUnit unit, IEnumerable<ICombatUnit> side)
        {
            if (!(unit is ISummonedBody body) || body.Definition == null || !body.Definition.ShieldedBySummons)
            {
                return false;
            }
            foreach (var other in side)
            {
                if (other != null && other.IsAlive && other is ISummonedBody called && called.IsSummoned)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary><paramref name="side"/> less every unit it shields.</summary>
        public static List<T> Targetable<T>(IList<T> side) where T : ICombatUnit
        {
            var result = new List<T>(side.Count);
            var asUnits = new List<ICombatUnit>(side.Count);
            foreach (var unit in side)
            {
                asUnits.Add(unit);
            }
            foreach (var unit in side)
            {
                if (unit != null && !IsShielded(unit, asUnits))
                {
                    result.Add(unit);
                }
            }
            return result;
        }
    }
}
