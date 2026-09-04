using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
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

        /// <summary>
        /// Stat buffs and debuffs its casts keep up. A cast is not only damage: Shield Up on itself
        /// lengthens the fight, and a hero debuff shortens the party's output.
        /// </summary>
        public List<StatShift> StatShifts = new List<StatShift>();

        public bool Casts => CastChance > 0f && CastableCount > 0;
    }

    /// <summary>
    /// Prices an enemy's <see cref="EnemySO.Spells"/> as offense, so the danger index and the
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
    /// <para><b>Stat</b> buff and debuff effects are counted as neither damage nor healing. They are
    /// real — an enemy casting Shield Up genuinely lengthens the fight — but the closed form has
    /// nowhere to put a stat delta, the same limitation the archetype multipliers already work around
    /// with a flat factor for Healer and Debuffer.</para>
    ///
    /// <para><b>Over-time</b> buffs and debuffs are the exception: a burn or a poison is damage
    /// wearing a Debuff's clothes, so <see cref="OverTimeAgainst"/> prices it into
    /// <see cref="DamageOfCast"/> over its full duration. Without that a poison cast would price as
    /// nothing at all — the Damage filter skips it and the stat-shift collector skips it too, which
    /// is precisely how the resistance buffs managed to be inert for months.</para>
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
            if (enemy == null || enemy.Spells == null)
            {
                return profile;
            }

            // Cast frequency is an authored action now, not a field: sum the share of turns the
            // behaviour's CastMagic entries claim. EnemyBehaviorModel owns that arithmetic.
            profile.CastChance = CastShareOf(enemy.ResolvedBehavior);
            float powerScale = LevelEnemyTuning.MagicPowerScaleFor(enemy, tuning);

            float totalWeight = 0f;
            var weights = new List<float>();
            var entries = new List<EnemySpellEntry>();

            foreach (var entry in enemy.Spells)
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
                CollectStatShifts(entries[i].Magic, share * profile.CastChance, profile.StatShifts);
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
                if (effect == null || effect.UnlockLevel > 0)
                {
                    continue;
                }

                float perTarget;
                if (effect.EffectType == SpellEffectType.Damage)
                {
                    // Not guarded on <= 0: AverageAgainst returns a *negative* average when the party
                    // absorbs the element (stacked cloaks past 100%), and that reduction is the whole
                    // point of building for it. Skipping it would price an absorbed cast as merely
                    // harmless instead of helpful.
                    perTarget = AverageAgainst(effect, RawPower(effect, powerScale, caster), heroes);
                }
                else
                {
                    perTarget = OverTimeAgainst(effect, caster, heroes);
                    if (perTarget <= 0f)
                    {
                        continue;   // not an over-time effect at all, or one that heals
                    }
                }

                total += everyone ? perTarget * CountAlive(heroes) : perTarget;
            }

            return total;
        }

        /// <summary>
        /// Expected damage from an over-time effect (burn, poison, bleed) inside a cast, summed over
        /// its whole duration. Returns 0 for anything that is not a damaging over-time effect —
        /// which is every stat buff, resistance, Frozen, Silence and Regeneration.
        ///
        /// <para>Without this a poison would price as <b>nothing at all</b>: it is authored as a
        /// Debuff, so <see cref="DamageOfCast"/>'s Damage filter skips it, and
        /// <see cref="CollectStatShifts"/> skips it too because its <c>BuffType</c> has no matching
        /// <c>StatType</c>. That is the exact shape of the resistance-buff bug — an effect that
        /// looked handled by two systems and was handled by neither.</para>
        ///
        /// <para><b>Two deliberate approximations.</b> Duration is charged in full, so a re-applied
        /// over-time effect is over-counted (the tracker refreshes rather than stacks, so a second
        /// application on a still-burning target adds almost nothing) and one that outlives the fight
        /// is over-counted too. Both push this term <i>up</i>, which makes an enemy caster read
        /// slightly more dangerous than it plays — the opposite of the model's usual optimism, and
        /// worth knowing when a poison enemy sits just outside a band.</para>
        /// </summary>
        private static float OverTimeAgainst(SpellEffect effect, SimUnit caster, IList<SimUnit> heroes)
        {
            if (effect.EffectType != SpellEffectType.Buff && effect.EffectType != SpellEffectType.Debuff)
            {
                return 0f;
            }

            var overTime = BuffHandlerRegistry.Get(effect.BuffType) as IOverTimeBuffHandler;
            if (overTime == null || overTime.Heals)
            {
                return 0f;
            }

            // The magnitude both buff executors compute: authored Power plus a *fraction* of the
            // caster's scaling stat. Every DoT authored today is ScalingStat.None so this is just
            // Power — but reading the field is what stops the model silently under-counting the first
            // time someone scales a poison off Intelligence.
            int perTick = Mathf.Abs(
                effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, null));
            int turns = Mathf.Max(0, effect.Duration);
            if (perTick <= 0 || turns <= 0)
            {
                return 0f;
            }

            float total = 0f;
            int counted = 0;

            foreach (var hero in heroes)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }

                // The tracker's arithmetic: defense applies unless the effect bypasses it, and the
                // element is the handler's, not the SpellEffect's.
                int defense = overTime.IgnoresDefense ? 0 : hero.GetEffectiveStat(StatType.Endurance);
                total += DamageCalculator.Calculate(
                    perTick, defense, overTime.TickDamageType, hero.Resistances) * turns;
                counted++;
            }

            return counted > 0 ? total / counted : 0f;
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
                total += effect.PowerMode == PowerMode.PercentOfMaxHealth
                    ? SpellPower.PercentOfMaxHealth(effect.Power, caster)
                    : RawPower(effect, powerScale, caster);
            }
            return total;
        }

        /// <summary>
        /// Buff and Debuff effects inside a cast, as stat shifts. Buff/Debuff <c>Power</c> is a stat
        /// delta rather than a damage number, so it is neither damage nor healing — it belonged to
        /// neither column, which is exactly why these used to price as nothing at all.
        ///
        /// <para><c>BuffType</c> maps to <c>StatType</c> by name, the same way
        /// <c>BuffHandlerRegistry</c> builds its stat handlers, so a buff type with no matching stat
        /// (a resistance, or a status effect like Frozen) is skipped rather than guessed at.</para>
        /// </summary>
        public static void CollectStatShifts(
            MagicSO magic, float claim, List<StatShift> into)
        {
            if (magic == null || magic.Effects == null || claim <= 0f)
            {
                return;
            }

            bool onHeroSide = HitsHeroSide(magic.TargetType);

            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.UnlockLevel > 0)
                {
                    continue;
                }
                if (effect.EffectType != SpellEffectType.Buff && effect.EffectType != SpellEffectType.Debuff)
                {
                    continue;
                }

                StatType stat;
                if (!System.Enum.TryParse(effect.BuffType.ToString(), out stat)
                    || stat == StatType.None
                    || !StatCatalog.Types.Contains(stat))
                {
                    continue;   // a resistance or a status effect - not a stat shift
                }

                into.Add(new StatShift
                {
                    Stat = stat,
                    Power = Mathf.Abs(effect.Power),
                    Uptime = EnemyBehaviorModel.Uptime(claim, effect.Duration),
                    OnHeroSide = onHeroSide
                });
            }
        }

        /// <summary>
        /// One effect's raw power as the executors see it: authored Power through the level's spell
        /// scale, plus the caster's scaling-stat contribution.
        ///
        /// <para><b>Buff/Debuff power does not take the level's spell scale</b>, for the same reason
        /// the upgrade bonus leaves it alone — `EffectResolver.ApplyPowerBonus` early-returns for
        /// anything that is not Damage or Heal. It <i>does</i> take a caster contribution, though:
        /// both buff executors add `SpellScaling.BuffContribution` unconditionally. Those are two
        /// different rules and this method only implements the first — see
        /// <see cref="OverTimeAgainst"/>, which applies the second for the effects where it matters.
        /// A previous version of this comment claimed neither applied, which was half wrong.</para>
        /// </summary>
        private static int RawPower(SpellEffect effect, float powerScale, SimUnit caster)
        {
            bool scales = effect.EffectType == SpellEffectType.Damage
                       || effect.EffectType == SpellEffectType.Heal;

            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                // Percentage power is resolved against the unit the effect lands on, so a level scale
                // and a caster stat have nothing to add to it. The per-target number comes from
                // AverageAgainst.
                return effect.Power;
            }

            int power = scales ? EnemyMagicPlan.ScalePower(effect.Power, powerScale) : effect.Power;
            if (effect.PowerMode == PowerMode.Flat)
            {
                return power;
            }
            return power + SpellScaling.CasterContribution(caster, effect.ScalingStat, null);
        }

        /// <summary>
        /// Average damage one effect lands on a uniformly random living hero — which is how
        /// <see cref="EnemyMagicPlan.ResolveTargets"/> picks a single target. Deliberately
        /// crit-free: <see cref="Cards.Effects.DamageEffectExecutor"/> has no crit roll.
        /// </summary>
        private static float AverageAgainst(SpellEffect effect, int rawPower, IList<SimUnit> heroes)
        {
            float total = 0f;
            int counted = 0;

            foreach (var hero in heroes)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }

                // A PercentOfMaxHealth effect reads the bar of whoever it lands on, so its raw power
                // is per hero rather than a single number for the whole party.
                int raw = effect.PowerMode == PowerMode.PercentOfMaxHealth
                    ? SpellPower.PercentOfMaxHealth(effect.Power, hero)
                    : rawPower;

                total += DamageCalculator.Calculate(
                    raw, hero.GetEffectiveStat(StatType.Endurance), effect.DamageType, hero.Resistances);
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
