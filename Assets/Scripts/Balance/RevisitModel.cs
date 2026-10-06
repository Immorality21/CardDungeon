using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// One replayable run, revisited at Fear level 0 by the party that just cleared it: what the
    /// revisit costs that party and what it pays per hero.
    /// </summary>
    public class RevisitCurve
    {
        public RunDefinitionSO Run;
        public string Name = "";

        /// <summary>The run modelled as a revisit (Fear level 0, the base only).</summary>
        public RunCurve Curve;

        /// <summary>XP one revisit pays each fielded hero, at the even split.</summary>
        public float XpPerHero;

        /// <summary>
        /// Worst ordinary floor's attrition on the revisit, for the party that cleared the run (see
        /// <see cref="RevisitModel.PeakAttrition"/> for why the boss floor is kept apart).
        /// </summary>
        public float PeakAttrition;

        /// <summary>The finale's attrition, reported beside the peak but not judged on.</summary>
        public float BossAttrition;
    }

    /// <summary>
    /// A run that opens after a replayable one, measured against the balancing guideline
    /// (<c>docs/BALANCING.md</c>, "Revisits pace the campaign"): the straight-line party should be
    /// pushed, and a few revisits of the run behind it should be enough to catch up.
    /// </summary>
    public class RevisitNudge
    {
        /// <summary>The run being judged.</summary>
        public RunDefinitionSO Run;
        public string Name = "";

        /// <summary>The replayable prerequisite the player would revisit to get stronger.</summary>
        public RevisitCurve Feeder;

        /// <summary>Worst ordinary floor's attrition for the party that arrives without revisiting.</summary>
        public float StraightLinePeak;

        /// <summary>The finale's attrition for that party - context, not judged.</summary>
        public float StraightLineBossAttrition;

        /// <summary>
        /// Worst floor's attrition after k revisits' XP, for k = 1.. the rule's maximum. Entry 0 is
        /// one revisit.
        /// </summary>
        public List<float> PeakAfterRevisits = new List<float>();

        /// <summary>Fewest revisits after which every floor sits under the catch-up line, or -1.</summary>
        public int RevisitsToCatchUp = -1;
    }

    /// <summary>
    /// Teaches the balance model about revisits (docs/plans/REVISITS.md). Two measurements, both
    /// built from <see cref="RunCurve.Build(RunDefinitionSO, PartyBaseline, BalanceRulesSO, IReadOnlyList{HeroSO}, IReadOnlyDictionary{HeroSO, int}, RunFear)"/>
    /// so a revisit is priced by the exact rule the game spawns it with:
    /// <list type="bullet">
    /// <item>each replayable run at Fear level 0 - what one revisit costs and pays;</item>
    /// <item>each run downstream of a replayable one, twice: with the straight-line party, and with
    /// that party plus k revisits' XP - which is what the nudge guideline is judged on.</item>
    /// </list>
    /// </summary>
    public static class RevisitModel
    {
        public static void Build(CampaignSO campaign, BalanceReport report, BalanceRulesSO rules)
        {
            report.Revisits.Clear();
            report.RevisitNudges.Clear();
            if (campaign == null || campaign.Nodes == null || rules == null || report.Party == null)
            {
                return;
            }

            var revisitRules = RevisitRulesSO.Load();
            var fear = RevisitOps.Resolve(revisitRules, true, null);

            var byRun = new Dictionary<RunDefinitionSO, RunCurve>();
            foreach (var curve in report.Runs)
            {
                if (curve?.Run != null && !byRun.ContainsKey(curve.Run))
                {
                    byRun[curve.Run] = curve;
                }
            }

            var feeders = new Dictionary<RunDefinitionSO, RevisitCurve>();
            foreach (var node in CampaignOps.GetNodesInPlayOrder(campaign))
            {
                var run = node?.Run;
                if (run == null || !RevisitOps.IsRevisitable(run) || !byRun.TryGetValue(run, out var cleared))
                {
                    continue;
                }

                var revisit = RunCurve.Build(run, report.Party, rules, cleared.EndRoster, cleared.EndLifetimeXp, fear);
                var entry = new RevisitCurve
                {
                    Run = run,
                    Name = cleared.Name,
                    Curve = revisit,
                    XpPerHero = XpSplit.ExpectedShare(revisit.TotalExpectedXp, Mathf.Max(1, cleared.EndRoster.Count)),
                    PeakAttrition = PeakAttrition(revisit),
                    BossAttrition = BossAttrition(revisit),
                };
                feeders[run] = entry;
                report.Revisits.Add(entry);
            }

            int maxRevisits = Mathf.Max(1, rules.RevisitCatchUpMaxRevisits);
            foreach (var node in CampaignOps.GetNodesInPlayOrder(campaign))
            {
                var run = node?.Run;
                if (run == null || run.Challenge || node.Requires == null || !byRun.TryGetValue(run, out var straight))
                {
                    continue;
                }

                // The replayable prerequisite the player would go back to. With more than one, the
                // one that pays the most per revisit - that is the one a player would farm.
                RevisitCurve feeder = null;
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite != null && feeders.TryGetValue(prerequisite, out var candidate) &&
                        (feeder == null || candidate.XpPerHero > feeder.XpPerHero))
                    {
                        feeder = candidate;
                    }
                }
                if (feeder == null || !byRun.TryGetValue(feeder.Run, out var feederCleared))
                {
                    continue;
                }

                var nudge = new RevisitNudge
                {
                    Run = run,
                    Name = straight.Name,
                    Feeder = feeder,
                    StraightLinePeak = PeakAttrition(straight),
                    StraightLineBossAttrition = BossAttrition(straight),
                };

                for (int k = 1; k <= maxRevisits; k++)
                {
                    var funded = new Dictionary<HeroSO, int>();
                    foreach (var pair in feederCleared.EndLifetimeXp)
                    {
                        funded[pair.Key] = pair.Value + Mathf.FloorToInt(feeder.XpPerHero * k);
                    }
                    var curve = RunCurve.Build(run, report.Party, rules, feederCleared.EndRoster, funded);
                    float peak = PeakAttrition(curve);
                    nudge.PeakAfterRevisits.Add(peak);
                    if (nudge.RevisitsToCatchUp < 0 && peak <= rules.RevisitCatchUpMaxAttrition)
                    {
                        nudge.RevisitsToCatchUp = k;
                    }
                }

                report.RevisitNudges.Add(nudge);
            }
        }

        /// <summary>
        /// The worst <b>ordinary</b> floor's attrition. Boss floors are left out on purpose: the
        /// closed form overstates a sealed room of dense bodies several times over
        /// (docs/BALANCING.md 5k), and a finale is a tier gate the frontier judges, not the attrition
        /// ceiling. A run that is only a boss floor (The Hollow Vault) falls back to it.
        /// </summary>
        public static float PeakAttrition(RunCurve curve)
        {
            float peak = 0f;
            bool any = false;
            if (curve?.Levels == null)
            {
                return peak;
            }
            foreach (var level in curve.Levels)
            {
                if (level != null && !level.IsBossLevel)
                {
                    peak = Mathf.Max(peak, level.AttritionLoad);
                    any = true;
                }
            }
            return any ? peak : BossAttrition(curve);
        }

        /// <summary>The highest boss floor's attrition, or 0 for a run with none.</summary>
        public static float BossAttrition(RunCurve curve)
        {
            float peak = 0f;
            if (curve?.Levels == null)
            {
                return peak;
            }
            foreach (var level in curve.Levels)
            {
                if (level != null && level.IsBossLevel)
                {
                    peak = Mathf.Max(peak, level.AttritionLoad);
                }
            }
            return peak;
        }
    }
}
