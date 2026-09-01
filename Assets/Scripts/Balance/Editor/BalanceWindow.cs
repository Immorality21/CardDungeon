using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// The balance dashboard. Reads every relevant asset, measures it against
    /// <see cref="BalanceRulesSO"/>, colours whatever is outside its band — and lets the offending
    /// numbers be edited in place, re-measuring as you type, so tuning is a loop inside one window
    /// instead of a round trip through the inspector.
    ///
    /// The window is only a view: all of the arithmetic lives in <c>Assets/Scripts/Balance</c>, which
    /// the EditMode balance tests use too. If the window is green, the tests pass, and vice versa.
    /// </summary>
    public partial class BalanceWindow : EditorWindow
    {
        private enum Tab
        {
            Party,
            Enemies,
            Levels,
            Variety,
            Elements,
            Simulation,
            Save,
            Issues
        }

        private static readonly string[] TabLabels =
        {
            "Party",
            "Enemies",
            "Levels & Runs",
            "Variety",
            "Elements & Unlocks",
            "Simulation",
            "Save Audit",
            "Issues"
        };

        // Remembering the tab across reopens matters here: tuning is an iterative loop, and being
        // dropped back on Issues every time you reopen the window is friction.
        private const string TabPrefKey = "CardDungeon.Balance.Tab";
        private const string SimulatePrefKey = "CardDungeon.Balance.Simulate";

        private BalanceRulesSO _rules;
        private BalanceReport _report;
        private Tab _tab = Tab.Issues;

        private readonly Dictionary<Tab, Vector2> _scroll = new Dictionary<Tab, Vector2>();
        private readonly Dictionary<Object, SerializedObject> _serializedCache = new Dictionary<Object, SerializedObject>();

        private bool _runSimulation;
        private bool _includeSaveAudit = true;
        private bool _problemsOnly;
        private string _filter = "";

        private bool _needsReanalyze;
        private double _lastAnalyzeSeconds;

        /// <summary>Whether the report on screen was built with the simulated phases included.</summary>
        private bool _reportIncludesSimulation;

        /// <summary>
        /// Something changed under a simulated report, and re-measuring it is too expensive to do
        /// behind the user's back. The numbers stay on screen — stale and labelled — until Re-analyze.
        /// </summary>
        private bool _simulationStale;

        /// <summary>The <see cref="BalanceAssetWatcher"/> tick the current report was measured at.</summary>
        private int _analyzedAssetVersion;

        /// <summary>Set by OpenFor(...) so the Levels tab lands on the run you came from.</summary>
        private RunDefinitionSO _focusRun;

        [MenuItem("Tools/Balance/Balance Analyzer")]
        public static BalanceWindow Open()
        {
            var window = GetWindow<BalanceWindow>("Balance");
            window.minSize = new Vector2(900f, 460f);
            window.EnsureAnalyzed();
            return window;
        }

        /// <summary>Opens the window focused on one run — used by the level layout editor's toolbar.</summary>
        public static BalanceWindow OpenFor(RunDefinitionSO run)
        {
            var window = Open();
            window._focusRun = run;
            window.SelectTab(Tab.Levels);
            window.Repaint();
            return window;
        }

        /// <summary>
        /// Opens the window focused on whichever run uses this hand-built layout. Called from the
        /// Manual Level Layout editor, which owns the spatial side of the same level.
        /// </summary>
        public static BalanceWindow OpenForLayout(ManualLevelLayoutSO layout)
        {
            if (layout == null)
            {
                return Open();
            }

            foreach (var run in BalanceAssetCollector.FindAll<RunDefinitionSO>())
            {
                if (run == null || run.Levels == null)
                {
                    continue;
                }
                foreach (var level in run.Levels)
                {
                    if (level != null && level.ManualLayout == layout)
                    {
                        return OpenFor(run);
                    }
                }
            }

            // The layout is not referenced by any run yet — still useful to show the run list.
            var window = Open();
            window.SelectTab(Tab.Levels);
            window.Repaint();
            return window;
        }

        /// <summary>Opens the window focused on one enemy — used by the EnemySO inspector footer.</summary>
        public static BalanceWindow OpenFor(EnemySO enemy)
        {
            var window = Open();
            window.SelectTab(Tab.Enemies);
            window._filter = enemy != null ? enemy.name : "";
            window.Repaint();
            return window;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Balance");
            _tab = (Tab)EditorPrefs.GetInt(TabPrefKey, (int)Tab.Issues);
            _runSimulation = EditorPrefs.GetBool(SimulatePrefKey, false);
        }

        /// <summary>Turns simulation on or off and remembers the choice across reopens.</summary>
        private void SetSimulation(bool enabled)
        {
            _runSimulation = enabled;
            EditorPrefs.SetBool(SimulatePrefKey, enabled);
        }

        private void SelectTab(Tab tab)
        {
            _tab = tab;
            EditorPrefs.SetInt(TabPrefKey, (int)tab);
        }

        private void OnFocus()
        {
            // Assets can be edited elsewhere while this window is open, so returning to it is the
            // right moment to re-measure — but only when something actually moved. This used to be
            // unconditional, which with Simulate on meant paying a ~19s analysis every time the
            // window was clicked into, almost always to arrive at the identical report.
            if (BalanceAssetWatcher.Version != _analyzedAssetVersion)
            {
                _needsReanalyze = true;
            }
        }

        private void OnGUI()
        {
            EnsureAnalyzed();

            DrawToolbar();
            DrawTabBar();

            EditorGUILayout.Space(2f);

            switch (_tab)
            {
                case Tab.Party:
                    DrawPartyTab();
                    break;
                case Tab.Enemies:
                    DrawEnemiesTab();
                    break;
                case Tab.Levels:
                    DrawLevelsTab();
                    break;
                case Tab.Variety:
                    DrawVarietyTab();
                    break;
                case Tab.Elements:
                    DrawElementsTab();
                    break;
                case Tab.Simulation:
                    DrawSimulationTab();
                    break;
                case Tab.Save:
                    DrawSaveTab();
                    break;
                default:
                    DrawIssuesTab();
                    break;
            }

            DrawFooter();

            if (_needsReanalyze && Event.current.type == EventType.Repaint)
            {
                ServicePendingAnalysis();
                Repaint();
            }
        }

        /// <summary>
        /// Acts on a queued re-measure — the automatic path, reached from focus, an inline edit or a
        /// toolbar toggle.
        ///
        /// <para>The rule is that <b>an automatic trigger may never start the simulated phases.</b>
        /// Without simulation an analysis costs ~130ms, which is inside the "re-measures as you type"
        /// budget the window is built around. With it, the encounter sims, floor sims and frontier
        /// sweeps cost ~19s on this project, which is not a re-measure but a hang — and the window was
        /// spending it on every focus and every keystroke. So a simulated report is left standing and
        /// flagged stale instead, and the user decides when to pay for a fresh one.</para>
        ///
        /// <para>This matches what the Simulate toggle already tells you it is for ("Slower — leave off
        /// while tuning numbers"): sim off is the tuning loop, sim on is a measurement pass.</para>
        /// </summary>
        private void ServicePendingAnalysis()
        {
            if (!_needsReanalyze)
            {
                return;
            }

            if (_runSimulation && _report != null)
            {
                _needsReanalyze = false;
                _simulationStale = true;
                // The asset state is now accounted for: it is recorded as stale rather than measured,
                // so refocusing does not queue this same decision over and over.
                _analyzedAssetVersion = BalanceAssetWatcher.Version;
                return;
            }

            Analyze();
        }

        // ------------------------------------------------------------------ chrome

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _rules = (BalanceRulesSO)EditorGUILayout.ObjectField(
                _rules, typeof(BalanceRulesSO), false, GUILayout.Width(180f));
            if (EditorGUI.EndChangeCheck())
            {
                _needsReanalyze = true;
            }

            if (_rules == null || !AssetDatabase.Contains(_rules))
            {
                if (GUILayout.Button("Create rules asset", EditorStyles.toolbarButton, GUILayout.Width(130f)))
                {
                    _rules = BalanceAssetCollector.LoadOrCreateRules(true);
                    _needsReanalyze = true;
                }
            }

            GUILayout.Space(8f);

            EditorGUIUtility.labelWidth = 44f;
            EditorGUI.BeginChangeCheck();
            int xp = EditorGUILayout.IntField("XP", _rules != null ? _rules.ReferenceHeroXp : 0, GUILayout.Width(90f));

            // The gold budget is the reproducible half of the gear story: it spends the item catalog
            // through GearLoadout, so it means the same thing on every machine. The saved-gear toggle
            // beside it reads one player's save and therefore cannot back a published number.
            int gold = EditorGUILayout.IntField(
                new GUIContent("Gold", "Gold the reference party has spent on gear, greedy-spent off "
                    + "the item catalog. 0 = no gear. Reproducible, unlike Saved gear."),
                _rules != null ? _rules.ReferencePartyGoldBudget : 0, GUILayout.Width(95f));
            bool savedGear = GUILayout.Toggle(
                _rules != null && _rules.ReferencePartyUsesSavedGear,
                new GUIContent("Saved gear", "Include the gear the save file has equipped in the reference "
                    + "party. Overrides the Gold budget, and is machine-specific - the regression suite "
                    + "never turns it on."),
                EditorStyles.toolbarButton,
                GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck() && _rules != null)
            {
                Undo.RecordObject(_rules, "Change balance reference party");
                _rules.ReferenceHeroXp = Mathf.Max(0, xp);
                _rules.ReferencePartyGoldBudget = Mathf.Max(0, gold);
                _rules.ReferencePartyUsesSavedGear = savedGear;
                EditorUtility.SetDirty(_rules);
                _needsReanalyze = true;
            }
            EditorGUIUtility.labelWidth = 0f;

            GUILayout.Space(8f);

            EditorGUI.BeginChangeCheck();
            _includeSaveAudit = GUILayout.Toggle(_includeSaveAudit,
                new GUIContent("Read save", "Load the live save files and audit real progression."),
                EditorStyles.toolbarButton, GUILayout.Width(70f));
            bool simulate = GUILayout.Toggle(_runSimulation,
                new GUIContent("Simulate", "Run headless battles: encounters, floors and investment "
                    + "frontiers. Costs seconds, not milliseconds — leave off while tuning numbers. "
                    + "While it is on, edits mark the report stale instead of re-measuring, so press "
                    + "Re-analyze when you want fresh simulated numbers."),
                EditorStyles.toolbarButton, GUILayout.Width(70f));
            if (EditorGUI.EndChangeCheck())
            {
                bool simulationToggled = simulate != _runSimulation;
                SetSimulation(simulate);

                // Flipping Simulate is an explicit request to change what gets measured, so it
                // measures now. Routing it through the automatic path would only ever mark a
                // simulated report stale and never actually run the simulation the user just asked
                // for.
                if (simulationToggled)
                {
                    Analyze();
                }
                else
                {
                    _needsReanalyze = true;
                }
            }

            GUILayout.FlexibleSpace();

            if (_simulationStale)
            {
                var previous = GUI.color;
                GUI.color = BalanceGui.TextColorFor(BalanceSeverity.Warning);
                GUILayout.Label(
                    new GUIContent("simulated numbers stale",
                        "Something changed since the last simulated run. The simulation costs seconds, "
                        + "so it is not re-run automatically — press Re-analyze."),
                    EditorStyles.miniLabel);
                GUI.color = previous;
            }

            BalanceGui.SeveritySummary(_report);

            if (GUILayout.Button(
                    new GUIContent(_simulationStale ? "Re-analyze *" : "Re-analyze",
                        _runSimulation
                            ? "Re-measure everything, simulation included. Takes seconds."
                            : "Re-measure everything."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(84f)))
            {
                _serializedCache.Clear();
                Analyze();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            int selected = GUILayout.Toolbar((int)_tab, TabLabels, GUILayout.Height(20f));
            if (selected != (int)_tab)
            {
                SelectTab((Tab)selected);
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string party = _report != null && _report.Party != null
                ? $"{_report.Party.SourceLabel} — {_report.Party.Size} hero(es), {_report.Party.HealthPool} HP "
                  + $"+ {_report.Party.HealingPool} healing"
                : "No party";
            GUILayout.Label(party, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            string scope = _reportIncludesSimulation ? "with simulation" : "no simulation";
            string staleness = _simulationStale ? " — stale, press Re-analyze" : "";
            GUILayout.Label(
                $"analysed in {_lastAnalyzeSeconds * 1000f:0} ms ({scope}){staleness}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private Vector2 BeginScroll()
        {
            if (!_scroll.ContainsKey(_tab))
            {
                _scroll[_tab] = Vector2.zero;
            }
            _scroll[_tab] = EditorGUILayout.BeginScrollView(_scroll[_tab]);
            return _scroll[_tab];
        }

        private void EndScroll()
        {
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ analysis

        private void EnsureAnalyzed()
        {
            if (_rules == null)
            {
                _rules = BalanceAssetCollector.LoadOrCreateRules(false);
            }
            if (_report != null)
            {
                return;
            }

            // Opening the window is not an explicit request to simulate. The toggle is remembered
            // across sessions, so honouring it here turned "open the Balance window" into a ~19s
            // stall with nothing on screen to explain it — which is how this was first noticed. Show
            // the cheap report immediately and let Re-analyze buy the simulated one.
            Analyze(false);
            _simulationStale = _runSimulation;
        }

        /// <summary>
        /// Measures the project, simulated phases included when the toggle asks for them. Only the
        /// explicit paths call this — the Re-analyze button, the first open, and a toggle that changes
        /// what is measured. Automatic triggers go through <see cref="ServicePendingAnalysis"/>.
        /// </summary>
        private void Analyze()
        {
            Analyze(_runSimulation);
        }

        private void Analyze(bool includeSimulation)
        {
            _needsReanalyze = false;

            if (_rules == null)
            {
                _rules = BalanceAssetCollector.LoadOrCreateRules(false);
            }

            // The simulated path blocks the editor for seconds, so say so rather than going dark. It
            // cannot report progress from inside BalanceAnalyzer without threading a callback through
            // it, but naming the work beats an unexplained freeze.
            try
            {
                if (includeSimulation)
                {
                    EditorUtility.DisplayProgressBar(
                        "Balance",
                        "Simulating encounters, floors and investment frontiers…",
                        0.5f);
                }

                double start = EditorApplication.timeSinceStartup;
                var input = BalanceAssetCollector.Collect(_rules, includeSimulation, _includeSaveAudit);
                _report = BalanceAnalyzer.Analyze(input);
                _lastAnalyzeSeconds = EditorApplication.timeSinceStartup - start;
            }
            finally
            {
                if (includeSimulation)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            _reportIncludesSimulation = includeSimulation;
            _simulationStale = false;
            _analyzedAssetVersion = BalanceAssetWatcher.Version;
        }

        /// <summary>
        /// Cached SerializedObject per asset. Rows call Update() before drawing and Apply() after, so
        /// edits are undoable and the asset is marked dirty exactly as the inspector would.
        /// </summary>
        private SerializedObject Serialized(Object asset)
        {
            if (asset == null)
            {
                return null;
            }

            if (!_serializedCache.TryGetValue(asset, out var serialized) || serialized.targetObject == null)
            {
                serialized = new SerializedObject(asset);
                _serializedCache[asset] = serialized;
            }

            serialized.Update();
            return serialized;
        }

        /// <summary>Commits a row's edits; a real change schedules a re-measure for this frame's end.</summary>
        private void Commit(SerializedObject serialized, bool changed)
        {
            if (serialized == null)
            {
                return;
            }

            if (serialized.ApplyModifiedProperties() || changed)
            {
                _needsReanalyze = true;
            }
        }

        private bool MatchesFilter(string text)
        {
            if (string.IsNullOrEmpty(_filter))
            {
                return true;
            }
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawFilterRow(string problemsOnlyTooltip)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(34f));
            _filter = EditorGUILayout.TextField(_filter, GUILayout.Width(180f));
            if (GUILayout.Button("clear", EditorStyles.miniButton, GUILayout.Width(42f)))
            {
                _filter = "";
                GUI.FocusControl(null);
            }
            GUILayout.Space(10f);
            _problemsOnly = GUILayout.Toggle(_problemsOnly,
                new GUIContent("Problems only", problemsOnlyTooltip), EditorStyles.miniButton, GUILayout.Width(96f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
