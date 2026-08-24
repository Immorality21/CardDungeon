using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Balance;
using Assets.Scripts.Balance.Editor;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The balance guard rail: these run the analyzer over the project's real assets and fail when a
    /// finding is outside the band set in <c>BalanceRules</c>. Unlike the balance window, a failing test
    /// cannot be left unopened — which is the whole point of having them.
    ///
    /// They are in the <c>Balance</c> category so they can be excluded from a quick unit-test pass, and
    /// each failure message carries the analyzer's own detail and suggestion so the Test Runner output
    /// is actionable on its own.
    ///
    /// Save-file state is deliberately excluded: it differs per machine, so a save-dependent assertion
    /// would not mean the same thing twice. Simulation is excluded too — it belongs in the window where
    /// its runtime is acceptable.
    /// </summary>
    [Category("Balance")]
    public class BalanceRegressionTests
    {
        private static BalanceReport _report;

        [OneTimeSetUp]
        public void Analyze()
        {
            var rules = BalanceAssetCollector.LoadOrCreateRules(false);
            var input = BalanceAssetCollector.Collect(rules, runSimulation: false, includeSaveAudit: false);
            _report = BalanceAnalyzer.Analyze(input);
        }

        [Test]
        public void Analyzer_ResolvesTheProjectsAssets()
        {
            Assert.IsNotNull(_report, "The analyzer returned no report.");
            Assert.IsNotNull(_report.Party, "No reference party could be built.");
            Assert.Greater(_report.Party.Size, 0, "No HeroSO assets were found in the project.");
            Assert.Greater(_report.Enemies.Count, 0, "No EnemySO assets were found in the project.");
        }

        [Test]
        public void NoEnemyOneShotsAHero()
        {
            AssertNoIssues(
                issue => issue.Title.Contains("one-shot"),
                "An enemy kills a hero in a single hit, so the fight is decided by turn order rather than play.");
        }

        [Test]
        public void EveryHeroSurvivesTheMinimumNumberOfHits()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Party && issue.Title.Contains("survives only"),
                "A hero dies to fewer ordinary hits than the rules allow.");
        }

        [Test]
        public void EveryEnemyIsInsideItsDangerBand()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Enemy
                         && (issue.Title.Contains("danger band") || issue.Title.Contains("beats the party")),
                "An enemy's danger index sits outside the band for its tier.");
        }

        [Test]
        public void EveryRunLevelIsClearableOnOneHealthBar()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Level && issue.Title.Contains("unclearable"),
                "A level costs more health than the party's HP and potion pool can cover, and health only "
                + "refills between levels.");
        }

        [Test]
        public void RunDifficultyEscalates()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Run && issue.Title.Contains("Difficulty spikes"),
                "A run level is far harder than the one before it.");
        }

        [Test]
        public void BossesStandProportionateToTheirLevel()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Level && issue.Title.Contains("trash difficulty"),
                "A boss is out of proportion with the level that leads up to it.");
        }

        [Test]
        public void TheElementalLayerAffectsSomething()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Variety
                         && (issue.Title.Contains("elemental layer") || issue.Title.Contains("nothing resists")),
                "No enemy resists anything, so every DamageType is arithmetically identical and choosing "
                + "which magic to cast can never matter.");
        }

        [Test]
        public void EveryHeroHasSomewhereToSpendXp()
        {
            // The grid-shaped successor to the old "somewhere to level to" check: a hero with no
            // sphere grid (or a token one) turns every kill's XP into a number that buys nothing.
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Progression
                         && (issue.Title.Contains("has no sphere grid") || issue.Title.Contains("grid runs out")),
                "A hero's sphere grid is missing or runs out almost immediately, so XP stops mattering.");
        }

        [Test]
        public void EveryRoomEventCanActuallyBePlaced()
        {
            // Room events are authored content reached through a chain that is easy to break by
            // accident: the event needs a room to list it, that room needs to be in a run's pool, its
            // SpawnChancePercent needs to be above zero, and some level's party needs to clear its
            // SpawnRequirements. Miss any link and the asset exists but nobody ever sees it.
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Event
                         && (issue.Title.Contains("No room in any run offers")
                             || issue.Title.Contains("can never be placed")
                             || issue.Title.Contains("No hero in the project can reach")),
                "A room event can never appear in play.");
        }

        [Test]
        public void EveryRoomEventHasSomethingToDo()
        {
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Event && issue.Title.Contains("nothing to do"),
                "A room event offers no option but walking away, so its Action button is a dead end.");
        }

        [Test]
        public void NoLevelTakesMostOfItsDifficultyFromRoomEvents()
        {
            // Events cost from the same health bar the fights do. Past the band in BalanceRules the
            // level's difficulty is coming from gambles in corridors, and retuning the spawn tables
            // will not move it.
            AssertNoIssues(
                issue => issue.Category == BalanceCategory.Level
                         && issue.Title.Contains("attrition from room events"),
                "A level takes more of its attrition from room events than the rules allow.");
        }

        [Test]
        public void NothingIsCriticallyOutOfBand()
        {
            // The umbrella assertion. Everything above is a named slice of this one, so this is what to
            // watch when adding content: any new critical finding fails here even if no specific test
            // covers its shape yet.
            AssertNoIssues(
                issue => issue.Severity == BalanceSeverity.Critical,
                "At least one measured value is critically outside its band.");
        }

        private static void AssertNoIssues(System.Func<BalanceIssue, bool> predicate, string summary)
        {
            var matches = new List<BalanceIssue>();
            foreach (var issue in _report.Issues)
            {
                if (predicate(issue))
                {
                    matches.Add(issue);
                }
            }

            if (matches.Count == 0)
            {
                return;
            }

            var message = new StringBuilder();
            message.AppendLine(summary);
            message.AppendLine();

            foreach (var issue in matches)
            {
                message.AppendLine($"[{issue.Severity}] {issue.Subject}: {issue.Title}");
                if (!string.IsNullOrEmpty(issue.Detail))
                {
                    message.AppendLine($"    {issue.Detail}");
                }
                if (!string.IsNullOrEmpty(issue.Suggestion))
                {
                    message.AppendLine($"    → {issue.Suggestion}");
                }
                message.AppendLine();
            }

            message.AppendLine("Open Tools ▸ Balance ▸ Balance Analyzer to edit these values in place.");
            Assert.Fail(message.ToString());
        }
    }
}
