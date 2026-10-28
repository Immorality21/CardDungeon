using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// What the HP plates <i>show</i>, which can briefly lag what the units <i>have</i>. A cast is
    /// resolved before it is presented, so its damage is already on every bar when its projectiles set
    /// off: the Floating Eye's party-wide Water Splash dropped every hero's plate at once and then flew
    /// its bolts at bars that had already moved (playtest 3 finding 3).
    ///
    /// <para>The cast path calls <see cref="Hold"/> just before it resolves, which freezes each unit's
    /// shown health at its current value; the presenter calls <see cref="Release"/> for a target as its
    /// hit lands, and <see cref="ReleaseAll"/> when the cast is done. A held unit's status icons
        /// stay as they were too (<see cref="IsHeld"/>), so a debuff shows as its hit lands. Presentation only - nothing in the
    /// rules reads it, so the simulator never needs to know.</para>
    /// </summary>
    public static class HealthDisplay
    {
        private static readonly Dictionary<ICombatUnit, int> Held = new Dictionary<ICombatUnit, int>();

        /// <summary>Freezes each unit's shown health where it stands now, until it is released.</summary>
        public static void Hold(IEnumerable<ICombatUnit> units)
        {
            if (units == null)
            {
                return;
            }
            foreach (var unit in units)
            {
                if (unit != null && unit.Stats != null)
                {
                    Held[unit] = unit.Stats.Health;
                }
            }
        }

        /// <summary>The unit's plate catches up with its real health.</summary>
        public static void Release(ICombatUnit unit)
        {
            if (unit != null)
            {
                Held.Remove(unit);
            }
        }

        public static void ReleaseAll()
        {
            Held.Clear();
        }

        /// <summary>True while the unit's plate is frozen at its pre-cast state. The status icons
        /// wait for it too: a debuff riding on a bolt used to appear before the bolt set off.</summary>
        public static bool IsHeld(ICombatUnit unit)
        {
            return unit != null && Held.ContainsKey(unit);
        }

        /// <summary>The health a plate should draw: the held value while one is held, else the real one.</summary>
        public static int Shown(ICombatUnit unit)
        {
            if (unit == null || unit.Stats == null)
            {
                return 0;
            }
            return Held.TryGetValue(unit, out int held) ? held : unit.Stats.Health;
        }
    }
}
