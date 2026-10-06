using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// The pure rules of a revisit (<c>docs/plans/REVISITS.md</c>): which runs can be revisited, what
    /// a set of chosen conditions adds up to, and the small edits the condition picker makes. Scene-
    /// free, like <c>CampaignOps</c>; <c>RevisitTests</c> drives it.
    /// </summary>
    public static class RevisitOps
    {
        /// <summary>
        /// Whether a cleared run comes back as a revisit. Authored per run (<c>Repeatable</c>) rather
        /// than ruled: today that is every run but the tutorial, the challenge run and the secret
        /// runs (owner, 2026-10-06, "for now"), and <c>RevisitTests</c> pins that list.
        /// </summary>
        public static bool IsRevisitable(RunDefinitionSO run)
        {
            return run != null && run.Repeatable;
        }

        /// <summary>Whether starting <paramref name="run"/> now would be a revisit.</summary>
        public static bool IsRevisit(RunDefinitionSO run, ICollection<string> completedRunKeys)
        {
            return IsRevisitable(run)
                && completedRunKeys != null
                && completedRunKeys.Contains(CampaignOps.RunKeyOf(run));
        }

        /// <summary>
        /// What a run plays under. Not a revisit means <see cref="RunFear.None"/>. Unknown keys are
        /// ignored and ranks clamped, so a save naming a removed condition still loads.
        /// </summary>
        public static RunFear Resolve(RevisitRulesSO rules, bool isRevisit, IEnumerable<RunModifierSelection> selections)
        {
            if (!isRevisit || rules == null)
            {
                return isRevisit ? new RunFear { IsRevisit = true } : RunFear.None;
            }

            // The base scales every stat; conditions add targeted percentages on top of it.
            int healthPercent = 0;
            int damagePercent = 0;
            int extraEnemies = 0;
            int healingPercent = 100;
            int agilityPercent = 0;
            int fear = 0;

            if (selections != null)
            {
                foreach (var selection in selections)
                {
                    var modifier = selection != null ? rules.Find(selection.Key) : null;
                    int rank = modifier != null ? ClampRank(modifier, selection.Rank) : 0;
                    if (rank <= 0)
                    {
                        continue;
                    }

                    fear += modifier.FearPerRank * rank;
                    if (modifier.Effects == null)
                    {
                        continue;
                    }

                    foreach (var effect in modifier.Effects)
                    {
                        if (effect == null)
                        {
                            continue;
                        }

                        int amount = effect.AmountPerRank * rank;
                        switch (effect.Kind)
                        {
                            case RunModifierEffectKind.EnemyHealthPercent:
                                healthPercent += amount;
                                break;
                            case RunModifierEffectKind.EnemyDamagePercent:
                                damagePercent += amount;
                                break;
                            case RunModifierEffectKind.ExtraEnemiesPerRoom:
                                extraEnemies += amount;
                                break;
                            case RunModifierEffectKind.HeroHealingPercent:
                                healingPercent += amount;
                                break;
                            case RunModifierEffectKind.EnemyAgilityPercent:
                                agilityPercent += amount;
                                break;
                        }
                    }
                }
            }

            return new RunFear
            {
                IsRevisit = true,
                Level = fear,
                EnemyStatMultiplier = Mathf.Max(0.1f, 1f + rules.BaseEnemyStatPercent / 100f),
                EnemyHealthMultiplier = Mathf.Max(0.1f, 1f + healthPercent / 100f),
                EnemyDamageMultiplier = Mathf.Max(0.1f, 1f + damagePercent / 100f),
                EnemyAgilityMultiplier = Mathf.Max(0.1f, 1f + agilityPercent / 100f),
                ExtraEnemiesPerRoom = Mathf.Max(0, extraEnemies),
                HeroHealingMultiplier = Mathf.Max(0, healingPercent) / 100f,
                RewardMultiplier = RewardMultiplierFor(rules, fear),
            };
        }

        /// <summary>
        /// Fills <see cref="RunFear.WithheldItemKeys"/>: on a revisit that does not beat
        /// <paramref name="bestClearedFear"/> (-1 = never revisited), the rules' scarce materials do
        /// not drop. A first clear and a new best both pay them.
        /// </summary>
        public static void ApplyScarcity(RunFear fear, RevisitRulesSO rules, int bestClearedFear)
        {
            if (fear == null || fear == RunFear.None)
            {
                return;
            }
            fear.WithheldItemKeys = new List<string>();
            if (!fear.IsRevisit || rules == null || rules.NewBestFearOnly == null || fear.Level > bestClearedFear)
            {
                return;
            }
            foreach (var item in rules.NewBestFearOnly)
            {
                if (item != null && !string.IsNullOrEmpty(item.Key))
                {
                    fear.WithheldItemKeys.Add(item.Key);
                }
            }
        }

        /// <summary>
        /// What a revisit does, in one line the player reads before going in and in combat: every
        /// change the fear makes, then the reward. Empty for a run that is not a revisit.
        /// </summary>
        public static string Summary(RunFear fear)
        {
            if (fear == null || !fear.IsRevisit)
            {
                return string.Empty;
            }

            var parts = new List<string>
            {
                $"enemies {Signed(fear.EnemyStatMultiplier)} to every stat",
            };
            if (fear.EnemyHealthMultiplier > 1.001f)
            {
                parts.Add($"{Signed(fear.EnemyHealthMultiplier)} more health");
            }
            if (fear.EnemyDamageMultiplier > 1.001f)
            {
                parts.Add($"{Signed(fear.EnemyDamageMultiplier)} more damage");
            }
            if (fear.EnemyAgilityMultiplier > 1.001f)
            {
                parts.Add($"{Signed(fear.EnemyAgilityMultiplier)} more speed");
            }
            if (fear.ExtraEnemiesPerRoom > 0)
            {
                parts.Add($"+{fear.ExtraEnemiesPerRoom} {(fear.ExtraEnemiesPerRoom == 1 ? "foe" : "foes")} per fight");
            }
            if (fear.HeroHealingMultiplier < 0.999f)
            {
                parts.Add(fear.HeroHealingMultiplier <= 0f
                    ? "no healing"
                    : $"healing {Signed(fear.HeroHealingMultiplier)}");
            }
            return $"Fear level {fear.Level}: {string.Join(", ", parts)}. Rewards {Signed(fear.RewardMultiplier)} XP, gold and Essence.";
        }

        private static string Signed(float multiplier)
        {
            int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            return percent >= 0 ? $"+{percent}%" : $"−{-percent}%";
        }

        /// <summary>XP, gold and Essence multiplier of a revisit at <paramref name="fear"/>.</summary>
        public static float RewardMultiplierFor(RevisitRulesSO rules, int fear)
        {
            if (rules == null)
            {
                return 1f;
            }
            return Mathf.Max(0f, 1f + (rules.BaseRewardPercent + rules.RewardPercentPerFear * Mathf.Max(0, fear)) / 100f);
        }

        public static int ClampRank(RunModifier modifier, int rank)
        {
            if (modifier == null)
            {
                return 0;
            }
            return Mathf.Clamp(rank, 0, Mathf.Max(1, modifier.MaxRank));
        }

        /// <summary>The rank <paramref name="key"/> is chosen at, 0 when not chosen.</summary>
        public static int RankOf(IEnumerable<RunModifierSelection> selections, string key)
        {
            if (selections == null || string.IsNullOrEmpty(key))
            {
                return 0;
            }
            foreach (var selection in selections)
            {
                if (selection != null && selection.Key == key)
                {
                    return selection.Rank;
                }
            }
            return 0;
        }

        /// <summary>
        /// Sets one condition's rank in place, clamped. Rank 0 removes the entry, so what is saved is
        /// only what is on.
        /// </summary>
        public static void SetRank(RevisitRulesSO rules, List<RunModifierSelection> selections, string key, int rank)
        {
            if (selections == null)
            {
                return;
            }
            var modifier = rules != null ? rules.Find(key) : null;
            if (modifier == null)
            {
                return;
            }

            rank = ClampRank(modifier, rank);
            selections.RemoveAll(s => s == null || s.Key == key);
            if (rank > 0)
            {
                selections.Add(new RunModifierSelection(key, rank));
            }
        }

        /// <summary>One step up, wrapping past the top back to off - what a click on a condition does.</summary>
        public static void CycleRank(RevisitRulesSO rules, List<RunModifierSelection> selections, string key)
        {
            var modifier = rules != null ? rules.Find(key) : null;
            if (modifier == null)
            {
                return;
            }
            int next = RankOf(selections, key) + 1;
            SetRank(rules, selections, key, next > modifier.MaxRank ? 0 : next);
        }

        /// <summary>A condition's line at a rank: its description with {0} as the rank's total amount.</summary>
        public static string Describe(RunModifier modifier, int rank)
        {
            if (modifier == null)
            {
                return string.Empty;
            }
            string text = modifier.Description ?? string.Empty;
            if (!text.Contains("{0}"))
            {
                return text;
            }

            int shown = Mathf.Max(1, rank);
            int amount = modifier.Effects != null && modifier.Effects.Count > 0 && modifier.Effects[0] != null
                ? Mathf.Abs(modifier.Effects[0].AmountPerRank) * shown
                : shown;
            return text.Replace("{0}", amount.ToString());
        }
    }
}
