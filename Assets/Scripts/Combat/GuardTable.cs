using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Who is shielding whom in one fight: a guarded unit's blows land on its guard instead, for as
    /// long as the guard stands. The Tinkerer riding a mech (<c>UltraKind.Mount</c>) is the first user:
    /// the mech's health takes what was aimed at her, and once it breaks she is hit normally again.
    ///
    /// <para>Kept on <see cref="CombatEvents"/> so the live fight and the simulator share it the way they
    /// share the stream. Consulted where a hit picks its victim - a basic attack and an ability's damage -
    /// <b>before</b> the damage is worked out, so the guard's own defences apply. Over-time ticks are not
    /// redirected: a poison already in her stays hers.</para>
    /// </summary>
    public sealed class GuardTable
    {
        private readonly Dictionary<ICombatUnit, ICombatUnit> _guardOf = new Dictionary<ICombatUnit, ICombatUnit>();

        /// <summary><paramref name="guard"/> takes <paramref name="guarded"/>'s blows from now on.</summary>
        public void Set(ICombatUnit guarded, ICombatUnit guard)
        {
            if (guarded == null || guard == null || ReferenceEquals(guarded, guard))
            {
                return;
            }
            _guardOf[guarded] = guard;
        }

        /// <summary>Forgets <paramref name="guard"/> wherever it was guarding.</summary>
        public void ClearGuard(ICombatUnit guard)
        {
            var guarded = new List<ICombatUnit>();
            foreach (var pair in _guardOf)
            {
                if (ReferenceEquals(pair.Value, guard))
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

        /// <summary>The living guard standing in front of <paramref name="target"/>, or null.</summary>
        public ICombatUnit GuardOf(ICombatUnit target)
        {
            return target != null && _guardOf.TryGetValue(target, out var guard) && guard != null && guard.IsAlive
                ? guard
                : null;
        }

        /// <summary>Who a blow aimed at <paramref name="target"/> actually lands on.</summary>
        public ICombatUnit Redirect(ICombatUnit target)
        {
            return GuardOf(target) ?? target;
        }
    }
}
