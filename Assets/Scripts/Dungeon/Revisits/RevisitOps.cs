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
        /// What a run plays under. Not a revisit means <see cref="RunHeat.None"/>. Unknown keys are
        /// ignored and ranks clamped, so a save naming a removed condition still loads.
        /// </summary>
        public static RunHeat Resolve(RevisitRulesSO rules, bool isRevisit, IEnumerable<RunModifierSelection> selections)
        {
            if (!isRevisit || rules == null)
            {
                return isRevisit ? new RunHeat { IsRevisit = true } : RunHeat.None;
            }

            int healthPercent = rules.BaseEnemyHealthPercent;
            int damagePercent = rules.BaseEnemyDamagePercent;
            int extraEnemies = 0;
            int healingPercent = 100;
            int heat = 0;

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

                    heat += modifier.HeatPerRank * rank;
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
                        }
                    }
                }
            }

            return new RunHeat
            {
                IsRevisit = true,
                Heat = heat,
                EnemyHealthMultiplier = Mathf.Max(0.1f, 1f + healthPercent / 100f),
                EnemyDamageMultiplier = Mathf.Max(0.1f, 1f + damagePercent / 100f),
                ExtraEnemiesPerRoom = Mathf.Max(0, extraEnemies),
                HeroHealingMultiplier = Mathf.Max(0, healingPercent) / 100f,
                RewardMultiplier = RewardMultiplierFor(rules, heat),
            };
        }

        /// <summary>
        /// Fills <see cref="RunHeat.WithheldItemKeys"/>: on a revisit that does not beat
        /// <paramref name="bestClearedHeat"/> (-1 = never revisited), the rules' scarce materials do
        /// not drop. A first clear and a new best both pay them.
        /// </summary>
        public static void ApplyScarcity(RunHeat heat, RevisitRulesSO rules, int bestClearedHeat)
        {
            if (heat == null || heat == RunHeat.None)
            {
                return;
            }
            heat.WithheldItemKeys = new List<string>();
            if (!heat.IsRevisit || rules == null || rules.NewBestHeatOnly == null || heat.Heat > bestClearedHeat)
            {
                return;
            }
            foreach (var item in rules.NewBestHeatOnly)
            {
                if (item != null && !string.IsNullOrEmpty(item.Key))
                {
                    heat.WithheldItemKeys.Add(item.Key);
                }
            }
        }

        /// <summary>XP, gold and Essence multiplier of a revisit at <paramref name="heat"/>.</summary>
        public static float RewardMultiplierFor(RevisitRulesSO rules, int heat)
        {
            if (rules == null)
            {
                return 1f;
            }
            return Mathf.Max(0f, 1f + (rules.BaseRewardPercent + rules.RewardPercentPerHeat * Mathf.Max(0, heat)) / 100f);
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
