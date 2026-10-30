using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Hub;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Dungeon.Editor
{
    /// <summary>
    /// Graph authoring for the story line (<see cref="CampaignSO"/>, <c>Resources/Campaign.asset</c>):
    /// every run as a node - secret ones included - with its prerequisite links drawn as arrows, on the
    /// same <see cref="SphereGridView"/> the hub's story map renders. The sibling of the Sphere Grid
    /// Editor and built the same way.
    ///
    /// <para>Drag a node to move its <c>MapPosition</c>; Connect toggles "A must be cleared before B"
    /// (an entry in B's <c>Requires</c>); Add places a run asset on the canvas; the side pane edits the
    /// node and, below it, the run asset itself. Every structural change goes through
    /// <c>Undo.RecordObject</c> + <c>SetDirty</c>; the panes are SerializedObject-bound.</para>
    ///
    /// <para><b>Preview</b> runs <see cref="CampaignOps.GetStates"/> - the game's own rules - over a
    /// pretend save: click nodes to mark them cleared, and the heroes owned are the starting lineup
    /// plus everyone the cleared runs hand over. Hidden nodes stay drawn, faded, so the graph does not
    /// jump around while you tinker.</para>
    /// </summary>
    public class CampaignMapEditorWindow : EditorWindow
    {
        private const string ThemePath = "Assets/UI/Theme/CardDungeon.uss";
        private const string DefaultCampaignPath = "Assets/Resources/Campaign.asset";

        private const string SecretClass = "cm-node--secret";
        private const string HiddenClass = "cm-node--hidden";
        private const string WarningClass = "cm-node--warning";

        private CampaignSO _campaign;
        private SerializedObject _serializedCampaign;
        private PartyRosterSO _roster;

        private SphereGridView _view;
        private ObjectField _campaignField;
        private ObjectField _addRunField;
        private ToolbarToggle _addToggle;
        private ToolbarToggle _connectToggle;
        private ToolbarToggle _previewToggle;
        private Label _modeHint;
        private ScrollView _inspector;

        private int _selectedIndex = -1;
        private string _connectSourceKey;
        private readonly HashSet<string> _previewCleared = new HashSet<string>();

        // Graph key (run key) -> index into Nodes, rebuilt with the graph.
        private readonly Dictionary<string, int> _indexByKey = new Dictionary<string, int>();

        private string _dragKey;
        private Vector2 _dragOffset;
        private Vector2 _dragStart;
        private int _dragPointerId = -1;

        [MenuItem("Tools/Campaign Map Editor", false, 22)]
        public static void Open()
        {
            var window = GetWindow<CampaignMapEditorWindow>("Campaign Map Editor");
            window.minSize = new Vector2(900f, 500f);
        }

        /// <summary>Programmatic entry point (the CampaignSO inspector's Open button).</summary>
        public void SetTarget(CampaignSO campaign)
        {
            _campaign = campaign;
            _serializedCampaign = campaign != null ? new SerializedObject(campaign) : null;
            _selectedIndex = -1;
            _connectSourceKey = null;
            _previewCleared.Clear();
            _campaignField?.SetValueWithoutNotify(campaign);
            RebuildAll();
            _view?.FrameAll();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            _serializedCampaign?.Update();
            RebuildAll();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            if (theme != null)
            {
                root.styleSheets.Add(theme);
            }

            _roster = FindRoster();

            // --- toolbar -----------------------------------------------------
            var toolbar = new Toolbar();
            _campaignField = new ObjectField("Campaign") { objectType = typeof(CampaignSO), allowSceneObjects = false };
            _campaignField.style.minWidth = 240f;
            _campaignField.RegisterValueChangedCallback(evt => SetTarget(evt.newValue as CampaignSO));
            toolbar.Add(_campaignField);

            _addToggle = new ToolbarToggle { text = "Add Run" };
            _addToggle.RegisterValueChangedCallback(_ => UpdateModeHint());
            toolbar.Add(_addToggle);

            _addRunField = new ObjectField { objectType = typeof(RunDefinitionSO), allowSceneObjects = false };
            _addRunField.style.minWidth = 170f;
            _addRunField.tooltip = "The run Add places. Leave empty to pick the first run in the project that is not on the map.";
            toolbar.Add(_addRunField);

            toolbar.Add(new ToolbarButton(CreateNewRun) { text = "New Run…" });

            _connectToggle = new ToolbarToggle { text = "Connect" };
            _connectToggle.RegisterValueChangedCallback(_ => { CancelConnect(); UpdateModeHint(); });
            toolbar.Add(_connectToggle);

            toolbar.Add(new ToolbarButton(DeleteSelected) { text = "Remove Node" });

            _previewToggle = new ToolbarToggle { text = "Preview save" };
            _previewToggle.RegisterValueChangedCallback(_ => { UpdateModeHint(); RebuildAll(); });
            toolbar.Add(_previewToggle);

            toolbar.Add(new ToolbarButton(() => { _previewCleared.Clear(); RebuildAll(); }) { text = "Reset preview" });
            toolbar.Add(new ToolbarButton(() => _view?.FrameAll()) { text = "Frame" });

            _modeHint = new Label();
            _modeHint.style.unityTextAlign = TextAnchor.MiddleLeft;
            _modeHint.style.marginLeft = 8f;
            toolbar.Add(_modeHint);
            root.Add(toolbar);

            // --- body: graph | inspector ------------------------------------
            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };

            _view = new SphereGridView
            {
                PanButtons = (1 << 1) | (1 << 2),
                StateClassNames = CampaignPresenter.StateClasses,
                EdgeStrongStateClass = CampaignPresenter.CompletedClass,
                EdgeOpenStateClass = CampaignPresenter.AvailableClass,
                DrawArrows = true
            };
            _view.style.flexGrow = 1f;
            _view.NodeClicked += OnNodeClicked;
            _view.NodePointerDown += OnNodePointerDown;
            _view.BackgroundPointerDown += OnBackgroundPointerDown;
            _view.RegisterCallback<PointerMoveEvent>(OnViewPointerMove);
            _view.RegisterCallback<PointerUpEvent>(OnViewPointerUp);
            body.Add(_view);

            _inspector = new ScrollView(ScrollViewMode.Vertical);
            _inspector.style.width = 380f;
            _inspector.style.flexShrink = 0f;
            _inspector.style.paddingLeft = 8f;
            _inspector.style.paddingRight = 8f;
            body.Add(_inspector);

            root.Add(body);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.focusable = true;

            UpdateModeHint();

            if (_campaign == null)
            {
                var fallback = AssetDatabase.LoadAssetAtPath<CampaignSO>(DefaultCampaignPath);
                if (fallback != null)
                {
                    SetTarget(fallback);
                    return;
                }
            }
            _campaignField.SetValueWithoutNotify(_campaign);
            RebuildAll();
            _view.FrameAll();
        }

        private void UpdateModeHint()
        {
            if (_modeHint == null)
            {
                return;
            }
            if (_connectToggle != null && _connectToggle.value)
            {
                _modeHint.text = "Connect: click the run that must be cleared, then the run it opens (again to unlink). Esc cancels.";
            }
            else if (_addToggle != null && _addToggle.value)
            {
                _modeHint.text = "Add: click empty canvas to place the run.";
            }
            else if (_previewToggle != null && _previewToggle.value)
            {
                _modeHint.text = "Preview: click a run to mark it cleared. Faded = hidden on the map.";
            }
            else
            {
                _modeHint.text = "Left-drag moves a run · middle/right-drag pans · scroll zooms.";
            }
        }

        // --- graph -----------------------------------------------------------------

        private void RebuildAll()
        {
            RebuildGraph();
            RebuildInspector();
        }

        private void RebuildGraph()
        {
            if (_view == null)
            {
                return;
            }

            _indexByKey.Clear();
            var nodes = new List<SphereGridView.NodeInfo>();
            var edges = new List<(string A, string B)>();

            if (_campaign == null || _campaign.Nodes == null)
            {
                _view.SetGraph(nodes, edges);
                return;
            }

            var positions = CampaignPresenter.ResolvePositions(_campaign);
            var warnings = WarningIndices();

            for (int i = 0; i < _campaign.Nodes.Count; i++)
            {
                var node = _campaign.Nodes[i];
                if (node?.Run == null)
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(node.Run);
                if (_indexByKey.ContainsKey(key))
                {
                    continue;
                }
                _indexByKey[key] = i;
                nodes.Add(new SphereGridView.NodeInfo
                {
                    Key = key,
                    Position = positions.TryGetValue(key, out var p) ? p : Vector2.zero,
                    KindClass = CampaignPresenter.KindClass(node),
                    Glyph = AuthoringGlyph(node),
                    Caption = Caption(node),
                    IsStart = node.Requires == null || node.Requires.Count == 0
                });
            }

            foreach (var pair in _indexByKey)
            {
                var node = _campaign.Nodes[pair.Value];
                if (node.Requires == null)
                {
                    continue;
                }
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite == null)
                    {
                        continue;
                    }
                    string from = CampaignOps.RunKeyOf(prerequisite);
                    if (_indexByKey.ContainsKey(from))
                    {
                        edges.Add((from, pair.Key));
                    }
                }
            }

            _view.SetGraph(nodes, edges);

            foreach (var pair in _indexByKey)
            {
                var node = _campaign.Nodes[pair.Value];
                _view.SetNodeFlag(pair.Key, SecretClass, node.Secret);
                _view.SetNodeFlag(pair.Key, WarningClass, warnings.Contains(pair.Value));
            }

            if (_selectedIndex >= 0 && (_selectedIndex >= _campaign.Nodes.Count || KeyOf(_selectedIndex) == null))
            {
                _selectedIndex = -1;
            }
            _view.SetSelected(KeyOf(_selectedIndex));
            RefreshStates();
        }

        private void RefreshStates()
        {
            if (_view == null || _campaign == null)
            {
                return;
            }

            bool preview = _previewToggle != null && _previewToggle.value;
            if (!preview)
            {
                foreach (var pair in _indexByKey)
                {
                    _view.SetNodeState(pair.Key, CampaignPresenter.AvailableClass);
                    _view.SetNodeFlag(pair.Key, HiddenClass, false);
                }
                return;
            }

            var states = CampaignOps.GetStates(_campaign, _previewCleared, null, PreviewOwnedHeroes());
            foreach (var state in states)
            {
                if (state?.Node?.Run == null)
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(state.Node.Run);
                _view.SetNodeState(key, CampaignPresenter.StateClass(state.Status));
                _view.SetNodeFlag(key, HiddenClass, !state.IsVisible);
            }
        }

        /// <summary>The starting lineup plus every hero a cleared run hands over - what a save that
        /// cleared exactly those runs would own (a room-event join is a chance, so it is not counted).</summary>
        private HashSet<string> PreviewOwnedHeroes()
        {
            var owned = new HashSet<string>();
            if (_roster != null)
            {
                foreach (var hero in _roster.StartingLineup())
                {
                    owned.Add(CampaignOps.HeroKeyOf(hero));
                }
            }
            foreach (var pair in _indexByKey)
            {
                if (_previewCleared.Contains(pair.Key))
                {
                    CampaignOps.AddRescuedHeroKeys(_campaign.Nodes[pair.Value].Run, owned);
                }
            }
            return owned;
        }

        private static string AuthoringGlyph(CampaignNodeEntry node)
        {
            if (node.Run != null && node.Run.Challenge)
            {
                return "!";
            }
            if (node.Secret)
            {
                return "?";
            }
            return node.Optional ? "◇" : "★";
        }

        /// <summary>The run's name, plus who it hands over, so where each hero comes from reads off the map.</summary>
        private static string Caption(CampaignNodeEntry node)
        {
            string name = CampaignOps.DisplayNameOf(node.Run);
            var heroes = new HashSet<string>();
            CampaignOps.AddRescuedHeroKeys(node.Run, heroes);
            var lines = new List<string> { name };
            if (heroes.Count > 0)
            {
                lines.Add("+ " + string.Join(", ", heroes));
            }
            if (node.RequiresHeroes != null && node.RequiresHeroes.Any(h => h != null))
            {
                lines.Add("needs " + string.Join(", ", node.RequiresHeroes.Where(h => h != null).Select(CampaignOps.HeroKeyOf)));
            }
            return string.Join("\n", lines);
        }

        private string KeyOf(int index)
        {
            if (_campaign?.Nodes == null || index < 0 || index >= _campaign.Nodes.Count)
            {
                return null;
            }
            var run = _campaign.Nodes[index]?.Run;
            return run != null ? CampaignOps.RunKeyOf(run) : null;
        }

        private CampaignNodeEntry NodeFor(string key)
        {
            return key != null && _indexByKey.TryGetValue(key, out int index) ? _campaign.Nodes[index] : null;
        }

        // --- interactions ----------------------------------------------------------

        private void OnNodeClicked(string key)
        {
            if (_connectToggle != null && _connectToggle.value)
            {
                HandleConnectClick(key);
                return;
            }

            if (_previewToggle != null && _previewToggle.value)
            {
                if (!_previewCleared.Remove(key))
                {
                    _previewCleared.Add(key);
                }
                RefreshStates();
            }

            Select(key);
        }

        private void Select(string key)
        {
            _selectedIndex = key != null && _indexByKey.TryGetValue(key, out int index) ? index : -1;
            _view.SetSelected(key);
            RebuildInspector();
        }

        private void HandleConnectClick(string key)
        {
            if (_connectSourceKey == null)
            {
                _connectSourceKey = key;
                _view.SetNodeFlag(key, "sg-node--source", true);
                return;
            }

            if (_connectSourceKey != key)
            {
                ToggleRequirement(_connectSourceKey, key);
            }
            CancelConnect();
            RebuildAll();
        }

        private void CancelConnect()
        {
            if (_connectSourceKey != null)
            {
                _view?.SetNodeFlag(_connectSourceKey, "sg-node--source", false);
            }
            _connectSourceKey = null;
            _view?.SetGhostEdge(null, null);
        }

        /// <summary>Adds <paramref name="fromKey"/>'s run to <paramref name="toKey"/>'s Requires, or removes it.</summary>
        private void ToggleRequirement(string fromKey, string toKey)
        {
            var from = NodeFor(fromKey);
            var to = NodeFor(toKey);
            if (from?.Run == null || to == null)
            {
                return;
            }

            Undo.RecordObject(_campaign, "Toggle Campaign Link");
            if (to.Requires == null)
            {
                to.Requires = new List<RunDefinitionSO>();
            }
            if (!to.Requires.Remove(from.Run))
            {
                to.Requires.Add(from.Run);
            }
            MarkDirty();
        }

        private void OnNodePointerDown(string key, PointerDownEvent evt)
        {
            if (evt.button != 0 || _campaign == null || (_connectToggle != null && _connectToggle.value))
            {
                return;
            }

            var node = NodeFor(key);
            if (node == null)
            {
                return;
            }

            BakeAutoLayoutIfNeeded();

            _dragKey = key;
            _dragStart = node.MapPosition;
            _dragOffset = node.MapPosition - _view.ToGridSpace(evt.position);
            _dragPointerId = evt.pointerId;
            Undo.RecordObject(_campaign, "Move Campaign Node");
            _view.CapturePointer(evt.pointerId);
        }

        /// <summary>
        /// A campaign with no authored positions is auto-laid-out by tier; the first drag writes that
        /// layout into every node, or the others would all collapse onto the origin.
        /// </summary>
        private void BakeAutoLayoutIfNeeded()
        {
            if (_campaign.Nodes.Any(n => n?.Run != null && n.MapPosition != Vector2.zero))
            {
                return;
            }
            Undo.RecordObject(_campaign, "Bake Campaign Layout");
            var positions = CampaignPresenter.ResolvePositions(_campaign);
            foreach (var node in _campaign.Nodes)
            {
                if (node?.Run != null && positions.TryGetValue(CampaignOps.RunKeyOf(node.Run), out var p))
                {
                    node.MapPosition = p;
                }
            }
            MarkDirty();
        }

        private void OnViewPointerMove(PointerMoveEvent evt)
        {
            if (_dragKey != null && evt.pointerId == _dragPointerId)
            {
                var node = NodeFor(_dragKey);
                if (node != null)
                {
                    var position = (Vector2)_view.ToGridSpace(evt.position) + _dragOffset;
                    // Snap to a 10-unit grid: positions are hand-tuned numbers and stay readable that way.
                    node.MapPosition = new Vector2(Mathf.Round(position.x / 10f) * 10f, Mathf.Round(position.y / 10f) * 10f);
                    _view.SetNodePosition(_dragKey, node.MapPosition);
                }
                return;
            }

            if (_connectSourceKey != null)
            {
                _view.SetGhostEdge(_connectSourceKey, _view.ToGridSpace(evt.position));
            }
        }

        private void OnViewPointerUp(PointerUpEvent evt)
        {
            if (_dragKey != null && evt.pointerId == _dragPointerId)
            {
                _view.ReleasePointer(evt.pointerId);
                var node = NodeFor(_dragKey);
                _dragKey = null;
                _dragPointerId = -1;
                if (node != null && node.MapPosition != _dragStart)
                {
                    MarkDirty();
                    RebuildInspector();
                }
            }
        }

        private void OnBackgroundPointerDown(Vector2 gridPosition, PointerDownEvent evt)
        {
            if (evt.button != 0 || _campaign == null)
            {
                return;
            }

            if (_addToggle != null && _addToggle.value)
            {
                AddRunAt(gridPosition);
                evt.StopPropagation();
                return;
            }

            Select(null);
        }

        private void AddRunAt(Vector2 gridPosition)
        {
            var run = _addRunField.value as RunDefinitionSO ?? FirstRunNotOnMap();
            if (run == null)
            {
                EditorUtility.DisplayDialog("Add Run", "Every run in the project is already on the map. Use New Run… to make one.", "OK");
                return;
            }
            if (_campaign.FindNode(run) != null)
            {
                EditorUtility.DisplayDialog("Add Run", $"{CampaignOps.DisplayNameOf(run)} is already on the map.", "OK");
                return;
            }

            BakeAutoLayoutIfNeeded();
            Undo.RecordObject(_campaign, "Add Campaign Node");
            _campaign.Nodes.Add(new CampaignNodeEntry
            {
                Run = run,
                Optional = true,
                MapPosition = new Vector2(Mathf.Round(gridPosition.x / 10f) * 10f, Mathf.Round(gridPosition.y / 10f) * 10f)
            });
            MarkDirty();
            _selectedIndex = _campaign.Nodes.Count - 1;
            _addRunField.SetValueWithoutNotify(null);
            RebuildAll();
        }

        private RunDefinitionSO FirstRunNotOnMap()
        {
            return AllRuns().FirstOrDefault(r => _campaign.FindNode(r) == null);
        }

        private static List<RunDefinitionSO> AllRuns()
        {
            return AssetDatabase.FindAssets("t:RunDefinitionSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RunDefinitionSO>)
                .Where(r => r != null)
                .ToList();
        }

        /// <summary>Creates an empty run asset and arms Add with it.</summary>
        private void CreateNewRun()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Run", "NewRun", "asset",
                "Where to save the new run definition.", "Assets/ScriptableObjects/Runs");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var run = CreateInstance<RunDefinitionSO>();
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            run.Key = name;
            global::ImmoralityGaming.Localization.Loc.EditorSet(global::ImmoralityGaming.Localization.Loc.KeyFor("run", name, "name"), ObjectNames.NicifyVariableName(name));
            AssetDatabase.CreateAsset(run, path);
            AssetDatabase.SaveAssets();

            _addRunField.value = run;
            if (_addToggle != null)
            {
                _addToggle.value = true;
            }
        }

        private void DeleteSelected()
        {
            var key = KeyOf(_selectedIndex);
            var node = NodeFor(key);
            if (node == null)
            {
                return;
            }

            Undo.RecordObject(_campaign, "Remove Campaign Node");
            _campaign.Nodes.Remove(node);
            foreach (var other in _campaign.Nodes)
            {
                other?.Requires?.RemoveAll(r => r == node.Run);
            }
            MarkDirty();
            _selectedIndex = -1;
            RebuildAll();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CancelConnect();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete && !(evt.target is TextField) && _selectedIndex >= 0)
            {
                DeleteSelected();
                evt.StopPropagation();
            }
        }

        private void MarkDirty()
        {
            EditorUtility.SetDirty(_campaign);
            _serializedCampaign?.Update();
        }

        // --- inspector -------------------------------------------------------------

        private void RebuildInspector()
        {
            if (_inspector == null)
            {
                return;
            }
            _inspector.Clear();

            if (_campaign == null)
            {
                _inspector.Add(new HelpBox("Pick a CampaignSO (Assets/Resources/Campaign.asset).", HelpBoxMessageType.Info));
                return;
            }

            if (_selectedIndex < 0 || KeyOf(_selectedIndex) == null)
            {
                AddSummary();
                return;
            }

            _serializedCampaign ??= new SerializedObject(_campaign);
            _serializedCampaign.Update();
            var element = _serializedCampaign.FindProperty("Nodes").GetArrayElementAtIndex(_selectedIndex);
            var node = _campaign.Nodes[_selectedIndex];

            var title = new Label(CampaignOps.DisplayNameOf(node.Run));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14f;
            title.style.marginTop = 6f;
            _inspector.Add(title);

            foreach (var warning in WarningsFor(_selectedIndex))
            {
                _inspector.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
            }

            var nodeBox = new VisualElement();
            foreach (var field in new[] { "Run", "Requires", "RequiresHeroes", "UnlockMode", "Secret", "Optional", "MapPosition" })
            {
                var property = element.FindPropertyRelative(field);
                if (property != null)
                {
                    nodeBox.Add(new PropertyField(property));
                }
            }
            nodeBox.Bind(_serializedCampaign);
            nodeBox.TrackSerializedObjectValue(_serializedCampaign, _ => RebuildGraph());
            _inspector.Add(nodeBox);

            var opens = _campaign.Nodes.Where(n => n?.Run != null && n.Requires != null && n.Requires.Contains(node.Run))
                .Select(n => CampaignOps.DisplayNameOf(n.Run)).ToList();
            var opensLabel = new Label("Opens: " + (opens.Count > 0 ? string.Join(", ", opens) : "(nothing)"));
            opensLabel.style.marginTop = 4f;
            opensLabel.style.whiteSpace = WhiteSpace.Normal;
            _inspector.Add(opensLabel);

            if (node.Run != null)
            {
                var runHeader = new Label("Run asset");
                runHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                runHeader.style.marginTop = 10f;
                _inspector.Add(runHeader);

                _inspector.Add(new Button(() => { Selection.activeObject = node.Run; EditorGUIUtility.PingObject(node.Run); })
                {
                    text = "Select in Project"
                });

                var runSerialized = new SerializedObject(node.Run);
                var runInspector = new InspectorElement(runSerialized);
                // A rename or a new captive changes the caption; redraw the graph, keep the pane.
                runInspector.TrackSerializedObjectValue(runSerialized, _ => RebuildGraph());
                _inspector.Add(runInspector);
            }
        }

        private void AddSummary()
        {
            int nodeCount = _campaign.Nodes != null ? _campaign.Nodes.Count : 0;
            _inspector.Add(new HelpBox(
                $"{_campaign.name}: {nodeCount} run(s).\n\nSelect a run to edit it. Arrows point from the run that " +
                "must be cleared to the run it opens. ? = secret, ◇ = optional, ! = challenge, ★ = main line; " +
                "a red ring is an authoring problem.",
                HelpBoxMessageType.Info));

            var problems = AllWarnings();
            if (problems.Count == 0)
            {
                _inspector.Add(new HelpBox("No authoring problems found.", HelpBoxMessageType.None));
            }
            foreach (var problem in problems)
            {
                _inspector.Add(new HelpBox(problem, HelpBoxMessageType.Warning));
            }
        }

        // --- validation (the same checks CampaignAssetTests and HeroUnlockTests run) ----------------

        private HashSet<int> WarningIndices()
        {
            var indices = new HashSet<int>();
            if (_campaign == null)
            {
                return indices;
            }
            indices.UnionWith(CampaignOps.GetNodesWithOutsidePrerequisites(_campaign));
            indices.UnionWith(CampaignOps.GetUnreachableNodes(_campaign));
            indices.UnionWith(CampaignOps.GetNodesWithBrokenHeroGates(_campaign, _roster));
            for (int i = 0; i < _campaign.Nodes.Count; i++)
            {
                var node = _campaign.Nodes[i];
                if (node?.Run != null && node.Run.Challenge && !node.Optional)
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        private List<string> WarningsFor(int index)
        {
            var warnings = new List<string>();
            if (CampaignOps.GetNodesWithOutsidePrerequisites(_campaign).Contains(index))
            {
                warnings.Add("Requires a run that is not on the map, so it can never open.");
            }
            if (CampaignOps.GetUnreachableNodes(_campaign).Contains(index))
            {
                warnings.Add("Unreachable: part of a prerequisite cycle, or downstream of one.");
            }
            if (CampaignOps.GetNodesWithBrokenHeroGates(_campaign, _roster).Contains(index))
            {
                warnings.Add("Hero gate on a hero the player is not guaranteed to own on arrival (a captive on an optional branch, or met by chance).");
            }
            var node = _campaign.Nodes[index];
            if (node?.Run != null && node.Run.Challenge && !node.Optional)
            {
                warnings.Add("A challenge run must be optional.");
            }
            return warnings;
        }

        private List<string> AllWarnings()
        {
            var warnings = new List<string>();
            foreach (int i in CampaignOps.GetNodesWithoutRun(_campaign))
            {
                warnings.Add($"Node {i} has no run.");
            }
            foreach (var key in CampaignOps.GetDuplicateRunKeys(_campaign))
            {
                warnings.Add($"{key} is on the map more than once.");
            }
            if (CampaignOps.GetRootNodes(_campaign).Count == 0)
            {
                warnings.Add("No starting run (a node with no Requires).");
            }
            foreach (int i in WarningIndices())
            {
                foreach (var warning in WarningsFor(i))
                {
                    warnings.Add($"{CampaignOps.DisplayNameOf(_campaign.Nodes[i].Run)}: {warning}");
                }
            }
            foreach (var run in AllRuns().Where(r => _campaign.FindNode(r) == null))
            {
                warnings.Add($"{CampaignOps.DisplayNameOf(run)} is not on the map (Add Run places it).");
            }
            return warnings;
        }

        private static PartyRosterSO FindRoster()
        {
            var guid = AssetDatabase.FindAssets("t:PartyRosterSO").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<PartyRosterSO>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }
    }

    /// <summary>One-button inspector so the campaign asset opens straight into the map editor.</summary>
    [CustomEditor(typeof(CampaignSO))]
    public class CampaignSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Campaign Map Editor"))
            {
                CampaignMapEditorWindow.Open();
                EditorWindow.GetWindow<CampaignMapEditorWindow>("Campaign Map Editor").SetTarget((CampaignSO)target);
            }
            EditorGUILayout.Space(4f);
            DrawDefaultInspector();
        }
    }
}
