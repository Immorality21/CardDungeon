using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>What one cast from an enemy is worth, in the same currency the rest of the model uses.</summary>
    public class EnemyCastProfile
    {
        /// <summary>Chance per turn the enemy casts instead of acting on its archetype.</summary>
        public float CastChance;

        /// <summary>Expected damage a single cast lands on the hero side, weighted across the enemy's entries.</summary>
        public float ExpectedDamage;

        /// <summary>
        /// Expected healing a single cast returns to the enemy side. Not netted against damage —
        /// reported so a healing caster is visible rather than reading as a harmless one.
        /// </summary>
        public float ExpectedHealing;

        /// <summary>How many of the enemy's entries the model could price at all.</summary>
        public int CastableCount;

        public bool Casts => CastChance > 0f && CastableCount > 0;
    }

    /// <summary>
    /// Prices an enemy's <see cref="EnemySO.DrawableMagics"/> as offense, so the danger index and the
    /// attrition curve see the spells an enemy actually throws
    /// (its <c>CastMagic</c> actions) instead of measuring it as attack-only.
    ///
    /// <para>The arithmetic is the executors' arithmetic, deliberately: base Power scaled by the
    /// level's <see cref="LevelEnemyTuning.MagicPowerScaleFor(EnemySO)"/> exactly as
    /// <see cref="EnemyMagicPlan.ScalePower"/> does it, plus
    /// <see cref="SpellScaling.CasterContribution"/>, through
    /// <see cref="Combat.DamageCalculator"/>. Three fidelity details worth not regressing:</para>
    /// <list type="bullet">
    /// <item><description><b>No crit.</b> Spells do not crit — only <c>CombatManager.ExecuteAttack</c>
    /// rolls one — so this calls <see cref="Combat.DamageCalculator"/> directly rather than going
    /// through <see cref="BalanceMath.AverageDamageAgainstGroup"/>, which always folds in an expected
    /// crit multiplier (it falls back to the <i>base</i> rate for a null attacker, not to 1, so
    /// there is no way to opt out through that path).</description></item>
    /// <item><description><b>Level 0.</b> Enemies cast the base version of a magic, so any effect
    /// behind an <c>UnlockLevel</c> is skipped, matching what the resolver does with
    /// <c>magicUpgradeLevel: 0</c>.</description></item>
    /// <item><description><b>No tags or combos.</b> Enemy casts pass neither tracker, so nothing here
    /// prices combo follow-ups. If that changes, this is the other half that has to change with
    /// it.</description></item>
    /// </list>
    ///
    /// <para>Buff and Debuff effects are counted as neither damage nor healing. They are real —
    /// an enemy casting Shield Up genuinely lengthens the fight — but the closed form has nowhere to
    /// put a stat delta, the same limitation the archetype multipliers already work around with a
    /// flat factor for Healer and Debuffer.</para>
    /// </summary>
    public static class EnemyMagicModel
    {
        /// <summary>
        /// Prices every castable entry on <paramref name="enemy"/> against
        /// <paramref name="heroes"/>. <paramref name="caster"/> supplies the scaling stat, so it
        /// should be the enemy as the level actually fights it.
        /// </summary>
        public static EnemyCastProfile Profile(
            EnemySO enemy, LevelEnemyTuning tuning, SimUnit caster, IList<SimUnit> heroes)
        {
            var profile = new EnemyCastProfile();
            if (enemy == null || enemy.DrawableMagics == null)
            {
                return profile;
            }

            // Cast frequency is an authored action now, not a field: sum the share of turns the
            // behaviour's CastMagic entries claim. EnemyBehaviorModel owns that arithmetic.
            profile.CastChance = CastShareOf(enemy.ResolvedBehavior);
            float powerScale = LevelEnemyTuning.MagicPowerScaleFor(enemy, tuning);

            float totalWeight = 0f;
            var weights = new List<float>();
            var entries = new List<DrawableMagicEntry>();

            foreach (var entry in enemy.DrawableMagics)
            {
                if (entry == null || entry.Magic == null || entry.Magic.Effects == null
                    || entry.Magic.Effects.Count == 0)
                {
                    continue;
                }
                entries.Add(entry);
                weights.Add(Mathf.Max(0f, entry.CastWeight));
                totalWeight += Mathf.Max(0f, entry.CastWeight);
            }

            profile.CastableCount = entries.Count;
            if (entries.Count == 0)
            {
                return profile;
            }

            // Mirrors EnemyMagicPlan.Select: all-zero weights (which is what assets authored before
            // CastWeight existed deserialize to) mean a uniform pick.
            bool uniform = totalWeight <= 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                float share = uniform ? 1f / entries.Count : weights[i] / totalWeight;
                if (share <= 0f)
                {
                    continue;
                }

                profile.ExpectedDamage += share * DamageOfCast(entries[i].Magic, powerScale, caster, heroes);
                profile.ExpectedHealing += share * HealingOfCast(entries[i].Magic, powerScale, caster);
            }

            return profile;
        }

        /// <summary>
        /// Expected damage one cast of <paramref name="magic"/> lands on the hero side. A
        /// single-target spell is averaged over the party (a uniformly random victim, as
        /// <see cref="EnemyMagicPlan.ResolveTargets"/> picks); a party-wide spell is summed, the same
        /// way the boss signature is counted as hitting everyone.
        /// </summary>
        public static float DamageOfCast(MagicSO magic, float powerScale, SimUnit caster, IList<SimUnit> heroes)
        {
            if (magic == null || heroes == null || heroes.Count == 0 || !HitsHeroSide(magic.TargetType))
            {
                return 0f;
            }

            bool everyone = magic.TargetType == MagicTargetType.AllEnemies;
            float total = 0f;

            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.EffectType != SpellEffectType.Damage || effect.UnlockLevel > 0)
                {
                    continue;
                }

                int raw = RawPower(effect, powerScale, caster);
                float perTarget = AverageAgainst(raw, effect.DamageType, heroes);

                total += everyone ? perTarget * CountAlive(heroes) : perTarget;
            }

            return total;
        }

        /// <summary>
        /// Share of turns this behaviour spends casting. Reads the authored actions rather than a
        /// per-enemy field, which is what <c>EnemySO.MagicCastChance</c> became.
        /// </summary>
        public static float CastShareOf(Assets.Scripts.Enemies.Behaviors.EnemyBehaviorSO behavior)
        {
            var profile = EnemyBehaviorModel.Profile(behavior, 1, 0f, 1f);
            return Mathf.Clamp01(profile.CastShare);
        }

        /// <summary>Expected healing one cast returns to the enemy side, for reporting.</summary>
        public static float HealingOfCast(MagicSO magic, float powerScale, SimUnit caster)
        {
            if (magic == null || HitsHeroSide(magic.TargetType))
            {
                return 0f;
            }

            float total = 0f;
            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.EffectType != SpellEffectType.Heal || effect.UnlockLevel > 0)
                {
                    continue;
                }
                total += RawPower(effect, powerScale, caster);
            }
            return total;
        }

        /// <summary>
        /// One effect's raw power as the executors see it: authored Power through the level's spell
        /// scale, plus the caster's scaling-stat contribution. Buff/Debuff power does not scale, for
        /// the same reason the upgrade bonus leaves it alone.
        /// </summary>
        private static int RawPower(SpellEffect effect, float powerScale, SimUnit caster)
        {
            bool scales = effect.EffectType == SpellEffectType.Damage
                       || effect.EffectType == SpellEffectType.Heal;

            int power = scales ? EnemyMagicPlan.ScalePower(effect.Power, powerScale) : effect.Power;
            return power + SpellScaling.CasterContribution(caster, effect.ScalingStat, null);
        }

        /// <summary>
        /// Average damage one effect lands on a uniformly random living hero — which is how
        /// <see cref="EnemyMagicPlan.ResolveTargets"/> picks a single target. Deliberately
        /// crit-free: <see cref="Cards.Effects.DamageEffectExecutor"/> has no crit roll.
        /// </summary>
        private static float AverageAgainst(int rawPower, DamageType type, IList<SimUnit> heroes)
        {
            float total = 0f;
            int counted = 0;

            foreach (var hero in heroes)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }
                total += DamageCalculator.Calculate(
                    rawPower, hero.GetEffectiveStat(StatType.Endurance), type, hero.Resistances);
                counted++;
            }

            return counted > 0 ? total / counted : 0f;
        }

        private static bool HitsHeroSide(MagicTargetType target)
        {
            return target == MagicTargetType.SingleEnemy || target == MagicTargetType.AllEnemies;
        }

        private static int CountAlive(IList<SimUnit> units)
        {
            int count = 0;
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive)
                {
                    count++;
                }
            }
            return Mathf.Max(1, count);
        }
    }
}
