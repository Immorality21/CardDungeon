using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Who the enemies are angry at, for one fight. WoW-style threat: every point of damage a unit
    /// deals and every point of healing it lands makes it more of a target - but it only
    /// <b>biases</b> the pick, never decides it.
    ///
    /// <para><b>The formula.</b> With <c>n</c> living candidates, each holding threat <c>T</c>:</para>
    /// <code>
    ///   weight_i = T_i + BaseThreat
    ///   chance_i = FlatShare / n  +  (1 - FlatShare) * weight_i / sum(weight)
    /// </code>
    /// <list type="bullet">
    /// <item><description>Half of every pick (<see cref="FlatShare"/>) is plain random, so no hero is
    /// ever safe: the floor is 25% each with two heroes, 12.5% with four.</description></item>
    /// <item><description>The other half follows threat, so no hero is ever certain: the ceiling is
    /// 75% with two heroes, 62.5% with four.</description></item>
    /// <item><description><see cref="BaseThreat"/> is what a unit is worth before it has done
    /// anything - the fight opens even, and the first hit shifts it rather than flipping
    /// it.</description></item>
    /// </list>
    ///
    /// <para><b>What earns it.</b> Damage dealt counts in full (<see cref="DamageThreat"/>), healing
    /// at half (<see cref="HealingThreat"/>) - the WoW split, so a healer draws attention without
    /// out-drawing whoever is doing the hitting. Only what actually landed counts: a kill shot is
    /// worth the health the enemy had left, and overhealing is worth nothing.</para>
    ///
    /// <para><b>Per ability.</b> Every ability earns this by default; <c>MagicSO.ThreatMultiplier</c>
    /// scales what its damage and healing earn and <c>MagicSO.BonusThreat</c> adds a flat amount per
    /// cast, so a showy strike can draw more than its damage and a taunt can draw aggro while dealing
    /// nothing.</para>
    ///
    /// <para>Pure and roll-injected, like the planner that uses it. A null table means "no threat",
    /// which is plain uniform random - what the balance simulator still assumes.</para>
    /// </summary>
    public class ThreatTable
    {
        /// <summary>Threat per point of damage dealt.</summary>
        public const float DamageThreat = 1f;

        /// <summary>Threat per point of healing landed.</summary>
        public const float HealingThreat = 0.5f;

        /// <summary>What every candidate is worth before it has earned any threat.</summary>
        public const float BaseThreat = 10f;

        /// <summary>The fraction of each pick that ignores threat entirely.</summary>
        public const float FlatShare = 0.5f;

        private readonly Dictionary<ICombatUnit, float> _threat = new Dictionary<ICombatUnit, float>();

        public float Get(ICombatUnit unit)
        {
            if (unit == null)
            {
                return 0f;
            }
            return _threat.TryGetValue(unit, out var value) ? value : 0f;
        }

        public void Add(ICombatUnit unit, float amount)
        {
            if (unit == null || amount <= 0f)
            {
                return;
            }
            _threat[unit] = Get(unit) + amount;
        }

        /// <summary>
        /// Credits an action's result: the damage it dealt and the healing it landed, scaled by the
        /// ability's <paramref name="multiplier"/>, plus its flat <paramref name="bonus"/>. A plain
        /// attack or an item uses the defaults (1 and 0).
        /// </summary>
        public void Credit(ICombatUnit unit, int damageDealt, int healingDone, float multiplier = 1f, int bonus = 0)
        {
            Add(unit, ThreatFor(damageDealt, healingDone, multiplier, bonus));
        }

        /// <summary>The threat one action earns - see <see cref="Credit"/>.</summary>
        public static float ThreatFor(int damageDealt, int healingDone, float multiplier = 1f, int bonus = 0)
        {
            float earned = System.Math.Max(0, damageDealt) * DamageThreat
                           + System.Math.Max(0, healingDone) * HealingThreat;
            return earned * System.Math.Max(0f, multiplier) + System.Math.Max(0, bonus);
        }

        /// <summary>Forgets a unit's threat - a hero who goes down comes back with a clean slate.</summary>
        public void Clear(ICombatUnit unit)
        {
            if (unit != null)
            {
                _threat.Remove(unit);
            }
        }

        public void Reset()
        {
            _threat.Clear();
        }

        /// <summary>
        /// Each living candidate's chance of being picked, in list order (0 for the dead). Sums to 1
        /// when anyone is alive. <paramref name="table"/> may be null: even odds.
        /// </summary>
        public static List<float> Chances(IList<ICombatUnit> candidates, ThreatTable table)
        {
            var chances = new List<float>();
            if (candidates == null)
            {
                return chances;
            }

            int living = 0;
            float weightSum = 0f;
            foreach (var unit in candidates)
            {
                if (IsCandidate(unit))
                {
                    living++;
                    weightSum += Weight(unit, table);
                }
            }

            foreach (var unit in candidates)
            {
                if (!IsCandidate(unit))
                {
                    chances.Add(0f);
                    continue;
                }

                float threatShare = weightSum > 0f ? Weight(unit, table) / weightSum : 1f / living;
                chances.Add(FlatShare / living + (1f - FlatShare) * threatShare);
            }
            return chances;
        }

        /// <summary>
        /// Picks a living candidate with <paramref name="roll"/> in [0, 1), biased by threat. Null
        /// when nobody is alive.
        /// </summary>
        public static ICombatUnit Pick(IList<ICombatUnit> candidates, ThreatTable table, float roll)
        {
            var chances = Chances(candidates, table);
            ICombatUnit last = null;
            float cumulative = 0f;
            for (int i = 0; i < chances.Count; i++)
            {
                if (chances[i] <= 0f)
                {
                    continue;
                }

                last = candidates[i];
                cumulative += chances[i];
                if (roll < cumulative)
                {
                    return candidates[i];
                }
            }

            // Float rounding can leave the cumulative sum a hair under 1.
            return last;
        }

        private static float Weight(ICombatUnit unit, ThreatTable table)
        {
            return BaseThreat + (table != null ? table.Get(unit) : 0f);
        }

        private static bool IsCandidate(ICombatUnit unit)
        {
            return unit != null && unit.IsAlive;
        }
    }
}
