using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// Whether an enemy casts this turn, which of its <see cref="DrawableMagicEntry"/> entries it
    /// casts, and at whom. Pure and roll-injected, so the combat loop, the balance model and the
    /// tests all decide the same way.
    ///
    /// <para><b>This sits beside the archetype behaviours, not inside them.</b> An enemy's
    /// archetype (<see cref="EnemyArchetype"/>) still owns its fixed repertoire — attack, charge,
    /// heavy, heal, debuff, boss signature. Casting is a roll taken <i>before</i> that behaviour is
    /// consulted: hit and the enemy casts, miss and the archetype decides exactly as it always
    /// did. Nothing about the existing behaviours changed.</para>
    ///
    /// <para>Enemy casts do not spend charges. <see cref="DrawableMagicEntry.Charges"/> is what a
    /// successful Draw grants the player; an enemy casting from the same list is free, which is how
    /// the FF games this system is modelled on treat it.</para>
    /// </summary>
    public static class EnemyMagicPlan
    {
        /// <summary>
        /// Whether the enemy casts this turn. <paramref name="roll"/> is a 0..1 sample (use
        /// <c>Random.Range(0f, 1f)</c>).
        ///
        /// <para>A charging enemy never casts: it has already telegraphed a heavy or a signature and
        /// the player has been shown that, so swallowing it would make the telegraph a lie.</para>
        /// </summary>
        public static bool ShouldCast(
            float castChance, IList<DrawableMagicEntry> magics, bool isCharging, float roll)
        {
            if (isCharging || castChance <= 0f || !HasCastable(magics))
            {
                return false;
            }
            return roll < castChance;
        }

        /// <summary>True when at least one entry carries a magic that can actually be cast.</summary>
        public static bool HasCastable(IList<DrawableMagicEntry> magics)
        {
            if (magics == null)
            {
                return false;
            }
            for (int i = 0; i < magics.Count; i++)
            {
                if (IsCastable(magics[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Picks which magic to cast, weighted by <see cref="DrawableMagicEntry.CastWeight"/>.
        /// <paramref name="roll"/> is a 0..1 sample.
        ///
        /// <para>Weights that sum to zero fall back to a uniform pick. That is not just defensive:
        /// <c>CastWeight</c> was added to a type that was already serialized on every enemy asset, so
        /// existing entries deserialize to 0 rather than to the C# initializer. Uniform is the
        /// behaviour those assets should have.</para>
        /// </summary>
        public static MagicSO Select(IList<DrawableMagicEntry> magics, float roll)
        {
            if (!HasCastable(magics))
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < magics.Count; i++)
            {
                if (IsCastable(magics[i]))
                {
                    total += Mathf.Max(0f, magics[i].CastWeight);
                }
            }

            if (total <= 0f)
            {
                return Uniform(magics, roll);
            }

            float target = Mathf.Clamp01(roll) * total;
            float running = 0f;
            MagicSO last = null;
            for (int i = 0; i < magics.Count; i++)
            {
                if (!IsCastable(magics[i]))
                {
                    continue;
                }
                last = magics[i].Magic;
                running += Mathf.Max(0f, magics[i].CastWeight);
                if (target < running)
                {
                    return last;
                }
            }

            // Only reachable on floating-point slop at roll == 1.
            return last;
        }

        /// <summary>
        /// Who the cast lands on, read from the enemy's side of the table.
        /// <see cref="MagicTargetType"/> is authored from the player's point of view, so it mirrors:
        /// "enemy" is the hero side and "ally" is the other monsters.
        ///
        /// <para>A single-ally cast picks the most wounded of the enemy and its allies — right for a
        /// heal, and harmless for a buff, since the one taking damage is the one worth shielding.</para>
        /// </summary>
        public static List<ICombatUnit> ResolveTargets(
            MagicSO magic,
            ICombatUnit self,
            IList<ICombatUnit> heroes,
            IList<ICombatUnit> allies,
            float roll)
        {
            var targets = new List<ICombatUnit>();
            if (magic == null)
            {
                return targets;
            }

            switch (magic.TargetType)
            {
                case MagicTargetType.SingleEnemy:
                {
                    var pick = PickAlive(heroes, roll);
                    if (pick != null)
                    {
                        targets.Add(pick);
                    }
                    break;
                }

                case MagicTargetType.AllEnemies:
                    AddAlive(targets, heroes);
                    break;

                case MagicTargetType.Self:
                    if (self != null && self.IsAlive)
                    {
                        targets.Add(self);
                    }
                    break;

                case MagicTargetType.SingleAlly:
                {
                    var pick = MostWounded(self, allies);
                    if (pick != null)
                    {
                        targets.Add(pick);
                    }
                    break;
                }

                case MagicTargetType.AllAllies:
                    if (self != null && self.IsAlive)
                    {
                        targets.Add(self);
                    }
                    AddAlive(targets, allies);
                    break;
            }

            return targets;
        }

        /// <summary>
        /// The level's spell-power multiplier applied to an authored base power. Rounds up off zero,
        /// so a scaled-down power never becomes a free spell.
        /// </summary>
        public static int ScalePower(int power, float scale)
        {
            if (power <= 0 || Mathf.Approximately(scale, 1f))
            {
                return power;
            }
            return Mathf.Max(1, Mathf.RoundToInt(power * scale));
        }

        private static bool IsCastable(DrawableMagicEntry entry)
        {
            return entry != null && entry.Magic != null
                && entry.Magic.Effects != null && entry.Magic.Effects.Count > 0;
        }

        private static MagicSO Uniform(IList<DrawableMagicEntry> magics, float roll)
        {
            var castable = new List<MagicSO>();
            for (int i = 0; i < magics.Count; i++)
            {
                if (IsCastable(magics[i]))
                {
                    castable.Add(magics[i].Magic);
                }
            }
            if (castable.Count == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll) * castable.Count), 0, castable.Count - 1);
            return castable[index];
        }

        private static ICombatUnit PickAlive(IList<ICombatUnit> units, float roll)
        {
            var alive = new List<ICombatUnit>();
            AddAlive(alive, units);
            if (alive.Count == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll) * alive.Count), 0, alive.Count - 1);
            return alive[index];
        }

        private static ICombatUnit MostWounded(ICombatUnit self, IList<ICombatUnit> allies)
        {
            ICombatUnit best = null;
            int worstMissing = -1;

            var candidates = new List<ICombatUnit>();
            if (self != null && self.IsAlive)
            {
                candidates.Add(self);
            }
            AddAlive(candidates, allies);

            foreach (var unit in candidates)
            {
                int missing = unit.GetEffectiveStat(StatType.MaxHealth) - unit.Stats.Health;
                if (missing > worstMissing)
                {
                    worstMissing = missing;
                    best = unit;
                }
            }

            return best;
        }

        private static void AddAlive(List<ICombatUnit> into, IList<ICombatUnit> from)
        {
            if (from == null)
            {
                return;
            }
            for (int i = 0; i < from.Count; i++)
            {
                if (from[i] != null && from[i].IsAlive && !into.Contains(from[i]))
                {
                    into.Add(from[i]);
                }
            }
        }
    }
}
