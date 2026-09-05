using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// The Elements &amp; Unlocks tab: the supply side of the elemental layer. Resistances and combos are
    /// authored content that only becomes live if the <b>sphere grids</b> hand the player the pieces, so
    /// this tab puts the two next to each other — what each level resists, and what the modelled party
    /// can actually bring to it by that point in the run order.
    ///
    /// <para>The supply used to be the Draw tables, so the magic matrix was magic × run. Magic is bought
    /// on the grid now (<c>docs/plans/SPECIALIZATION.md</c> §9b), so it is magic × <b>hero</b>: the
    /// question is which hero to invest in and what the route costs, not which level to walk into.</para>
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
                + "reads the sphere grids as a supply chain: which hero teaches what and for how much XP, when "
                + "the modelled party first holds each combo's pieces, and whether a level's resistances are in "
                + "elements it can already deal.",
                BalanceGui.WrapMiniStyle);

            DrawFilterRow("Show only unreachable magic and combos.");

            BeginScroll();

            DrawUnlockTimeline(map);
            EditorGUILayout.Space(8f);
            DrawMagicMatrix(map);
            EditorGUILayout.Space(8f);
            DrawComboReachability(map);
            EditorGUILayout.Space(8f);
            DrawMaterialYield();

            EndScroll();
        }

        // ---------------------------------------------------------------- material yield

        /// <summary>
        /// The raw-material tap, per run and campaign-wide. Magic is the supply chain this tab was
        /// built for; materials are the second one, and they are here rather than on their own tab
        /// because the question is identical - is the thing the player needs actually obtainable, and
        /// by the time they need it.
        ///
        /// <para>Measured before anything spends materials, which is the point: buildings and
        /// material-priced grid nodes (<c>docs/plans/HUB.md</c> §7) are drains, and a drain can only
        /// be priced against a counted source.</para>
        /// </summary>
        private void DrawMaterialYield()
        {
            BalanceGui.SectionHeader(
                "Material yield",
                "Expected units per run at the modelled traversal — kills plus caches. Nothing spends "
                + "these yet; this is the tap, measured before the drains are authored.");

            if (_report == null || _report.Materials.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No material drops are authored. Materials are ItemSOs with Category = Material, "
                    + "listed on EnemySO.LootTable (what a monster is made of) and "
                    + "LevelDefinitionSO.MaterialTable (what the floor is made of, found in caches).",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Material", ElementNameWidth);
            BalanceGui.HeaderCell("Campaign", ElementCellWidth, "Expected units across every run.");
            BalanceGui.HeaderCell("Kills", ElementCellWidth, "Units from enemy drop tables.");
            BalanceGui.HeaderCell("Caches", ElementCellWidth, "Units from the levels' own material tables.");
            foreach (var run in _report.Runs)
            {
                BalanceGui.HeaderCell(run.Name, ElementCellWidth);
            }
            EditorGUILayout.EndHorizontal();

            // Per-run columns, so "which run pays for this material" is readable off one line.
            var perRun = new List<Dictionary<string, float>>();
            foreach (var run in _report.Runs)
            {
                var totals = new Dictionary<string, float>();
                foreach (var yield in MaterialYieldModel.ForRun(run))
                {
                    totals[yield.Key] = yield.Total;
                }
                perRun.Add(totals);
            }

            foreach (var material in _report.Materials)
            {
                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(material.Material, material.Name, ElementNameWidth);
                BalanceGui.Cell($"{material.Total:0.0}", ElementCellWidth,
                    material.Total > 0f ? BalanceSeverity.Ok : BalanceSeverity.Warning);
                BalanceGui.Cell($"{material.FromKills:0.0}", ElementCellWidth);
                BalanceGui.Cell($"{material.FromCaches:0.0}", ElementCellWidth);

                foreach (var totals in perRun)
                {
                    float units = totals.TryGetValue(material.Key, out var value) ? value : 0f;
                    BalanceGui.Cell(units > 0f ? $"{units:0.0}" : "-", ElementCellWidth,
                        BalanceSeverity.Ok,
                        units > 0f ? null : "This run never yields it.");
                }
                EditorGUILayout.EndHorizontal();
            }
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
                BalanceGui.Cell($"{run.NewlyKnown.Count} new magic", MetricWidth + 20f,
                    run.NewlyKnown.Count == 0 ? BalanceSeverity.Info : BalanceSeverity.Ok);
                BalanceGui.Cell($"{run.NewlyEnabledCombos.Count} new combo(s)", MetricWidth + 30f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.Paragraph(
                    run.NewlyKnown.Count > 0
                        ? "learns: " + string.Join(", ", MagicNames(run.NewlyKnown))
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
                    level.NewlyKnown.Count > 0 ? string.Join(", ", MagicNames(level.NewlyKnown)) : "—",
                    200f);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ---------------------------------------------------------------- magic matrix

        private void DrawMagicMatrix(ProgressionMap map)
        {
            BalanceGui.SectionHeader(
                "Magic availability",
                $"Grid coverage {map.ReachableMagicCount}/{map.CatalogMagicCount}. One column per hero: the "
                + "cheapest XP to own a MagicKnown node teaching it, with the node in the tooltip. A grid is "
                + "the only route to magic, so anything unreachable here cannot be cast by anyone.");

            var heroes = HeroColumns(map);

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Magic", ElementNameWidth);
            BalanceGui.HeaderCell("Element", 76f, "Damage types this magic deals.");
            BalanceGui.HeaderCell("Tags", 110f, "Combo tags it applies.");
            BalanceGui.HeaderCell("Cheapest", 150f, "Cheapest hero and total XP to reach a node teaching it.");
            foreach (var hero in heroes)
            {
                BalanceGui.HeaderCell(hero, ElementCellWidth, hero);
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

                // Unreachable is Critical now, not Warning: with Draw gone there is no second route.
                var severity = availability.IsReachable
                    ? (availability.SingleHeroOnly ? BalanceSeverity.Info : BalanceSeverity.Ok)
                    : BalanceSeverity.Critical;

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
                    var first = availability.FirstSource;
                    BalanceGui.Cell(
                        $"{first.HeroName} — {first.PathCost} xp",
                        150f, severity,
                        $"Node '{first.NodeKey}', depth {first.Depth}, {first.Charges} charges. "
                        + $"The node itself costs {first.NodeCost}; {first.PathCost} is the whole chain from "
                        + "the grid's start."
                        + (availability.SingleHeroOnly
                            ? "\nOnly this hero teaches it, so fielding them is a precondition."
                            : ""));
                }
                else
                {
                    BalanceGui.Cell("never", 150f, BalanceSeverity.Critical,
                        "No MagicKnown node on any grid teaches this, so no hero can ever cast it.");
                }

                foreach (var hero in heroes)
                {
                    DrawAvailabilityCell(availability, hero);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>Hero column order: every hero that teaches anything, first-seen order.</summary>
        private static List<string> HeroColumns(ProgressionMap map)
        {
            var heroes = new List<string>();
            foreach (var availability in map.Magic)
            {
                foreach (var source in availability.Sources)
                {
                    if (!heroes.Contains(source.HeroName))
                    {
                        heroes.Add(source.HeroName);
                    }
                }
            }
            return heroes;
        }

        private void DrawAvailabilityCell(MagicAvailability availability, string heroName)
        {
            int cheapest = int.MaxValue;
            var detail = new List<string>();

            foreach (var source in availability.Sources)
            {
                if (source.HeroName != heroName)
                {
                    continue;
                }
                if (source.PathCost < cheapest)
                {
                    cheapest = source.PathCost;
                }
                detail.Add($"'{source.NodeKey}' — depth {source.Depth}, {source.PathCost} xp, "
                    + $"{source.Charges} charges");
            }

            if (detail.Count == 0)
            {
                BalanceGui.Cell("—", ElementCellWidth);
                return;
            }

            BalanceGui.Cell($"{cheapest} xp", ElementCellWidth, BalanceSeverity.Ok,
                string.Join("\n", detail));
        }

        // ---------------------------------------------------------------- combos

        private void DrawComboReachability(ProgressionMap map)
        {
            BalanceGui.SectionHeader(
                "Combo reachability",
                $"{map.ReachableComboCount}/{map.Combos.Count} reachable. A combo needs one required tag already "
                + "on the target and another arriving with the incoming cast, so every required tag has to be "
                + "carried by magic some hero's grid teaches.");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Combo", ElementNameWidth);
            BalanceGui.HeaderCell("Requires", 140f);
            BalanceGui.HeaderCell("Reachable", 76f);
            BalanceGui.HeaderCell("Held from", 170f,
                "First level at which the modelled party owns a magic for every required tag at once.");
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
            if (combo.TagsNotLearnable.Count > 0)
            {
                return $"{string.Join(", ", combo.TagsNotLearnable)} exists only on magic no sphere grid teaches.";
            }
            if (combo.UnlockedAt == null)
            {
                return $"Every piece is on a grid (about {combo.InvestmentToEnable} xp in total), but no "
                     + "modelled party ever owns them all at once. GreedySpend is a breadth build, so read "
                     + "this as under-buying rather than as proof the combo is unreachable.";
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
