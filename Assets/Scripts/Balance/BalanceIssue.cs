using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
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
        public const string Event = "Event";
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

    /// <summary>
    /// One floor, simulated end to end off a single pool of health, potions and charges.
    ///
    /// <para>This is the report the per-encounter <see cref="EncounterSimReport"/> cannot produce. That
    /// one re-clones a full-health party with a full potion belt for every room, so it measures a
    /// floor of four rooms as four independent opening fights and can never report the way a run
    /// actually ends: attrition compounding across rooms, with no revive to undo a hero lost on the
    /// way.</para>
    /// </summary>
    public class FloorSimReport
    {
        public string Label = "";
        public Object Asset;
        public RunDefinitionSO Run;
        public int LevelIndex = -1;

        /// <summary>Combat rooms fought, in order, boss last.</summary>
        public int Rooms;

        /// <summary>The closed-form attrition the run curve predicted for this floor, for comparison.</summary>
        public float PredictedAttrition;

        /// <summary>True for a run's first floor, the only one that starts on full charges.</summary>
        public bool StartsWithFullCharges;

        public Dictionary<SimPolicy, EncounterSimulator.FloorOutcome> Outcomes =
            new Dictionary<SimPolicy, EncounterSimulator.FloorOutcome>();

        /// <summary>
        /// The outcome to judge the floor by: competent play, not the best of three. A floor is too
        /// lethal only if it is too lethal for someone playing well.
        /// </summary>
        public EncounterSimulator.FloorOutcome Adaptive =>
            Outcomes.TryGetValue(SimPolicy.Adaptive, out var outcome) ? outcome : null;

        /// <summary>The kindest reading across policies - the floor at its most survivable.</summary>
        public EncounterSimulator.FloorOutcome Safest
        {
            get
            {
                EncounterSimulator.FloorOutcome best = null;
                foreach (var kvp in Outcomes)
                {
                    if (best == null || kvp.Value.WipeRate < best.WipeRate)
                    {
                        best = kvp.Value;
                    }
                }
                return best;
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
        public ProgressionMap Progression;
        /// <summary>
        /// The party each enemy is first met with, so enemy metrics *and* the simulator judge it
        /// against the roster the player actually brings. Filled by the analyzer.
        /// </summary>
        public Dictionary<EnemySO, PartyBaseline> PartyByEnemy = new Dictionary<EnemySO, PartyBaseline>();

        public List<EncounterSimReport> Simulations = new List<EncounterSimReport>();

        /// <summary>Per-floor simulations - the read on whether a run can actually be lost.</summary>
        public List<FloorSimReport> Floors = new List<FloorSimReport>();
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
