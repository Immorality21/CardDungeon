using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// The Elements &amp; Unlocks tab: the supply side of the elemental layer. Resistances and combos are
    /// authored content that only becomes live if the Draw tables hand the player the pieces, so this tab
    /// puts the two next to each other — what each level resists, and what the player can actually bring
    /// to it by that point in the run order.
    /// </summary>
    public partial class BalanceWindow
    {
        private const float ElementNameWidth = 130f;
        private const float ElementCellWidth = 88f;

        private void DrawElementsTab()
        {
            var map = _report != null ? _report.Progression : null;
            if (map == null)
            {
                EditorGUILayout.HelpBox("Nothing to measure.", MessageType.Info);
                return;
            }

            BalanceGui.Paragraph(
                "Resistances only create decisions if the player can bring the element to decide with. This tab "
                + "reads the Draw tables as a supply chain: what each run unlocks, when each combo becomes "
                + "possible, and whether a level's resistances are in elements the player already has.",
                BalanceGui.WrapMiniStyle);

            DrawFilterRow("Show only unreachable magic and combos.");

            BeginScroll();

            DrawUnlockTimeline(map);
            EditorGUILayout.Space(8f);
            DrawMagicMatrix(map);
            EditorGUILayout.Space(8f);
            DrawComboReachability(map);

            EndScroll();
        }

        // ---------------------------------------------------------------- unlock timeline

        private void DrawUnlockTimeline(ProgressionMap map)
        {
            BalanceGui.SectionHeader(
                "Unlock timeline",
                "Runs in play order. SequenceIndex is editable — runs are not chained in game yet, so this is "
                + "purely the order the analysis assumes.");

            if (map.RunOrderIsImplicit)
            {
                EditorGUILayout.HelpBox(
                    "No run sets a SequenceIndex, so the order below is alphabetical by asset name and may not "
                    + "be the intended play order.",
                    MessageType.Info);
            }

            foreach (var run in map.Runs)
            {
                EditorGUILayout.BeginHorizontal();

                var serialized = Serialized(run.Run);
                BalanceGui.HeaderCell("seq", 28f, "SequenceIndex — 0 plays first.");
                bool changed = BalanceGui.EditableCell(serialized, "SequenceIndex", 34f);
                Commit(serialized, changed);

                BalanceGui.AssetCell(run.Run, run.Name, NameWidth);
                BalanceGui.Cell($"{run.NewlyDrawable.Count} new magic", MetricWidth + 20f,
                    run.NewlyDrawable.Count == 0 ? BalanceSeverity.Info : BalanceSeverity.Ok);
                BalanceGui.Cell($"{run.NewlyEnabledCombos.Count} new combo(s)", MetricWidth + 30f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.Paragraph(
                    run.NewlyDrawable.Count > 0
                        ? "unlocks: " + string.Join(", ", MagicNames(run.NewlyDrawable))
                        : "unlocks nothing new",
                    BalanceGui.WrapMiniStyle);
                EditorGUILayout.EndHorizontal();

                if (run.NewlyEnabledCombos.Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16f);
                    BalanceGui.Paragraph("enables combos: " + string.Join(", ", ComboNames(run.NewlyEnabledCombos)),
                        BalanceGui.WrapMiniStyle);
                    EditorGUILayout.EndHorizontal();
                }

                DrawRunLevelElements(run);
                EditorGUILayout.Space(6f);
            }
        }

        private void DrawRunLevelElements(RunProgression run)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            BalanceGui.HeaderCell("Level", NameWidth);
            BalanceGui.HeaderCell("Enemies", 60f, "Expected enemies across the level.");
            BalanceGui.HeaderCell("Resists", 130f, "Share of the level's enemies carrying a resistance.");
            BalanceGui.HeaderCell("Weak", 130f, "Share carrying a weakness (negative resistance).");
            BalanceGui.HeaderCell("Resisted types", 150f);
            BalanceGui.HeaderCell("Weak types", 150f);
            BalanceGui.HeaderCell("Deals", 150f, "Elements the level's enemies attack with — what the party has to defend against.");
            BalanceGui.HeaderCell("Player has", 150f, "Elements the player can deal by this point, Normal aside.");
            BalanceGui.HeaderCell("Matters?", 70f, "Whether any resistance here is in an element the player has.");
            BalanceGui.HeaderCell("Unlocks here", 200f);
            EditorGUILayout.EndHorizontal();

            foreach (var level in run.Levels)
            {
                if (!level.HasCombat)
                {
                    continue;
                }

                bool hasAny = level.ResistingWeight + level.WeakWeight > 0f;
                var coverageSeverity = level.ResistanceCoverage <= 0f
                    ? BalanceSeverity.Warning
                    : level.ResistanceCoverage < _rules.MinResistanceCoverage
                        ? BalanceSeverity.Warning
                        : BalanceSeverity.Ok;

                var mattersSeverity = !hasAny
                    ? BalanceSeverity.Warning
                    : level.ElementChoiceMatters
                        ? BalanceSeverity.Ok
                        : BalanceSeverity.Warning;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.Cell(level.Reference, NameWidth);
                BalanceGui.Cell($"{level.EnemyWeight:0.0}", 60f);

                EditorGUILayout.BeginHorizontal(GUILayout.Width(130f));
                Bar(level.ResistanceCoverage, 74f, BalanceGui.TextColorFor(coverageSeverity),
                    $"{level.ResistanceCoverage:P0} of expected enemies resist something. "
                    + $"Floor is {_rules.MinResistanceCoverage:P0}.");
                BalanceGui.Cell($"{level.ResistanceCoverage:P0}", 52f, coverageSeverity);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(GUILayout.Width(130f));
                Bar(level.WeaknessCoverage, 74f, new Color(0.5f, 0.8f, 1f),
                    $"{level.WeaknessCoverage:P0} of expected enemies are weak to something.");
                BalanceGui.Cell($"{level.WeaknessCoverage:P0}", 52f);
                EditorGUILayout.EndHorizontal();

                BalanceGui.Cell(FormatTypes(level.ResistWeightByType), 150f);
                BalanceGui.Cell(FormatTypes(level.WeakWeightByType), 150f);
                BalanceGui.Cell(
                    FormatTypes(level.IncomingWeightByType),
                    150f,
                    level.UndefendableIncoming.Count > 0 ? BalanceSeverity.Warning : BalanceSeverity.Ok,
                    level.UndefendableIncoming.Count > 0
                        ? $"Nothing in the project resists {string.Join(", ", level.UndefendableIncoming)} — "
                          + "no gear grants it and no magic buffs it, so the element is pure downside."
                        : "Physical (Normal) attacks are not listed: they bypass the elemental layer.");
                BalanceGui.Cell(
                    level.ElementsAvailable.Count > 0 ? string.Join(", ", level.ElementsAvailable) : "none",
                    150f,
                    level.ElementsAvailable.Count == 0 ? BalanceSeverity.Warning : BalanceSeverity.Ok);
                BalanceGui.Cell(
                    !hasAny ? "no resist" : level.ElementChoiceMatters ? "yes" : "NOT YET",
                    70f, mattersSeverity,
                    hasAny && !level.ElementChoiceMatters
                        ? "This level's resistances are in elements the player cannot obtain yet, so they cannot "
                          + "change any decision here."
                        : null);
                BalanceGui.Cell(
                    level.NewlyDrawable.Count > 0 ? string.Join(", ", MagicNames(level.NewlyDrawable)) : "—",
                    200f);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ---------------------------------------------------------------- magic matrix

        private void DrawMagicMatrix(ProgressionMap map)
        {
            BalanceGui.SectionHeader(
                "Magic availability",
                $"Draw coverage {map.ReachableMagicCount}/{map.CatalogMagicCount}. One column per run: the levels "
                + "that offer the magic, with the enemies in the tooltip. Draw is the only route to new magic, so "
                + "anything unreachable here is unreachable in play.");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Magic", ElementNameWidth);
            BalanceGui.HeaderCell("Element", 76f, "Damage types this magic deals.");
            BalanceGui.HeaderCell("Tags", 110f, "Combo tags it applies.");
            BalanceGui.HeaderCell("First unlock", 150f, "Earliest point in the run order it can be drawn.");
            foreach (var run in map.Runs)
            {
                BalanceGui.HeaderCell(run.Name, ElementCellWidth, run.Name);
            }
            EditorGUILayout.EndHorizontal();

            foreach (var availability in map.Magic)
            {
                if (_problemsOnly && availability.IsReachable)
                {
                    continue;
                }
                if (!MatchesFilter(availability.DisplayName) && !MatchesFilter(availability.Key))
                {
                    continue;
                }

                var severity = availability.IsReachable
                    ? (availability.BossGatedOnly ? BalanceSeverity.Info : BalanceSeverity.Ok)
                    : BalanceSeverity.Warning;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(availability.Magic, availability.DisplayName, ElementNameWidth, severity);
                BalanceGui.Cell(
                    availability.DamageTypes.Count > 0 ? string.Join(", ", availability.DamageTypes) : "—",
                    76f);
                BalanceGui.Cell(
                    availability.Tags.Count > 0 ? string.Join(", ", availability.Tags) : "none",
                    110f,
                    availability.Tags.Count == 0 ? BalanceSeverity.Info : BalanceSeverity.Ok,
                    availability.Tags.Count == 0 ? "No tags, so this magic can never take part in a combo." : null);

                if (availability.FirstSource != null)
                {
                    BalanceGui.Cell(
                        $"{availability.FirstSource.RunName} / {availability.FirstSource.LevelReference}",
                        150f, severity,
                        $"From {availability.FirstSource.EnemyName}"
                        + (availability.BossGatedOnly ? " (boss only)" : ""));
                }
                else
                {
                    BalanceGui.Cell("never", 150f, BalanceSeverity.Warning,
                        "No enemy offers this magic, so it cannot be drawn anywhere.");
                }

                foreach (var run in map.Runs)
                {
                    DrawAvailabilityCell(availability, run);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawAvailabilityCell(MagicAvailability availability, RunProgression run)
        {
            var levels = new List<string>();
            var detail = new List<string>();

            foreach (var source in availability.Sources)
            {
                if (source.Run != run.Run)
                {
                    continue;
                }
                levels.Add("L" + source.LevelIndex);
                detail.Add($"Levels[{source.LevelIndex}] {source.EnemyName} x{source.Charges} charges"
                    + (source.BossOnly ? " (boss)" : "")
                    + $" — {source.ExpectedEnemies:0.0} expected");
            }

            if (levels.Count == 0)
            {
                BalanceGui.Cell("—", ElementCellWidth);
                return;
            }

            BalanceGui.Cell(string.Join(",", levels), ElementCellWidth, BalanceSeverity.Ok,
                string.Join("\n", detail));
        }

        // ---------------------------------------------------------------- combos

        private void DrawComboReachability(ProgressionMap map)
        {
            BalanceGui.SectionHeader(
                "Combo reachability",
                $"{map.ReachableComboCount}/{map.Combos.Count} reachable. A combo needs one required tag already "
                + "on the target and another arriving with the incoming cast, so every required tag has to be "
                + "carried by magic the player can draw.");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Combo", ElementNameWidth);
            BalanceGui.HeaderCell("Requires", 140f);
            BalanceGui.HeaderCell("Reachable", 76f);
            BalanceGui.HeaderCell("Unlocked at", 170f, "Where the last required piece becomes drawable.");
            BalanceGui.HeaderCell("Enabled by", 300f);
            EditorGUILayout.EndHorizontal();

            foreach (var combo in map.Combos)
            {
                if (_problemsOnly && combo.IsReachable)
                {
                    continue;
                }
                if (!MatchesFilter(combo.Name) && !MatchesFilter(combo.Key))
                {
                    continue;
                }

                var severity = combo.IsReachable
                    ? BalanceSeverity.Ok
                    : combo.TagsWithNoMagic.Count > 0
                        ? BalanceSeverity.Critical
                        : BalanceSeverity.Warning;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(combo.Combo, combo.Name, ElementNameWidth, severity);
                BalanceGui.Cell(string.Join(" + ", combo.RequiredTags), 140f);
                BalanceGui.Cell(combo.IsReachable ? "yes" : "no", 76f, severity, BlockerTooltip(combo));
                BalanceGui.Cell(
                    combo.UnlockedAt != null
                        ? $"{combo.UnlockedAt.RunName} / {combo.UnlockedAt.LevelReference}"
                        : "never",
                    170f, severity);
                BalanceGui.Cell(
                    combo.EnablingMagic.Count > 0 ? string.Join("   ", combo.EnablingMagic) : "—",
                    300f, BalanceSeverity.Ok,
                    combo.EnablingMagic.Count > 0 ? string.Join("\n", combo.EnablingMagic) : null);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string BlockerTooltip(ComboAvailability combo)
        {
            if (combo.TagsWithNoMagic.Count > 0)
            {
                return $"No magic in the catalog carries {string.Join(", ", combo.TagsWithNoMagic)} — "
                     + "the combo cannot fire by construction.";
            }
            if (combo.TagsNotDrawable.Count > 0)
            {
                return $"{string.Join(", ", combo.TagsNotDrawable)} exists only on magic no enemy offers.";
            }
            return null;
        }

        // ---------------------------------------------------------------- helpers

        private static string FormatTypes(Dictionary<DamageType, float> weights)
        {
            if (weights.Count == 0)
            {
                return "—";
            }

            var parts = new List<string>();
            foreach (var kvp in weights)
            {
                parts.Add($"{kvp.Key} ({kvp.Value:0.0})");
            }
            return string.Join(", ", parts);
        }

        private static List<string> MagicNames(List<MagicSO> magic)
        {
            var names = new List<string>();
            foreach (var entry in magic)
            {
                if (entry != null)
                {
                    names.Add(string.IsNullOrEmpty(entry.DisplayName) ? entry.name : entry.DisplayName);
                }
            }
            return names;
        }

        private static List<string> ComboNames(List<MagicComboSO> combos)
        {
            var names = new List<string>();
            foreach (var entry in combos)
            {
                if (entry != null)
                {
                    names.Add(string.IsNullOrEmpty(entry.ComboName) ? entry.name : entry.ComboName);
                }
            }
            return names;
        }
    }
}
