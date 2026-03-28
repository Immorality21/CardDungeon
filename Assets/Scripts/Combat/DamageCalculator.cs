using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    public static class DamageCalculator
    {
        /// <summary>
        /// Controls defense diminishing returns. At defense == K, reduction is 50%.
        /// Higher K means defense is less effective per point.
        /// </summary>
        public const float DefenseConstant = 20f;

        /// <summary>
        /// Calculates final damage after applying resistance and defense with diminishing returns.
        ///
        /// Pipeline:
        /// 1. Start with raw damage
        /// 2. Apply resistance modifier (scales damage up or down based on DamageType)
        ///    - 0% resistance   = 100% damage taken
        ///    - 100% resistance = immune (0 damage)
        ///    - 200% resistance = absorbs 100% (heals instead — returns negative damage)
        ///    - -100% resistance = 200% damage taken
        ///    - If resistance > 100%, defense is NOT applied (absorption bypasses defense)
        /// 3. Apply defense reduction with diminishing returns: reduction = defense / (defense + K)
        /// 4. Minimum 1 damage (unless absorbed)
        /// </summary>
        public static int Calculate(int rawDamage, int defense, DamageType damageType, List<Resistance> resistances)
        {
            if (rawDamage <= 0)
            {
                return 0;
            }

            float resistPercent = GetResistance(damageType, resistances);
            resistPercent = Mathf.Clamp(resistPercent, -100f, 200f);

            // Resistance multiplier: 0% resist = 1.0x, 100% = 0.0x, -100% = 2.0x, 200% = -1.0x
            float resistMultiplier = 1f - (resistPercent / 100f);
            float afterResist = rawDamage * resistMultiplier;

            // Absorption: if resistance > 100%, damage is negative (healing). Skip defense.
            if (resistPercent > 100f)
            {
                return Mathf.RoundToInt(afterResist);
            }

            // Immune at exactly 100%
            if (Mathf.Approximately(resistPercent, 100f))
            {
                return 0;
            }

            // Apply defense diminishing returns
            float effectiveDefense = Mathf.Max(0, defense);
            float reduction = effectiveDefense / (effectiveDefense + DefenseConstant);
            float afterDefense = afterResist * (1f - reduction);

            // Minimum 1 damage
            return Mathf.Max(1, Mathf.RoundToInt(afterDefense));
        }

        /// <summary>
        /// Gets the resistance percentage for a given damage type.
        /// Returns 0 if no matching resistance is found.
        /// </summary>
        public static float GetResistance(DamageType damageType, List<Resistance> resistances)
        {
            if (resistances == null)
            {
                return 0f;
            }

            foreach (var r in resistances)
            {
                if (r.DamageType == damageType)
                {
                    return r.Percent;
                }
            }

            return 0f;
        }
    }
}
