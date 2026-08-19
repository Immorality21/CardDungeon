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
            // Assets can be edited elsewhere while this window is open; re-measure on return.
            _needsReanalyze = true;
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
                Analyze();
                Repaint();
            }
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
            int level = EditorGUILayout.IntField("Level", _rules != null ? _rules.ReferenceHeroLevel : 1, GUILayout.Width(90f));
            bool savedGear = GUILayout.Toggle(
                _rules != null && _rules.ReferencePartyUsesSavedGear,
                new GUIContent("Saved gear", "Include the gear the save file has equipped in the reference party."),
                EditorStyles.toolbarButton,
                GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck() && _rules != null)
            {
                Undo.RecordObject(_rules, "Change balance reference party");
                _rules.ReferenceHeroLevel = Mathf.Max(1, level);
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
                new GUIContent("Simulate", "Run headless battles. Slower — leave off while tuning numbers."),
                EditorStyles.toolbarButton, GUILayout.Width(70f));
            if (EditorGUI.EndChangeCheck())
            {
                SetSimulation(simulate);
                _needsReanalyze = true;
            }

            GUILayout.FlexibleSpace();

            BalanceGui.SeveritySummary(_report);

            if (GUILayout.Button("Re-analyze", EditorStyles.toolbarButton, GUILayout.Width(80f)))
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

            GUILayout.Label($"analysed in {_lastAnalyzeSeconds * 1000f:0} ms", EditorStyles.miniLabel);
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
            if (_report == null)
            {
                Analyze();
            }
        }

        private void Analyze()
        {
            _needsReanalyze = false;

            if (_rules == null)
            {
                _rules = BalanceAssetCollector.LoadOrCreateRules(false);
            }

            double start = EditorApplication.timeSinceStartup;
            var input = BalanceAssetCollector.Collect(_rules, _runSimulation, _includeSaveAudit);
            _report = BalanceAnalyzer.Analyze(input);
            _lastAnalyzeSeconds = EditorApplication.timeSinceStartup - start;
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
