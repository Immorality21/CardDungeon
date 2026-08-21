using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>How hard one enemy hits one specific hero — the one-shot detector lives here.</summary>
    public class EnemyVsHero
    {
        public string HeroName;
        public float DamagePerHit;
        public int HitsToKill;
        public float HealthFractionPerHit;
    }

    /// <summary>
    /// Derived numbers for a single <see cref="EnemySO"/>, measured against a reference party.
    /// Nothing here is authored — every field is computed from the asset plus the party, so editing a
    /// stat in the balance window and recomputing shows the consequence immediately.
    /// </summary>
    public class EnemyMetrics
    {
        public EnemySO Definition;
        public string Name = "";
        public bool IsBoss;
        public EnemyArchetype Archetype;

        /// <summary>Weighted stat budget, for spotting outliers inside a tier.</summary>
        public float PowerScore;

        /// <summary>Party-turns to kill this enemy alone, basic attacks only.</summary>
        public float PartyTurnsToKill;

        /// <summary>Danger of this enemy alone against the whole party (see BalanceMath.DangerIndex).</summary>
        public float SoloDangerIndex;

        /// <summary>Average damage of one of its plain hits against the average party member.</summary>
        public float AverageDamagePerHit;

        /// <summary>Its per-turn output as a multiple of one plain hit (charges, heavies, AoE).</summary>
        public float OffenseMultiplier;

        /// <summary>Effective per-turn damage: plain hit x archetype multiplier.</summary>
        public float EffectiveDamagePerTurn;

        /// <summary>Turns it takes per 100 CTB ticks relative to the party average — the hidden threat multiplier.</summary>
        public float ActionShareVsParty;

        /// <summary>Fewest hits it needs to drop any single hero. 1 means it one-shots someone.</summary>
        public int FewestHitsToKillAHero = int.MaxValue;

        public string FastestKillTarget = "";

        public List<EnemyVsHero> PerHero = new List<EnemyVsHero>();

        /// <summary>Reward paid per unit of danger — catches enemies that pay badly for their risk.</summary>
        public float XpPerDanger;
        public float GoldPerDanger;

        public int ResistanceCount;
        public int DrawableCount;

        /// <param name="party">Who this enemy is judged against — the party it is first met with.</param>
        /// <param name="rewardParty">
        /// Party used for the reward-per-danger figures only. Danger is measured against whoever
        /// meets the enemy, which varies by run position, so comparing XP-per-danger across enemies
        /// needs one common yardstick or the spread just reports roster growth. Defaults to
        /// <paramref name="party"/>.
        /// </param>
        public static EnemyMetrics Compute(EnemySO enemy, PartyBaseline party, BalanceRulesSO rules,
            PartyBaseline rewardParty = null)
        {
            var metrics = new EnemyMetrics { Definition = enemy };
            if (enemy == null || party == null || rules == null)
            {
                return metrics;
            }

            metrics.Name = string.IsNullOrEmpty(enemy.DisplayName) ? enemy.name : enemy.DisplayName;
            metrics.IsBoss = enemy.IsBoss;
            metrics.Archetype = enemy.Archetype;
            metrics.PowerScore = BalanceMath.PowerScore(enemy, rules);
            metrics.ResistanceCount = enemy.Resistances != null ? enemy.Resistances.Count : 0;
            metrics.DrawableCount = enemy.DrawableMagics != null ? enemy.DrawableMagics.Count : 0;

            var unit = SimUnit.FromEnemy(enemy);
            var group = new List<SimUnit> { unit };
            var partyUnits = party.Units;

            metrics.PartyTurnsToKill = BalanceMath.PartyTurnsToKill(partyUnits, unit);
            metrics.SoloDangerIndex = BalanceMath.DangerIndex(partyUnits, group);
            metrics.OffenseMultiplier = BalanceMath.AverageOffenseMultiplier(enemy.Archetype, party.Size);
            metrics.AverageDamagePerHit = BalanceMath.AverageDamageAgainstGroup(enemy.BaseStats[StatType.Strength], partyUnits);
            metrics.EffectiveDamagePerTurn = metrics.AverageDamagePerHit * metrics.OffenseMultiplier;

            // Per-hero breakdown: plain hits, since that is what the player actually feels turn to turn.
            foreach (var hero in party.Heroes)
            {
                if (hero.Unit == null)
                {
                    continue;
                }

                float perHit = BalanceMath.AverageDamage(enemy.BaseStats[StatType.Strength], hero.Unit);
                int htk = BalanceMath.HitsToKill(perHit, hero.Effective[StatType.MaxHealth]);

                metrics.PerHero.Add(new EnemyVsHero
                {
                    HeroName = hero.Name,
                    DamagePerHit = perHit,
                    HitsToKill = htk,
                    HealthFractionPerHit = hero.Effective[StatType.MaxHealth] > 0 ? perHit / hero.Effective[StatType.MaxHealth] : 0f
                });

                if (htk < metrics.FewestHitsToKillAHero)
                {
                    metrics.FewestHitsToKillAHero = htk;
                    metrics.FastestKillTarget = hero.Name;
                }
            }

            if (metrics.PerHero.Count == 0)
            {
                metrics.FewestHitsToKillAHero = 0;
            }

            metrics.ActionShareVsParty = ComputeActionShare(unit, party);

            var yardstick = rewardParty ?? party;
            float danger = ReferenceEquals(yardstick, party)
                ? metrics.SoloDangerIndex
                : BalanceMath.DangerIndex(yardstick.Units, group);
            if (danger > 0f && !float.IsInfinity(danger))
            {
                metrics.XpPerDanger = enemy.XpReward / danger;
                metrics.GoldPerDanger = enemy.GoldReward / danger;
            }

            return metrics;
        }

        /// <summary>
        /// How often this enemy acts compared with the average hero. A 2x agility enemy has roughly
        /// twice the real threat its raw Attack suggests, and nothing in the inspector shows that.
        /// </summary>
        private static float ComputeActionShare(SimUnit enemy, PartyBaseline party)
        {
            if (party.Size == 0)
            {
                return 1f;
            }

            float partyTurnsPerTick = 0f;
            foreach (var hero in party.Heroes)
            {
                partyTurnsPerTick += BalanceMath.TurnsPerTick(hero.Unit);
            }

            float partyAverage = partyTurnsPerTick / party.Size;
            if (partyAverage <= 0f)
            {
                return 1f;
            }

            return BalanceMath.TurnsPerTick(enemy) / partyAverage;
        }

        /// <summary>The danger band this enemy should be judged against, given boss status.</summary>
        public float DangerCeiling(BalanceRulesSO rules)
        {
            return IsBoss ? rules.MaxBossDanger : rules.MaxTrashDanger;
        }

        public float TimeToKillCeiling(BalanceRulesSO rules)
        {
            return IsBoss ? rules.MaxBossTimeToKill : rules.MaxEnemyTimeToKill;
        }
    }
}
