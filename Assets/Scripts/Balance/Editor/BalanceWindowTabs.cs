using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// The tab bodies for <see cref="BalanceWindow"/>. Kept in its own partial so the window's chrome
    /// and analysis plumbing stay readable.
    /// </summary>
    public partial class BalanceWindow
    {
        private const float NameWidth = 150f;
        private const float StatWidth = 46f;
        private const float MetricWidth = 68f;
        private const float WideWidth = 96f;

        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        private bool Foldout(string key, string label, bool defaultOpen = false)
        {
            if (!_foldouts.ContainsKey(key))
            {
                _foldouts[key] = defaultOpen;
            }
            _foldouts[key] = EditorGUILayout.Foldout(_foldouts[key], label, true, EditorStyles.foldoutHeader);
            return _foldouts[key];
        }

        private static void Bar(float fraction, float width, Color color, string tooltip = null)
        {
            var rect = GUILayoutUtility.GetRect(width, BalanceGui.RowHeight,
                GUILayout.Width(width), GUILayout.Height(BalanceGui.RowHeight));

            var inner = new Rect(rect.x, rect.y + 3f, rect.width, rect.height - 6f);
            EditorGUI.DrawRect(inner, new Color(0f, 0f, 0f, 0.25f));

            float clamped = Mathf.Clamp01(fraction);
            if (clamped > 0f)
            {
                EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width * clamped, inner.height), color);
            }

            // A tick at 100% so an over-budget bar reads as over-budget, not just full.
            EditorGUI.DrawRect(new Rect(inner.xMax - 1f, inner.y - 1f, 1f, inner.height + 2f),
                new Color(1f, 1f, 1f, 0.5f));

            if (!string.IsNullOrEmpty(tooltip))
            {
                EditorGUI.LabelField(rect, new GUIContent("", tooltip));
            }
        }

        // ================================================================== Party

        private void DrawPartyTab()
        {
            BeginScroll();

            if (_report == null || _report.Party == null || _report.Party.Size == 0)
            {
                EditorGUILayout.HelpBox("No HeroSO assets found.", MessageType.Warning);
                EndScroll();
                return;
            }

            var party = _report.Party;

            BalanceGui.SectionHeader(
                "Reference party",
                "Every other number in this window is measured against this party. Base stats are editable — "
                + "the whole analysis re-runs as you change them.");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Hero", NameWidth);
            BalanceGui.HeaderCell("Spent", StatWidth, "XP the modelled sphere-grid activations cost.");
            BalanceGui.HeaderCell("Nodes", StatWidth, "Activated / authored sphere-grid nodes.");
            // One column per stat, generated: a new StatType appears here without touching this file.
            foreach (var stat in StatCatalog.Types)
            {
                BalanceGui.HeaderCell(StatCatalog.ShortName(stat), StatWidth, stat + " (editable)");
            }
            BalanceGui.HeaderCell("Atk pwr", MetricWidth, "The stat this hero attacks with, after node gains and gear.");
            BalanceGui.HeaderCell("Eff HP", MetricWidth, "After node gains and gear.");
            BalanceGui.HeaderCell("END cut", MetricWidth, "Share of incoming damage the Endurance curve removes.");
            BalanceGui.HeaderCell("Survives", WideWidth, "Fewest ordinary (non-boss) hits that would kill this hero.");
            BalanceGui.HeaderCell("Worst hit", WideWidth, "Biggest average non-boss hit, and from whom.");
            EditorGUILayout.EndHorizontal();

            foreach (var hero in party.Heroes)
            {
                var serialized = Serialized(hero.Definition);
                bool changed = false;

                int hitsToKill;
                float worstHit;
                string worstEnemy;
                WorstCaseAgainst(hero, out hitsToKill, out worstHit, out worstEnemy);

                var durability = hitsToKill <= 1
                    ? BalanceSeverity.Critical
                    : hitsToKill < _rules.MinHitsToKillHero
                        ? BalanceSeverity.Warning
                        : BalanceSeverity.Ok;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(hero.Definition, hero.Name, NameWidth);
                BalanceGui.Cell(hero.SpentXp.ToString(), StatWidth);
                BalanceGui.Cell($"{hero.NodesActivated}/{hero.NodesTotal}", StatWidth,
                    hero.NodesTotal < _rules.MinGridNodes ? BalanceSeverity.Warning : BalanceSeverity.Ok,
                    hero.NodesTotal < _rules.MinGridNodes
                        ? "A grid this small means XP stops mattering almost immediately."
                        : null);

                foreach (var stat in StatCatalog.Types)
                {
                    changed |= BalanceGui.EditableStatCell(serialized, "BaseStats", stat, StatWidth,
                        stat == StatType.MaxHealth ? durability : BalanceSeverity.Ok,
                        stat == StatType.MaxHealth ? "The root cause of one-shot fights lives here." : null);
                }

                BalanceGui.Cell(hero.Unit != null ? hero.Unit.EffectiveAttackPower.ToString() : "—", MetricWidth);
                BalanceGui.Cell(hero.Effective[StatType.MaxHealth].ToString(), MetricWidth, durability);
                BalanceGui.Cell($"{hero.EnduranceReduction:P0}", MetricWidth);
                BalanceGui.Cell(
                    hitsToKill == int.MaxValue ? "—" : $"{hitsToKill} hit{(hitsToKill == 1 ? "" : "s")}",
                    WideWidth, durability,
                    $"Target is at least {_rules.MinHitsToKillHero} hits.");
                BalanceGui.Cell(worstHit > 0f ? $"{worstHit:0.0} ({worstEnemy})" : "—", WideWidth, BalanceSeverity.Ok,
                    "Average damage including the expected crit contribution.");
                EditorGUILayout.EndHorizontal();

                Commit(serialized, changed);
            }

            EditorGUILayout.Space(6f);
            DrawHealingSection(party);
            EditorGUILayout.Space(6f);
            DrawSphereGridSection(party);

            EndScroll();
        }

        private void WorstCaseAgainst(HeroBaseline hero, out int hitsToKill, out float worstHit, out string worstEnemy)
        {
            hitsToKill = int.MaxValue;
            worstHit = 0f;
            worstEnemy = "";

            foreach (var metrics in _report.Enemies)
            {
                if (metrics.IsBoss)
                {
                    continue;
                }

                foreach (var record in metrics.PerHero)
                {
                    if (record.HeroName != hero.Name)
                    {
                        continue;
                    }
                    if (record.HitsToKill < hitsToKill)
                    {
                        hitsToKill = record.HitsToKill;
                    }
                    if (record.DamagePerHit > worstHit)
                    {
                        worstHit = record.DamagePerHit;
                        worstEnemy = metrics.Name;
                    }
                }
            }
        }

        private void DrawHealingSection(PartyBaseline party)
        {
            BalanceGui.SectionHeader(
                "Healing",
                "HP only refills between levels, so potions are the party's whole in-level recovery. A heal "
                + "that tops a hero straight off removes the decision from using it.");

            if (party.PotionItem == null)
            {
                EditorGUILayout.HelpBox(
                    "No RestoreHealth consumable found, so the in-level healing pool is assumed to be zero.",
                    MessageType.Info);
                return;
            }

            var serialized = Serialized(party.PotionItem);
            bool changed = false;

            EditorGUILayout.BeginHorizontal();
            BalanceGui.AssetCell(party.PotionItem, party.PotionItem.DisplayName, NameWidth);
            BalanceGui.HeaderCell("Restores", MetricWidth);
            changed |= BalanceGui.EditableCell(serialized, "ConsumableAmount", StatWidth);
            BalanceGui.Cell($"x{party.PotionCount} carried", WideWidth, BalanceSeverity.Ok,
                "Belt capacity from the save's resource maximums (or the default).");
            BalanceGui.Cell($"pool {party.HealingPool} HP", WideWidth);
            EditorGUILayout.EndHorizontal();

            foreach (var hero in party.Heroes)
            {
                if (hero.Effective[StatType.MaxHealth] <= 0)
                {
                    continue;
                }

                float fraction = (float)party.PotionItem.ConsumableAmount / hero.Effective[StatType.MaxHealth];
                var severity = fraction >= 1f
                    ? BalanceSeverity.Warning
                    : fraction >= _rules.MaxSingleHealFraction
                        ? BalanceSeverity.Info
                        : BalanceSeverity.Ok;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.Cell($"on {hero.Name}", NameWidth - 16f);
                BalanceGui.Cell($"{fraction:P0} of bar", MetricWidth + StatWidth, severity,
                    $"Target ceiling is {_rules.MaxSingleHealFraction:P0} of a hero's health bar.");
                EditorGUILayout.EndHorizontal();
            }

            Commit(serialized, changed);
        }

        /// <summary>
        /// The sphere grid's *numbers*, per hero — cost and gains per node, with the same
        /// half-of-base severity tint the old level curve had. Graph shape (positions, edges, adding
        /// and deleting nodes) is authored in Tools ▸ Heroes ▸ Sphere Grid Editor; this table exists
        /// so a balance pass can retune costs and gains next to the findings they cause.
        /// </summary>
        private void DrawSphereGridSection(PartyBaseline party)
        {
            BalanceGui.SectionHeader(
                "Sphere grids",
                "XP banked per hero (kills split evenly — XpSplit) is spent on nodes at the hub. "
                + "Costs and stat gains are editable here; the graph itself is edited in "
                + "Tools ▸ Heroes ▸ Sphere Grid Editor.");

            foreach (var hero in party.Heroes)
            {
                if (hero.Definition == null)
                {
                    continue;
                }

                var grid = hero.Definition.SphereGrid;
                if (grid == null || grid.Nodes == null || grid.Nodes.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"{hero.Name} has no sphere grid — banked XP can never be spent.",
                        MessageType.Warning);
                    continue;
                }

                int totalCost = SphereGridOps.TotalGridCost(grid);
                if (!Foldout($"grid:{grid.GetHashCode()}",
                        $"{hero.Name} — {grid.Nodes.Count} node(s), {totalCost} XP to complete"))
                {
                    continue;
                }

                var serialized = Serialized(grid);
                var nodes = serialized.FindProperty("Nodes");
                bool changed = false;

                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.HeaderCell("Node", NameWidth, "Key (save data — rename in the grid editor only).");
                BalanceGui.HeaderCell("Kind", MetricWidth);
                BalanceGui.HeaderCell("XP", StatWidth, "XpCost (editable)");
                foreach (var stat in StatCatalog.Types)
                {
                    BalanceGui.HeaderCell("+" + StatCatalog.ShortName(stat), StatWidth,
                        "Gains[" + stat + "] (editable; Stat nodes only)");
                }
                BalanceGui.HeaderCell("Grant", WideWidth, "What a Resistance or MagicSlot node grants.");
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < nodes.arraySize && i < grid.Nodes.Count; i++)
                {
                    var entry = nodes.GetArrayElementAtIndex(i);
                    var node = grid.Nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16f);
                    BalanceGui.Cell(
                        node.Key == SphereGridOps.StartKey(grid) ? node.Key + " ★" : node.Key,
                        NameWidth);
                    BalanceGui.Cell(node.Kind.ToString(), MetricWidth);
                    changed |= BalanceGui.EditableCell(entry.FindPropertyRelative("XpCost"), StatWidth);

                    // One editable gain per stat, read out of the node's Gains block.
                    foreach (var stat in StatCatalog.Types)
                    {
                        // A single node that reshapes a stat by half its base is almost certainly
                        // a slip - most visibly on Agility, which doubles the hero's turn rate.
                        int baseValue = hero.Definition.BaseStats[stat];
                        int gain = BalanceGui.StatEntryValue(entry, "Gains", stat);
                        var severity = baseValue > 0 && gain >= baseValue * 0.5f
                            ? BalanceSeverity.Warning
                            : BalanceSeverity.Ok;

                        changed |= BalanceGui.EditableStatCell(entry, "Gains", stat, StatWidth, severity,
                            severity == BalanceSeverity.Warning
                                ? "A gain of half the base stat or more reshapes this hero in one node."
                                : null);
                    }

                    string grant = node.Kind == SphereNodeKind.Resistance
                        ? $"{node.ResistType} resist {node.ResistPercent:0}%"
                        : node.Kind == SphereNodeKind.MagicSlot
                            ? "+1 magic slot"
                            : "";
                    BalanceGui.Cell(grant, WideWidth);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

                Commit(serialized, changed);
            }
        }

        // ================================================================== Enemies

        private void DrawEnemiesTab()
        {
            DrawFilterRow("Show only enemies with a finding against them.");

            BalanceGui.Paragraph(
                "Danger index = ticks for the party to win ÷ ticks for the party to die. Under 1 the party wins "
                + "with margin; at 1 the fight is a coin flip on turn order.",
                BalanceGui.WrapMiniStyle);

            BeginScroll();

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Enemy", NameWidth);
            BalanceGui.HeaderCell("Boss", 34f);
            BalanceGui.HeaderCell("Archetype", WideWidth);
            foreach (var stat in StatCatalog.Types)
            {
                BalanceGui.HeaderCell(StatCatalog.ShortName(stat), StatWidth, stat + " (editable)");
            }
            BalanceGui.HeaderCell("XP", StatWidth);
            BalanceGui.HeaderCell("Gold", StatWidth);
            BalanceGui.HeaderCell("Dmg/hit", MetricWidth, "Average damage on a party member, crit included.");
            BalanceGui.HeaderCell("Dmg/turn", MetricWidth, "Per-turn output including charges, heavies and AoE cadence.");
            BalanceGui.HeaderCell("Kills in", MetricWidth, "Fewest hits it needs to drop any one hero.");
            BalanceGui.HeaderCell("Turns to kill", MetricWidth, "Party turns needed to kill it, basic attacks only.");
            BalanceGui.HeaderCell("Danger", MetricWidth);
            BalanceGui.HeaderCell("Acts", MetricWidth, "Turn rate relative to the party average.");
            BalanceGui.HeaderCell("XP/danger", MetricWidth, "Reward paid per unit of risk.");
            BalanceGui.HeaderCell("Resist", MetricWidth, "Number of resistance entries.");
            EditorGUILayout.EndHorizontal();

            foreach (var metrics in _report.Enemies)
            {
                if (!MatchesFilter(metrics.Name) && !MatchesFilter(metrics.Definition.name))
                {
                    continue;
                }

                var worst = WorstSeverityFor(metrics.Definition);
                if (_problemsOnly && worst == BalanceSeverity.Ok)
                {
                    continue;
                }

                var serialized = Serialized(metrics.Definition);
                bool changed = false;

                float ceiling = metrics.DangerCeiling(_rules);
                var dangerSeverity = metrics.SoloDangerIndex >= 1f
                    ? BalanceSeverity.Critical
                    : metrics.SoloDangerIndex > ceiling
                        ? BalanceSeverity.Warning
                        : metrics.SoloDangerIndex < _rules.MinMeaningfulDanger
                            ? BalanceSeverity.Warning
                            : BalanceSeverity.Ok;

                var killsSeverity = metrics.FewestHitsToKillAHero <= 1
                    ? BalanceSeverity.Critical
                    : metrics.FewestHitsToKillAHero < _rules.MinHitsToKillHero
                        ? BalanceSeverity.Warning
                        : BalanceSeverity.Ok;

                float ttkCeiling = metrics.TimeToKillCeiling(_rules);
                var ttkSeverity = metrics.PartyTurnsToKill > ttkCeiling
                    ? BalanceSeverity.Warning
                    : metrics.PartyTurnsToKill < _rules.MinEnemyTimeToKill
                        ? BalanceSeverity.Info
                        : BalanceSeverity.Ok;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(metrics.Definition, metrics.Name, NameWidth, worst);
                changed |= BalanceGui.EditableCell(serialized, "IsBoss", 34f);
                changed |= BalanceGui.EditableCell(serialized, "Archetype", WideWidth);
                // One editable column per stat. Only three carry a warning colour, and which three is
                // the interesting part: Strength drives one-shots, MaxHealth drives time-to-kill, and
                // Agility drives how often it acts.
                foreach (var stat in StatCatalog.Types)
                {
                    var statSeverity = BalanceSeverity.Ok;
                    if (stat == StatType.Strength)
                    {
                        statSeverity = killsSeverity;
                    }
                    else if (stat == StatType.MaxHealth)
                    {
                        statSeverity = ttkSeverity;
                    }
                    else if (stat == StatType.Agility && metrics.ActionShareVsParty >= 1.5f)
                    {
                        statSeverity = BalanceSeverity.Info;
                    }

                    changed |= BalanceGui.EditableStatCell(serialized, "BaseStats", stat, StatWidth, statSeverity);
                }

                changed |= BalanceGui.EditableCell(serialized, "XpReward", StatWidth,
                    metrics.Definition.XpReward <= 0 ? BalanceSeverity.Info : BalanceSeverity.Ok);
                changed |= BalanceGui.EditableCell(serialized, "GoldReward", StatWidth);

                BalanceGui.Cell(BalanceGui.Number(metrics.AverageDamagePerHit, "0.0"), MetricWidth);
                BalanceGui.Cell(BalanceGui.Number(metrics.EffectiveDamagePerTurn, "0.0"), MetricWidth,
                    BalanceSeverity.Ok,
                    $"Archetype cadence multiplier {metrics.OffenseMultiplier:0.00}x on a plain hit.");
                BalanceGui.Cell(BalanceGui.Count(metrics.FewestHitsToKillAHero)
                    + (string.IsNullOrEmpty(metrics.FastestKillTarget) ? "" : $" ({metrics.FastestKillTarget})"),
                    MetricWidth, killsSeverity);
                BalanceGui.Cell(BalanceGui.Number(metrics.PartyTurnsToKill, "0.0"), MetricWidth, ttkSeverity,
                    $"Band is {_rules.MinEnemyTimeToKill:0.0}–{ttkCeiling:0.0} turns.");
                BalanceGui.Cell(BalanceGui.Number(metrics.SoloDangerIndex), MetricWidth, dangerSeverity,
                    $"Band ceiling for this tier is {ceiling:0.00}.");
                BalanceGui.Cell($"{metrics.ActionShareVsParty:0.0}x", MetricWidth);
                BalanceGui.Cell(BalanceGui.Number(metrics.XpPerDanger, "0"), MetricWidth);
                BalanceGui.Cell(metrics.ResistanceCount.ToString(), MetricWidth,
                    metrics.ResistanceCount == 0 ? BalanceSeverity.Info : BalanceSeverity.Ok,
                    "With no resistances anywhere, every damage type is arithmetically identical.");
                EditorGUILayout.EndHorizontal();

                Commit(serialized, changed);
            }

            EndScroll();
        }

        private BalanceSeverity WorstSeverityFor(Object asset)
        {
            var worst = BalanceSeverity.Ok;
            if (_report == null || asset == null)
            {
                return worst;
            }

            foreach (var issue in _report.Issues)
            {
                if (issue.Asset == asset && issue.Severity > worst)
                {
                    worst = issue.Severity;
                }
            }
            return worst;
        }

        // ================================================================== Levels & Runs

        private void DrawLevelsTab()
        {
            if (_report == null || _report.Runs.Count == 0)
            {
                EditorGUILayout.HelpBox("No RunDefinitionSO assets found.", MessageType.Warning);
                return;
            }

            BalanceGui.Paragraph(
                "Attrition load is the level's expected HP cost as a share of the party's HP + potion pool. "
                + "Health only refills between levels, so anything at or above 1.00 cannot be cleared.",
                BalanceGui.WrapMiniStyle);

            BeginScroll();

            foreach (var run in _report.Runs)
            {
                bool focused = _focusRun != null && run.Run == _focusRun;
                string key = $"run:{(run.Run != null ? run.Run.GetHashCode() : 0)}";

                if (!Foldout(key, $"{run.Name} — {run.Levels.Count} level(s)", focused || _report.Runs.Count == 1))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                BalanceGui.HeaderCell("#", 24f);
                BalanceGui.HeaderCell("Level", NameWidth);
                BalanceGui.HeaderCell("Layout", 74f);
                BalanceGui.HeaderCell("Rooms", 60f, "Rooms to generate, editable on the level template.");
                BalanceGui.HeaderCell("Combat", MetricWidth, "Expected number of rooms containing enemies.");
                BalanceGui.HeaderCell("Enemies", MetricWidth, "Expected total enemies across the level.");
                BalanceGui.HeaderCell("Events", MetricWidth, "Expected rooms offering an Action, and what "
                    + "engaging with them costs. Scaled by EventEngagementRate.");
                BalanceGui.HeaderCell("HP cost", MetricWidth, "Expected party HP spent on the level — "
                    + "fights plus events.");
                BalanceGui.HeaderCell("Attrition", 120f, "Share of the party's HP + healing pool consumed.");
                BalanceGui.HeaderCell("Peak", MetricWidth, "Danger of the level's hardest expected room.");
                BalanceGui.HeaderCell("Worst roll", MetricWidth, "Danger when every spawn roll lands.");
                BalanceGui.HeaderCell("Boss", MetricWidth);
                BalanceGui.HeaderCell("vs trash", MetricWidth, "Boss danger relative to the level's average room.");
                BalanceGui.HeaderCell("XP", MetricWidth);
                BalanceGui.HeaderCell("Gold", MetricWidth);
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < run.Levels.Count; i++)
                {
                    DrawLevelRow(run, run.Levels[i], i);
                }

                EditorGUILayout.Space(4f);
                DrawDifficultyCurve(run);

                EditorGUILayout.Space(4f);
                foreach (var level in run.Levels)
                {
                    DrawLevelRooms(run, level);
                }

                EditorGUILayout.Space(8f);
            }

            EndScroll();
        }

        private void DrawLevelRow(RunCurve run, LevelCurve level, int index)
        {
            var attritionSeverity = level.AttritionMargin < 0f
                ? BalanceSeverity.Critical
                : level.AttritionMargin < _rules.MinAttritionMargin
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Ok;

            var worstRollSeverity = level.PeakWorstCaseDanger >= 1f
                ? BalanceSeverity.Warning
                : BalanceSeverity.Ok;

            var bossSeverity = BalanceSeverity.Ok;
            if (level.Boss != null && level.BossToTrashRatio > 0f)
            {
                if (level.BossToTrashRatio > _rules.MaxBossToTrashRatio)
                {
                    bossSeverity = BalanceSeverity.Critical;
                }
                else if (level.BossToTrashRatio < _rules.MinBossToTrashRatio)
                {
                    bossSeverity = BalanceSeverity.Warning;
                }
            }

            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell((index + 1).ToString(), 24f);
            BalanceGui.AssetCell(level.Template != null ? (Object)level.Template : run.Run, level.Name, NameWidth);
            BalanceGui.Cell(level.LayoutKind, 74f, BalanceSeverity.Ok,
                level.Layout != null ? "Hand-placed rooms." : "Rooms drawn uniformly from the template's pool.");

            if (level.Template != null && level.Layout == null)
            {
                var serialized = Serialized(level.Template);
                bool changed = BalanceGui.EditableCell(serialized, "RoomsToGenerate", 60f,
                    level.AttritionMargin < 0f ? BalanceSeverity.Critical : BalanceSeverity.Ok);
                Commit(serialized, changed);
            }
            else
            {
                BalanceGui.Cell(level.Layout != null && level.Layout.Rooms != null
                    ? level.Layout.Rooms.Count.ToString()
                    : "—", 60f);
            }

            BalanceGui.Cell($"{level.ExpectedCombatRooms:0.0}", MetricWidth);
            BalanceGui.Cell($"{level.ExpectedEnemyCount:0.0}", MetricWidth);

            var eventSeverity = level.EventAttritionShare > _rules.MaxEventAttritionShare
                ? BalanceSeverity.Warning
                : BalanceSeverity.Ok;
            BalanceGui.Cell(level.ExpectedEventRooms > 0f ? $"{level.ExpectedEventRooms:0.0}" : "—",
                MetricWidth, eventSeverity,
                level.ExpectedEventRooms > 0f
                    ? $"{level.ExpectedEventHealthCost:0} HP ({level.EventAttritionShare:P0} of the level's "
                      + $"cost) and {level.ExpectedEventGold:0} gold, from {level.ExpectedEventRooms:0.0} "
                      + $"expected event(s). {level.ExpectedAfflictions:0.0} level affliction(s), which the "
                      + "curve counts but cannot price."
                    : "No room in this level's pool offers an event.");

            BalanceGui.Cell($"{level.ExpectedHealthCost:0}", MetricWidth, attritionSeverity,
                $"{level.ExpectedCombatHealthCost:0} HP from fights + {level.ExpectedEventHealthCost:0} HP "
                + "from room events.");

            EditorGUILayout.BeginVertical(GUILayout.Width(120f));
            EditorGUILayout.BeginHorizontal();
            Bar(level.AttritionLoad, 60f, BalanceGui.TextColorFor(attritionSeverity),
                $"{level.AttritionLoad:P0} of the party's {_report.Party.SustainPool} HP + healing pool.");
            BalanceGui.Cell($"{level.AttritionLoad:0.00}x", 56f, attritionSeverity);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            BalanceGui.Cell(BalanceGui.Number(level.PeakRoomDanger), MetricWidth,
                level.PeakRoomDanger >= 1f ? BalanceSeverity.Critical : BalanceSeverity.Ok);
            BalanceGui.Cell(BalanceGui.Number(level.PeakWorstCaseDanger), MetricWidth, worstRollSeverity);

            if (level.Boss != null)
            {
                BalanceGui.AssetCell(level.Boss, BalanceGui.Number(level.BossDanger), MetricWidth, bossSeverity);
                BalanceGui.Cell($"{level.BossToTrashRatio:0.0}x", MetricWidth, bossSeverity,
                    $"Band is {_rules.MinBossToTrashRatio:0.0}x–{_rules.MaxBossToTrashRatio:0.0}x.");
            }
            else
            {
                BalanceGui.Cell("—", MetricWidth);
                BalanceGui.Cell("—", MetricWidth);
            }

            BalanceGui.Cell($"{level.ExpectedXp:0}", MetricWidth);
            BalanceGui.Cell($"{level.ExpectedGold:0}", MetricWidth);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDifficultyCurve(RunCurve run)
        {
            GUILayout.Label("Difficulty curve (attrition load per level)", EditorStyles.miniBoldLabel);

            float peak = 0.01f;
            foreach (var level in run.Levels)
            {
                peak = Mathf.Max(peak, level.AttritionLoad);
            }

            var rect = GUILayoutUtility.GetRect(0f, 70f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));

            if (run.Levels.Count == 0)
            {
                return;
            }

            // The 1.0 line is the survivable ceiling: bars crossing it are unclearable levels.
            float unity = Mathf.Clamp01(1f / peak);
            float unityY = rect.yMax - rect.height * unity;
            EditorGUI.DrawRect(new Rect(rect.x, unityY, rect.width, 1f), new Color(1f, 0.4f, 0.4f, 0.7f));

            float slot = rect.width / run.Levels.Count;
            for (int i = 0; i < run.Levels.Count; i++)
            {
                var level = run.Levels[i];
                float normalized = Mathf.Clamp01(level.AttritionLoad / peak);
                float height = rect.height * normalized;

                var severity = level.AttritionMargin < 0f
                    ? BalanceSeverity.Critical
                    : level.AttritionMargin < _rules.MinAttritionMargin
                        ? BalanceSeverity.Warning
                        : BalanceSeverity.Ok;

                var bar = new Rect(rect.x + slot * i + slot * 0.2f, rect.yMax - height, slot * 0.6f, height);
                EditorGUI.DrawRect(bar, BalanceGui.TextColorFor(severity));
                EditorGUI.LabelField(bar, new GUIContent("",
                    $"{level.Name}: attrition {level.AttritionLoad:0.00}x, {level.ExpectedCombatRooms:0.0} combat rooms"));
            }

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < run.Levels.Count; i++)
            {
                GUILayout.Label(run.Levels[i].Name, EditorStyles.miniLabel, GUILayout.Width(slot));
            }
            EditorGUILayout.EndHorizontal();

            if (run.DifficultyJumps.Count > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < run.DifficultyJumps.Count; i++)
                {
                    parts.Add($"{run.Levels[i].Name}→{run.Levels[i + 1].Name} {run.DifficultyJumps[i]:P0}");
                }
                BalanceGui.Paragraph("Level-to-level growth: " + string.Join("   ", parts), BalanceGui.WrapMiniStyle);
            }
        }

        private void DrawLevelRooms(RunCurve run, LevelCurve level)
        {
            string key = $"level:{(run.Run != null ? run.Run.GetHashCode() : 0)}:{level.Index}";
            if (!Foldout(key, $"    {level.Name} — rooms and spawn tables"))
            {
                return;
            }

            EditorGUI.indentLevel++;

            foreach (var room in level.Rooms)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                BalanceGui.AssetCell(room.Room, room.RoomName, NameWidth);
                BalanceGui.Cell($"x{room.Occurrences:0.0}", 56f, BalanceSeverity.Ok,
                    "Expected appearances of this room in the level.");
                BalanceGui.Cell(room.IsCombatRoom ? $"{room.Expected.TotalCount:0.0} enemies" : "no combat", WideWidth);
                BalanceGui.Cell(BalanceGui.Number(room.ExpectedDanger), MetricWidth,
                    room.ExpectedDanger >= 1f ? BalanceSeverity.Critical : BalanceSeverity.Ok, "Expected danger.");
                BalanceGui.Cell(BalanceGui.Number(room.WorstCaseDanger), MetricWidth,
                    room.WorstCaseDanger >= 1f ? BalanceSeverity.Warning : BalanceSeverity.Ok,
                    "Danger when every spawn roll lands.");
                BalanceGui.Cell($"{room.ExpectedHealthCost:0} HP", MetricWidth);
                if (room.GuaranteedSpawns)
                {
                    BalanceGui.Cell("guaranteed", MetricWidth, BalanceSeverity.Info,
                        "GuaranteeAllSpawns skips the roll, so the worst case is the only case.");
                }
                EditorGUILayout.EndHorizontal();

                DrawSpawnTable(room);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSpawnTable(RoomEncounter room)
        {
            if (room.Room == null || room.Room.EnemySpawnTable == null || room.Room.EnemySpawnTable.Count == 0)
            {
                return;
            }

            if (room.UsesSpawnOverride)
            {
                // The numbers in play live on the layout entry, not on the shared room template, so
                // offering the template's table here would edit the wrong thing.
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                BalanceGui.Cell("spawn table overridden by the manual layout — edit it there", 420f,
                    BalanceSeverity.Info,
                    "This room's ManualRoomEntry has an EnemySpawnOverride, which replaces the RoomSO table.");
                EditorGUILayout.EndHorizontal();
                return;
            }

            var serialized = Serialized(room.Room);
            var table = serialized.FindProperty("EnemySpawnTable");
            bool changed = false;

            for (int i = 0; i < table.arraySize; i++)
            {
                var entry = table.GetArrayElementAtIndex(i);
                var enemyProperty = entry.FindPropertyRelative("Enemy");
                var enemy = enemyProperty.objectReferenceValue as EnemySO;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                BalanceGui.Cell(enemy != null
                    ? (string.IsNullOrEmpty(enemy.DisplayName) ? enemy.name : enemy.DisplayName)
                    : "(empty)", NameWidth);
                BalanceGui.HeaderCell("chance", 50f);
                changed |= BalanceGui.EditableCell(entry.FindPropertyRelative("SpawnChance"), 60f);
                BalanceGui.HeaderCell("rolls", 40f);
                changed |= BalanceGui.EditableCell(entry.FindPropertyRelative("EvaluationCount"), 40f);

                float expected = entry.FindPropertyRelative("SpawnChance").floatValue
                               * entry.FindPropertyRelative("EvaluationCount").intValue;
                BalanceGui.Cell($"= {expected:0.00} expected", WideWidth + 20f, BalanceSeverity.Ok,
                    "SpawnChance x EvaluationCount — the expected number of this enemy per room.");
                EditorGUILayout.EndHorizontal();
            }

            Commit(serialized, changed);
        }

        // ================================================================== Variety

        private void DrawVarietyTab()
        {
            var variety = _report != null ? _report.Variety : null;
            if (variety == null)
            {
                EditorGUILayout.HelpBox("Nothing to measure.", MessageType.Info);
                return;
            }

            BeginScroll();

            BalanceGui.SectionHeader(
                "Behaviour mix",
                "Stat balance can be perfect and every fight still ask the same question. This is the axis "
                + "that catches that.");

            foreach (var share in variety.Archetypes)
            {
                var severity = share.Share > _rules.MaxArchetypeShare
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Ok;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.Cell(share.Archetype.ToString(), NameWidth, severity);
                Bar(share.Share, 200f, BalanceGui.TextColorFor(severity));
                BalanceGui.Cell($"{share.Share:P0}", MetricWidth, severity,
                    $"Ceiling for a single archetype is {_rules.MaxArchetypeShare:P0}.");
                BalanceGui.Cell($"{share.Weight:0.#} enemies", WideWidth);
                EditorGUILayout.EndHorizontal();
            }

            BalanceGui.SectionHeader(
                "Elemental relevance",
                "DamageCalculator applies resistance before defense. With no resistances anywhere, every "
                + "damage type is arithmetically identical and the whole elemental layer is decoration.");

            var coverageSeverity = variety.ResistanceCoverage <= 0f
                ? BalanceSeverity.Critical
                : variety.ResistanceCoverage < _rules.MinResistanceCoverage
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Ok;

            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell("Enemies with a resistance", 200f);
            Bar(variety.ResistanceCoverage, 200f, BalanceGui.TextColorFor(coverageSeverity));
            BalanceGui.Cell($"{variety.ResistanceCoverage:P0}", MetricWidth, coverageSeverity,
                $"Target floor is {_rules.MinResistanceCoverage:P0}.");
            EditorGUILayout.EndHorizontal();

            if (variety.InertDamageTypes.Count > 0)
            {
                BalanceGui.Paragraph(
                    $"Inert (dealt by magic, resisted by nobody): {string.Join(", ", variety.InertDamageTypes)}");
            }
            if (variety.UnusedDamageTypes.Count > 0)
            {
                BalanceGui.Paragraph(
                    $"Unused by any magic: {string.Join(", ", variety.UnusedDamageTypes)}");
            }

            BalanceGui.SectionHeader(
                "Draw variety",
                "Draw is the party's only route to new magic, so identical offerings collapse the reason to "
                + "fight one enemy over another.");

            var coverage = variety.DrawCoverage < 0.5f ? BalanceSeverity.Warning : BalanceSeverity.Ok;
            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell("Catalog reachable by Draw", 200f);
            Bar(variety.DrawCoverage, 200f, BalanceGui.TextColorFor(coverage));
            BalanceGui.Cell($"{variety.DistinctDrawableMagic}/{variety.CatalogMagicCount}", MetricWidth, coverage);
            EditorGUILayout.EndHorizontal();

            if (variety.EnemiesWithoutDrawList > 0)
            {
                BalanceGui.Paragraph(
                    $"{variety.EnemiesWithoutDrawList} enemy definition(s) offer no Draw at all.",
                    BalanceGui.WrapMiniStyle);
            }

            foreach (var overlap in variety.DrawOverlaps)
            {
                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(overlap.A, overlap.A.name, NameWidth, BalanceSeverity.Warning);
                BalanceGui.AssetCell(overlap.B, overlap.B.name, NameWidth, BalanceSeverity.Warning);
                BalanceGui.Cell($"{overlap.Share:P0} shared", MetricWidth + 20f, BalanceSeverity.Warning);
                BalanceGui.Cell(string.Join(", ", overlap.SharedMagic), 260f);
                EditorGUILayout.EndHorizontal();
            }

            if (variety.DuplicateLootPairs.Count > 0)
            {
                BalanceGui.SectionHeader("Loot overlap");
                foreach (var duplicate in variety.DuplicateLootPairs)
                {
                    BalanceGui.Paragraph(duplicate, BalanceGui.WrapMiniStyle);
                }
            }

            EndScroll();
        }

        // ================================================================== Simulation

        private void DrawSimulationTab()
        {
            BalanceGui.Paragraph(
                "Headless battles using the real TurnManager, DamageCalculator, buff tracker, effect resolver "
                + "and enemy behaviours. The depth gap is what attack-spam leaves on the table: near zero means "
                + "the fight plays itself.");

            if (!_runSimulation)
            {
                EditorGUILayout.HelpBox(
                    "Simulation is off — it runs hundreds of battles per encounter, which is too slow to re-run "
                    + "on every keystroke while tuning numbers.",
                    MessageType.Info);

                if (GUILayout.Button("Run simulation", GUILayout.Width(140f)))
                {
                    SetSimulation(true);
                    Analyze();
                }
                return;
            }

            if (_report == null || _report.Simulations.Count == 0)
            {
                EditorGUILayout.HelpBox("No encounters to simulate.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{_rules.SimulationTrials} trials per policy, seed {_rules.SimulationSeed}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Re-run", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                Analyze();
            }
            EditorGUILayout.EndHorizontal();

            BeginScroll();

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Encounter", 260f);
            BalanceGui.HeaderCell("Best win%", MetricWidth);
            BalanceGui.HeaderCell("Policy", WideWidth, "The policy that produced the best score.");
            BalanceGui.HeaderCell("Turns", MetricWidth);
            BalanceGui.HeaderCell("End HP", MetricWidth, "Party health remaining on a win.");
            BalanceGui.HeaderCell("Deaths", MetricWidth, "Average hero deaths per attempt.");
            BalanceGui.HeaderCell("Potions", MetricWidth);
            BalanceGui.HeaderCell("Attack-only", MetricWidth, "Score with basic attacks only.");
            BalanceGui.HeaderCell("Best score", MetricWidth);
            BalanceGui.HeaderCell("Depth gap", MetricWidth, "Best minus attack-only. Near zero = no decisions.");
            EditorGUILayout.EndHorizontal();

            foreach (var simulation in _report.Simulations)
            {
                var best = simulation.Best;
                if (best == null)
                {
                    continue;
                }

                if (!MatchesFilter(simulation.Label))
                {
                    continue;
                }

                var winSeverity = best.WinRate <= 0f
                    ? BalanceSeverity.Critical
                    : best.WinRate < _rules.MinEncounterWinRate
                        ? BalanceSeverity.Warning
                        : best.WinRate >= 1f && best.AverageEndHealthFraction >= _rules.TrivialEndHealthFraction
                            ? BalanceSeverity.Info
                            : BalanceSeverity.Ok;

                var depthSeverity = simulation.DepthGap <= _rules.DominantStrategyTolerance && best.WinRate > 0f
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Ok;

                var attackOnly = simulation.AttackOnly;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(simulation.Asset, simulation.Label, 260f, winSeverity);
                BalanceGui.Cell($"{best.WinRate:P0}", MetricWidth, winSeverity,
                    $"{best.Wins}/{best.Trials} wins; floor is {_rules.MinEncounterWinRate:P0}.");
                BalanceGui.Cell(best.Policy.ToString(), WideWidth);
                BalanceGui.Cell($"{best.AverageTurns:0.0}", MetricWidth);
                BalanceGui.Cell($"{best.AverageEndHealthFraction:P0}", MetricWidth,
                    best.WinRate >= 1f && best.AverageEndHealthFraction >= _rules.TrivialEndHealthFraction
                        ? BalanceSeverity.Info
                        : BalanceSeverity.Ok);
                BalanceGui.Cell($"{best.AverageHeroDeaths:0.0}", MetricWidth,
                    best.AverageHeroDeaths >= 1f ? BalanceSeverity.Warning : BalanceSeverity.Ok);
                BalanceGui.Cell($"{best.AveragePotionsUsed:0.0}", MetricWidth);
                BalanceGui.Cell(attackOnly != null ? $"{attackOnly.Score:0.000}" : "—", MetricWidth);
                BalanceGui.Cell($"{best.Score:0.000}", MetricWidth);
                BalanceGui.Cell($"{simulation.DepthGap:0.000}", MetricWidth, depthSeverity,
                    $"Tolerance is {_rules.DominantStrategyTolerance:0.000}.");
                EditorGUILayout.EndHorizontal();

                if (simulation.HasStalemates())
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20f);
                    BalanceGui.Cell($"{best.Stalemates} of {best.Trials} battles hit the {_rules.MaxSimTurns}-turn cap",
                        420f, BalanceSeverity.Warning,
                        "Neither side can finish the other: usually a defense value at or above the attacker's damage.");
                    EditorGUILayout.EndHorizontal();
                }
            }

            EndScroll();
        }

        // ================================================================== Save Audit

        private void DrawSaveTab()
        {
            var save = _report != null ? _report.Save : null;

            if (!_includeSaveAudit)
            {
                EditorGUILayout.HelpBox("Save reading is off. Enable \"Read save\" in the toolbar.", MessageType.Info);
                return;
            }

            if (save == null)
            {
                EditorGUILayout.HelpBox("No save data loaded.", MessageType.Info);
                return;
            }

            BeginScroll();

            BalanceGui.SectionHeader("Save location");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(save.SaveDirectory, EditorStyles.textField, GUILayout.Height(18f));
            if (GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Width(56f)))
            {
                EditorUtility.RevealInFinder(save.SaveDirectory);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            BalanceGui.DrawChip(save.HasPartySave ? "Party.json" : "no Party.json",
                save.HasPartySave ? BalanceSeverity.Ok : BalanceSeverity.Info);
            BalanceGui.DrawChip(save.HasMetaSave ? "Meta.json" : "no Meta.json",
                save.HasMetaSave ? BalanceSeverity.Ok : BalanceSeverity.Info);
            BalanceGui.DrawChip(save.HasRunSave ? "Run.json" : "no Run.json",
                save.HasRunSave ? BalanceSeverity.Ok : BalanceSeverity.Info);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!save.HasPartySave)
            {
                EditorGUILayout.HelpBox(
                    "No party save yet, so there is no real progression to audit. Play a run and come back.",
                    MessageType.Info);
                EndScroll();
                return;
            }

            BalanceGui.SectionHeader(
                "Real party",
                "Rebuilt from each hero's saved XP bank and activated sphere-grid nodes plus equipped "
                + "gear. Kills split XP evenly across the fielded party (XpSplit), and each hero "
                + "spends their own bank at the hub.");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.HeaderCell("Hero", NameWidth);
            BalanceGui.HeaderCell("Bank", MetricWidth, "Unspent XP.");
            BalanceGui.HeaderCell("Nodes", StatWidth, "Activated / authored sphere-grid nodes.");
            BalanceGui.HeaderCell("Next cost", MetricWidth, "Cheapest node on the hero's frontier.");
            BalanceGui.HeaderCell("Gear", WideWidth);
            EditorGUILayout.EndHorizontal();

            foreach (var hero in save.Heroes)
            {
                var severity = hero.Definition == null
                    ? BalanceSeverity.Warning
                    : hero.GridComplete
                        ? BalanceSeverity.Info
                        : BalanceSeverity.Ok;

                EditorGUILayout.BeginHorizontal();
                BalanceGui.AssetCell(hero.Definition, hero.Definition != null ? hero.Definition.DisplayName : hero.HeroKey,
                    NameWidth, severity);
                BalanceGui.Cell(hero.Xp.ToString(), MetricWidth,
                    hero.CanAffordNext ? BalanceSeverity.Info : BalanceSeverity.Ok,
                    hero.CanAffordNext ? "Can afford a node right now — a hub visit is due." : null);
                BalanceGui.Cell($"{hero.NodesActivated}/{hero.NodesTotal}", StatWidth, severity,
                    hero.GridComplete ? "Grid complete: further XP does nothing." : null);
                BalanceGui.Cell(hero.CheapestNextCost >= 0 ? hero.CheapestNextCost.ToString() : "complete", MetricWidth);
                BalanceGui.Cell(hero.Gear.Count > 0 ? $"{hero.Gear.Count} item(s)" : "none", WideWidth,
                    BalanceSeverity.Ok,
                    hero.Gear.Count > 0 ? string.Join(", ", GearNames(hero.Gear)) : null);
                EditorGUILayout.EndHorizontal();
            }

            BalanceGui.SectionHeader("Wallet and investment");

            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell($"Gold {save.Gold}", 120f);
            BalanceGui.Cell($"Essence {save.Essence}", 120f);
            if (save.LegacyBonusSlots > 0)
            {
                BalanceGui.Cell($"Legacy slots {save.LegacyBonusSlots} (refund pending)", 220f, BalanceSeverity.Info,
                    "The Essence-bought global slot upgrade was retired for per-hero MagicSlot grid "
                    + "nodes; the game refunds the Essence on its next launch.");
            }
            BalanceGui.Cell($"Potions carried {save.PotionsCarried}/{save.PotionCap}", 220f, BalanceSeverity.Ok,
                "Carried right now. The level analysis assumes a full belt instead, because DungeonManager "
                + "tops the belt up to its cap on entering each dungeon — otherwise the same save would read "
                + "as clearable or not depending on when it happened to be written.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell($"Run '{save.RunKey}' at level index {save.CurrentLevelIndex}", 300f);
            BalanceGui.Cell($"Magic discovered {save.DiscoveredMagicCount}", 180f);
            BalanceGui.Cell($"Combos discovered {save.DiscoveredComboCount}", 180f);
            EditorGUILayout.EndHorizontal();

            if (save.MagicUpgrades.Count > 0)
            {
                EditorGUILayout.Space(4f);
                foreach (var upgrade in save.MagicUpgrades)
                {
                    BalanceGui.Paragraph(
                        $"{upgrade.Key} — upgrade level {upgrade.Level} (+{upgrade.PowerBonus} power)",
                        BalanceGui.WrapMiniStyle);
                }
            }

            BalanceGui.SectionHeader(
                "Essence pacing",
                "Derived from MetaProgressManager's own constants — the shape of the economy, whatever the "
                + "current wallet happens to hold.");

            var firstSeverity = save.ClearsToFirstUpgrade > _rules.TargetClearsToFirstUpgrade * 1.5f
                ? BalanceSeverity.Warning
                : BalanceSeverity.Ok;
            var maxSeverity = save.ClearsToMaxOneMagic > _rules.MaxClearsToMaxOneMagic
                ? BalanceSeverity.Info
                : BalanceSeverity.Ok;

            EditorGUILayout.BeginHorizontal();
            BalanceGui.Cell($"{save.EssencePerClear} Essence per level-clear", 220f);
            BalanceGui.Cell($"first upgrade: {save.ClearsToFirstUpgrade:0.0} clears", 220f, firstSeverity,
                $"Target is {_rules.TargetClearsToFirstUpgrade} clears.");
            BalanceGui.Cell($"max one magic: {save.ClearsToMaxOneMagic:0} clears", 220f, maxSeverity,
                $"{save.EssenceToMaxOneMagic} Essence total; ceiling is {_rules.MaxClearsToMaxOneMagic} clears.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            BalanceGui.Paragraph(
                "Findings for this save (including the level it would die on) are listed on the Issues tab "
                + "under the Save category.",
                BalanceGui.WrapMiniStyle);

            EndScroll();
        }

        private static List<string> GearNames(List<ItemSO> gear)
        {
            var names = new List<string>();
            foreach (var item in gear)
            {
                if (item != null)
                {
                    names.Add(string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName);
                }
            }
            return names;
        }

        // ================================================================== Issues

        private void DrawIssuesTab()
        {
            if (_report == null)
            {
                return;
            }

            DrawFilterRow("Hide informational findings.");

            var issues = _report.SortedIssues();
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing outside its target band. Everything measured is inside the rules.",
                    MessageType.Info);
                return;
            }

            BeginScroll();

            string lastCategory = null;
            foreach (var issue in issues)
            {
                if (_problemsOnly && issue.Severity <= BalanceSeverity.Info)
                {
                    continue;
                }
                if (!MatchesFilter(issue.Title) && !MatchesFilter(issue.Subject) && !MatchesFilter(issue.Category))
                {
                    continue;
                }

                if (issue.Category != lastCategory)
                {
                    lastCategory = issue.Category;
                    BalanceGui.SectionHeader(issue.Category);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                BalanceGui.DrawChip(BalanceGui.SymbolFor(issue.Severity), issue.Severity);

                // The title expands into the remaining width and wraps; no FlexibleSpace, or the
                // label would be squeezed to its minimum and truncated.
                BalanceGui.Paragraph(issue.Title, BalanceGui.WrapBoldStyle, BalanceGui.TextColorFor(issue.Severity));

                if (issue.Asset != null && GUILayout.Button("select", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    Selection.activeObject = issue.Asset;
                    EditorGUIUtility.PingObject(issue.Asset);
                }
                EditorGUILayout.EndHorizontal();

                // Which asset (and which array entry) the finding is about. Several levels or heroes
                // routinely produce the same title, so without this the cards are indistinguishable.
                BalanceGui.Paragraph(issue.Subject, BalanceGui.WrapMiniStyle);

                BalanceGui.Paragraph(issue.Detail);
                if (!string.IsNullOrEmpty(issue.Suggestion))
                {
                    BalanceGui.Paragraph("→ " + issue.Suggestion, BalanceGui.WrapStyle,
                        new Color(0.7f, 0.85f, 0.7f));
                }

                EditorGUILayout.EndVertical();
            }

            EndScroll();
        }
    }
}
