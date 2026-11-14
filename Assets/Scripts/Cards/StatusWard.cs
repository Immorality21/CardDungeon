using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Protection against one affliction, carried by a piece of gear (<c>ItemSO.StatusWards</c>):
    /// 100 is immunity, anything less cuts the affliction's chance to land by that share
    /// (<see cref="AfflictionRules.Lands(int, Combat.ICombatUnit, BuffType)"/>). Wards from several
    /// pieces add up, capped at 100.
    /// </summary>
    [Serializable]
    public class StatusWard
    {
        public BuffType Status;

        [Range(0, 100)]
        [Tooltip("100 = immune. Less cuts the chance the affliction lands by that share: a 75% poison " +
                 "against a 50% ward lands 37.5% of the time.")]
        public int Percent = 100;
    }

    public static class StatusWards
    {
        public const int Immune = 100;

        /// <summary>
        /// The statuses a ward can name: the harmful ones. A ward against Haste or Regeneration would
        /// only refuse a friend's help, so it is ignored everywhere rather than half-honoured.
        /// </summary>
        public static bool IsAffliction(BuffType type)
        {
            switch (type)
            {
                case BuffType.Frozen:
                case BuffType.Slow:
                case BuffType.Burning:
                case BuffType.Poisoned:
                case BuffType.Bleeding:
                case BuffType.Silenced:
                case BuffType.Blinded:
                case BuffType.Leeched:
                case BuffType.Confused:
                case BuffType.Marked:
                case BuffType.DeathMarked:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The summed ward against <paramref name="type"/> over several lists, 0 to 100.</summary>
        public static int Total(IEnumerable<List<StatusWard>> lists, BuffType type)
        {
            if (lists == null || !IsAffliction(type))
            {
                return 0;
            }
            int total = 0;
            foreach (var list in lists)
            {
                if (list == null)
                {
                    continue;
                }
                foreach (var ward in list)
                {
                    if (ward != null && ward.Status == type && ward.Percent > 0)
                    {
                        total += ward.Percent;
                    }
                }
            }
            return Mathf.Clamp(total, 0, Immune);
        }

        /// <summary>Every affliction the lists ward against, with its summed share, in enum order.</summary>
        public static Dictionary<BuffType, int> Summed(IEnumerable<List<StatusWard>> lists)
        {
            var result = new Dictionary<BuffType, int>();
            if (lists == null)
            {
                return result;
            }
            foreach (var list in lists)
            {
                if (list == null)
                {
                    continue;
                }
                foreach (var ward in list)
                {
                    if (ward == null || ward.Percent <= 0 || !IsAffliction(ward.Status))
                    {
                        continue;
                    }
                    result.TryGetValue(ward.Status, out int sum);
                    result[ward.Status] = Mathf.Min(Immune, sum + ward.Percent);
                }
            }
            return result;
        }
    }
}
