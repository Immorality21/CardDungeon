using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Who is shielding whom in one fight: a guarded unit's blows land on its guard instead, for as
    /// long as the guard stands. Two kinds of guard share it:
    /// <list type="bullet">
    /// <item><description>A <b>full guard</b> (<see cref="Set"/>) - the Tinkerer riding a mech
    /// (<c>UltraKind.Mount</c>). The mech's health takes everything aimed at her, area blows included,
    /// and once it breaks she is hit normally again.</description></item>
    /// <item><description>A <b>cover</b> (<see cref="Cover"/>) - an enemy's Guard action
    /// (COMBAT_DEPTH §12). Final Fantasy's Cover: it intercepts <b>single-target</b> blows only, so an
    /// area attack still reaches the covered unit, and it lasts until the guard's own next turn
    /// (<see cref="ExpireCovers"/>), when it may cover again or do something else.</description></item>
    /// </list>
    ///
    /// <para>Kept on <see cref="CombatEvents"/> so the live fight and the simulator share it the way they
    /// share the stream. Consulted where a hit picks its victim - a basic attack and an ability's damage -
    /// <b>before</b> the damage is worked out, so the guard's own defences apply. Over-time ticks are not
    /// redirected: a poison already in her stays hers.</para>
    /// </summary>
    public sealed class GuardTable
    {
        private struct Entry
        {
            public ICombatUnit Guard;
            public bool IsCover;
        }

        private readonly Dictionary<ICombatUnit, Entry> _guardOf = new Dictionary<ICombatUnit, Entry>();

        /// <summary><paramref name="guard"/> takes every one of <paramref name="guarded"/>'s blows from now on.</summary>
        public void Set(ICombatUnit guarded, ICombatUnit guard)
        {
            Add(guarded, guard, isCover: false);
        }

        /// <summary>
        /// <paramref name="guard"/> takes the single-target blows aimed at <paramref name="guarded"/>
        /// until the guard's next turn begins.
        /// </summary>
        public void Cover(ICombatUnit guarded, ICombatUnit guard)
        {
            Add(guarded, guard, isCover: true);
        }

        private void Add(ICombatUnit guarded, ICombatUnit guard, bool isCover)
        {
            if (guarded == null || guard == null || ReferenceEquals(guarded, guard))
            {
                return;
            }
            _guardOf[guarded] = new Entry { Guard = guard, IsCover = isCover };
        }

        /// <summary>Forgets <paramref name="guard"/> wherever it was guarding.</summary>
        public void ClearGuard(ICombatUnit guard)
        {
            RemoveWhere(entry => ReferenceEquals(entry.Guard, guard));
        }

        /// <summary>
        /// Ends every cover <paramref name="unit"/> is holding. Called at the start of each unit's turn by
        /// both combat loops, so a cover lasts exactly until its guard acts again. A full guard is untouched:
        /// a mech's own turn does not put its rider down.
        /// </summary>
        public void ExpireCovers(ICombatUnit unit)
        {
            RemoveWhere(entry => entry.IsCover && ReferenceEquals(entry.Guard, unit));
        }

        private void RemoveWhere(System.Func<Entry, bool> match)
        {
            var guarded = new List<ICombatUnit>();
            foreach (var pair in _guardOf)
            {
                if (match(pair.Value))
                {
                    guarded.Add(pair.Key);
                }
            }
            foreach (var unit in guarded)
            {
                _guardOf.Remove(unit);
            }
        }

        public void Clear()
        {
            _guardOf.Clear();
        }

        /// <summary>The living guard standing in front of <paramref name="target"/>, of either kind, or null.</summary>
        public ICombatUnit GuardOf(ICombatUnit target)
        {
            return target != null && _guardOf.TryGetValue(target, out var entry) && entry.Guard != null && entry.Guard.IsAlive
                ? entry.Guard
                : null;
        }

        /// <summary>True when <paramref name="target"/> is under a living guard's cover (not a full guard).</summary>
        public bool IsCovered(ICombatUnit target)
        {
            return GuardOf(target) != null && _guardOf[target].IsCover;
        }

        /// <summary>True when <paramref name="guard"/> is covering anyone right now.</summary>
        public bool IsCovering(ICombatUnit guard)
        {
            if (guard == null || !guard.IsAlive)
            {
                return false;
            }
            foreach (var pair in _guardOf)
            {
                if (pair.Value.IsCover && ReferenceEquals(pair.Value.Guard, guard) && pair.Key.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Who a single-target blow aimed at <paramref name="target"/> actually lands on.</summary>
        public ICombatUnit Redirect(ICombatUnit target)
        {
            return GuardOf(target) ?? target;
        }

        /// <summary>
        /// Who a blow aimed at <paramref name="target"/> lands on when it is one of several targets of the
        /// same effect: a full guard still steps in, a cover does not.
        /// </summary>
        public ICombatUnit RedirectAreaHit(ICombatUnit target)
        {
            var guard = GuardOf(target);
            return guard != null && !_guardOf[target].IsCover ? guard : target;
        }
    }
}
