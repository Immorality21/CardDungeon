using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>How badly a measured value misses the target set in <see cref="BalanceRulesSO"/>.</summary>
    public enum BalanceSeverity
    {
        Ok,        // inside the target band
        Info,      // worth knowing, not a problem
        Warning,   // outside the band but playable
        Critical   // broken: unwinnable, one-shot, or a dead mechanic
    }

    /// <summary>Areas a finding can belong to — used to group the Issues list.</summary>
    public static class BalanceCategory
    {
        public const string Party = "Party";
        public const string Enemy = "Enemy";
        public const string Level = "Level";
        public const string Run = "Run";
        public const string Variety = "Variety";
        public const string Economy = "Economy";
        public const string Progression = "Progression";
        public const string Simulation = "Simulation";
        public const string Save = "Save";
    }

    /// <summary>
    /// One balance finding: what is off, by how much, and (where the fix is mechanical) the value
    /// that would bring it inside the target band. Carries the offending asset so the editor
    /// window can select and edit it in place.
    /// </summary>
    public class BalanceIssue
    {
        public BalanceSeverity Severity = BalanceSeverity.Warning;
        public string Category = BalanceCategory.Enemy;

        /// <summary>Name of the thing the finding is about (enemy, hero, level, magic).</summary>
        public string Subject = "";

        /// <summary>One-line statement of the defect.</summary>
        public string Title = "";

        /// <summary>The numbers behind the claim, so the value is checkable rather than asserted.</summary>
        public string Detail = "";

        /// <summary>Optional suggested change. Advice only — nothing is applied automatically.</summary>
        public string Suggestion = "";

        /// <summary>Asset the finding points at, for click-to-select / inline editing.</summary>
        public Object Asset;

        public BalanceIssue() { }

        public BalanceIssue(BalanceSeverity severity, string category, string subject, string title)
        {
            Severity = severity;
            Category = category;
            Subject = subject;
            Title = title;
        }
    }

    /// <summary>
    /// One simulated encounter, played out under every policy. The gap between attack-spam and
    /// competent play is the encounter's depth: a small gap means the fight plays itself.
    /// </summary>
    public class EncounterSimReport
    {
        public string Label = "";
        public Object Asset;
        public bool IsBoss;
        public List<SimUnit> Enemies = new List<SimUnit>();
        public Dictionary<SimPolicy, SimOutcome> Outcomes = new Dictionary<SimPolicy, SimOutcome>();

        public SimOutcome Best
        {
            get
            {
                SimOutcome best = null;
                foreach (var kvp in Outcomes)
                {
                    if (best == null || kvp.Value.Score > best.Score)
                    {
                        best = kvp.Value;
                    }
                }
                return best;
            }
        }

        public SimOutcome AttackOnly =>
            Outcomes.TryGetValue(SimPolicy.AttackOnly, out var outcome) ? outcome : null;

        /// <summary>True when some battles hit the turn cap — neither side able to finish the other.</summary>
        public bool HasStalemates()
        {
            foreach (var kvp in Outcomes)
            {
                if (kvp.Value != null && kvp.Value.Stalemates > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How much better the best policy is than attack-spam. Near zero = no decisions.</summary>
        public float DepthGap
        {
            get
            {
                var best = Best;
                var floor = AttackOnly;
                if (best == null || floor == null)
                {
                    return 0f;
                }
                return best.Score - floor.Score;
            }
        }
    }

    /// <summary>The full output of an analysis pass: the per-area records plus the flat issue list.</summary>
    public class BalanceReport
    {
        public PartyBaseline Party;
        public List<EnemyMetrics> Enemies = new List<EnemyMetrics>();
        public List<RunCurve> Runs = new List<RunCurve>();
        public VarietyReport Variety;
        public List<EncounterSimReport> Simulations = new List<EncounterSimReport>();
        public SaveAudit Save;
        public List<BalanceIssue> Issues = new List<BalanceIssue>();

        public int CountOf(BalanceSeverity severity)
        {
            int count = 0;
            foreach (var issue in Issues)
            {
                if (issue.Severity == severity)
                {
                    count++;
                }
            }
            return count;
        }

        public BalanceSeverity WorstSeverity()
        {
            var worst = BalanceSeverity.Ok;
            foreach (var issue in Issues)
            {
                if (issue.Severity > worst)
                {
                    worst = issue.Severity;
                }
            }
            return worst;
        }

        /// <summary>Issues ordered worst-first, then by category and subject, for display.</summary>
        public List<BalanceIssue> SortedIssues()
        {
            var sorted = new List<BalanceIssue>(Issues);
            sorted.Sort((a, b) =>
            {
                int bySeverity = b.Severity.CompareTo(a.Severity);
                if (bySeverity != 0)
                {
                    return bySeverity;
                }
                int byCategory = string.Compare(a.Category, b.Category, System.StringComparison.Ordinal);
                if (byCategory != 0)
                {
                    return byCategory;
                }
                return string.Compare(a.Subject, b.Subject, System.StringComparison.Ordinal);
            });
            return sorted;
        }
    }
}
