using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Heroes.Editor
{
    /// <summary>
    /// Graph authoring for <see cref="SphereGridSO"/> assets: drag nodes into place, connect and
    /// disconnect edges, add/delete nodes, set the start node, edit payloads — on the very same
    /// <see cref="SphereGridView"/> the in-game hub screen renders, so what this window shows is
    /// exactly what the player sees.
    ///
    /// <para>Every structural mutation goes through <c>Undo.RecordObject</c> + <c>SetDirty</c>;
    /// payload edits ride SerializedObject-bound PropertyFields (which get undo for free, and let
    /// the existing <c>StatBlockDrawer</c> render a node's <c>Gains</c>). The "Preview" toggle runs
    /// <see cref="SphereGridPresenter.ClassifyAll"/> — the same classifier as the game — over a
    /// fresh hero with N banked XP, so affordability colouring can be checked while authoring.</para>
    /// </summary>
    public class SphereGridEditorWindow : EditorWindow
    {
        private const string ThemePath = "Assets/UI/Theme/CardDungeon.uss";

        private SphereGridSO _grid;
        private SerializedObject _serializedGrid;

        private SphereGridView _view;
        private ObjectField _gridField;
        private ObjectField _heroField;
        private ToolbarToggle _addToggle;
        private ToolbarToggle _connectToggle;
        private IntegerField _previewXpField;
        private ToolbarToggle _previewToggle;
        private Label _modeHint;
        private ScrollView _inspector;

        private string _selectedKey;
        private string _connectSourceKey;

        // Drag state. The offset is from the node's origin to the click point, so the node never
        // snaps its origin to the cursor mid-drag.
        private string _dragKey;
        private Vector2 _dragOffset;
        private int _dragPointerId = -1;

        [MenuItem("Tools/Heroes/Sphere Grid Editor")]
        public static void Open()
        {
            var window = GetWindow<SphereGridEditorWindow>("Sphere Grid Editor");
            window.minSize = new Vector2(900f, 500f);
        }

        /// <summary>Programmatic entry point (the SphereGridSO inspector's Open button).</summary>
        public void SetTarget(SphereGridSO grid)
        {
            _grid = grid;
            _serializedGrid = grid != null ? new SerializedObject(grid) : null;
            _selectedKey = null;
            _connectSourceKey = null;
            if (_gridField != null)
            {
                _gridField.SetValueWithoutNotify(grid);
            }
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
            _serializedGrid?.Update();
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

            // --- toolbar -----------------------------------------------------
            var toolbar = new Toolbar();
            _gridField = new ObjectField("Grid") { objectType = typeof(SphereGridSO), allowSceneObjects = false };
            _gridField.style.minWidth = 260f;
            _gridField.RegisterValueChangedCallback(evt => SetTarget(evt.newValue as SphereGridSO));
            toolbar.Add(_gridField);

            _heroField = new ObjectField("Hero") { objectType = typeof(HeroSO), allowSceneObjects = false };
            _heroField.style.minWidth = 220f;
            _heroField.RegisterValueChangedCallback(evt =>
            {
                var hero = evt.newValue as HeroSO;
                if (hero != null && hero.SphereGrid != null)
                {
                    SetTarget(hero.SphereGrid);
                }
            });
            toolbar.Add(_heroField);

            _addToggle = new ToolbarToggle { text = "Add Node" };
            _addToggle.RegisterValueChangedCallback(_ => UpdateModeHint());
            toolbar.Add(_addToggle);

            _connectToggle = new ToolbarToggle { text = "Connect" };
            _connectToggle.RegisterValueChangedCallback(_ => { CancelConnect(); UpdateModeHint(); });
            toolbar.Add(_connectToggle);

            var deleteButton = new ToolbarButton(DeleteSelected) { text = "Delete Node" };
            toolbar.Add(deleteButton);

            var startButton = new ToolbarButton(SetStartToSelected) { text = "Set Start" };
            toolbar.Add(startButton);

            _previewXpField = new IntegerField("Preview XP") { value = 100 };
            _previewXpField.style.minWidth = 140f;
            _previewXpField.RegisterValueChangedCallback(_ => RefreshStates());
            toolbar.Add(_previewXpField);

            _previewToggle = new ToolbarToggle { text = "Preview states" };
            _previewToggle.RegisterValueChangedCallback(_ => RefreshStates());
            toolbar.Add(_previewToggle);

            _modeHint = new Label();
            _modeHint.style.unityTextAlign = TextAnchor.MiddleLeft;
            _modeHint.style.marginLeft = 8f;
            toolbar.Add(_modeHint);
            root.Add(toolbar);

            // --- body: graph | inspector ------------------------------------
            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };

            _view = new SphereGridView
            {
                // Left button is reserved for select/drag/connect/place; middle or right pans.
                PanButtons = (1 << 1) | (1 << 2)
            };
            _view.style.flexGrow = 1f;
            _view.NodeClicked += OnNodeClicked;
            _view.NodePointerDown += OnNodePointerDown;
            _view.BackgroundPointerDown += OnBackgroundPointerDown;
            _view.RegisterCallback<PointerMoveEvent>(OnViewPointerMove);
            _view.RegisterCallback<PointerUpEvent>(OnViewPointerUp);
            body.Add(_view);

            _inspector = new ScrollView(ScrollViewMode.Vertical);
            _inspector.style.width = 340f;
            _inspector.style.flexShrink = 0f;
            _inspector.style.paddingLeft = 8f;
            _inspector.style.paddingRight = 8f;
            body.Add(_inspector);

            root.Add(body);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.focusable = true;

            UpdateModeHint();
            if (_grid != null)
            {
                RebuildAll();
                _view.FrameAll();
            }
            else
            {
                RebuildInspector();
            }
        }

        private void UpdateModeHint()
        {
            if (_modeHint == null)
            {
                return;
            }
            if (_connectToggle != null && _connectToggle.value)
            {
                _modeHint.text = "Connect: click a source node, then a target. Esc cancels.";
            }
            else if (_addToggle != null && _addToggle.value)
            {
                _modeHint.text = "Add: click empty canvas to place a node.";
            }
            else
            {
                _modeHint.text = "Left-drag moves a node · middle/right-drag pans · scroll zooms.";
            }
        }

        // --- graph rebuilds ------------------------------------------------------

        /// <summary>Full rebuild: view model, states and the inspector pane.</summary>
        private void RebuildAll()
        {
            if (_view == null)
            {
                return;
            }

            var nodes = new List<SphereGridView.NodeInfo>();
            var edges = new List<(string A, string B)>();
            SphereGridPresenter.BuildViewModel(_grid, nodes, edges);
            _view.SetGraph(nodes, edges);

            if (_selectedKey != null && SphereGridOps.FindNode(_grid, _selectedKey) == null)
            {
                _selectedKey = null;
            }
            _view.SetSelected(_selectedKey);
            RefreshStates();
            RebuildInspector();
        }

        /// <summary>Recolours nodes without touching shape: preview mode runs the game's own
        /// classifier over a fresh hero with the preview bank; off = neutral authoring grey.</summary>
        private void RefreshStates()
        {
            if (_view == null || _grid == null || _grid.Nodes == null)
            {
                return;
            }

            bool preview = _previewToggle != null && _previewToggle.value;
            if (preview)
            {
                int bank = Mathf.Max(0, _previewXpField != null ? _previewXpField.value : 0);
                foreach (var pair in SphereGridPresenter.ClassifyAll(_grid, new List<string>(), bank))
                {
                    _view.SetNodeState(pair.Key, SphereGridPresenter.StateClass(pair.Value));
                }
            }
            else
            {
                foreach (var node in _grid.Nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.Key))
                    {
                        _view.SetNodeState(node.Key, "sg-node--adjacent");
                    }
                }
            }
        }

        // --- interactions -----------------------------------------------------------

        private void OnNodeClicked(string key)
        {
            if (_connectToggle != null && _connectToggle.value)
            {
                HandleConnectClick(key);
                return;
            }

            Select(key);
        }

        private void Select(string key)
        {
            _selectedKey = key;
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
                ToggleEdge(_connectSourceKey, key);
            }
            CancelConnect();
            RebuildAll();
        }

        private void CancelConnect()
        {
            if (_connectSourceKey != null)
            {
                _view.SetNodeFlag(_connectSourceKey, "sg-node--source", false);
            }
            _connectSourceKey = null;
            _view?.SetGhostEdge(null, null);
        }

        /// <summary>Adds the edge, or removes it when it already exists (checked both orientations).</summary>
        private void ToggleEdge(string a, string b)
        {
            var nodeA = SphereGridOps.FindNode(_grid, a);
            var nodeB = SphereGridOps.FindNode(_grid, b);
            if (nodeA == null || nodeB == null)
            {
                return;
            }

            Undo.RecordObject(_grid, "Toggle Sphere Grid Edge");
            bool existed = (nodeA.Neighbors != null && nodeA.Neighbors.Remove(b))
                         | (nodeB.Neighbors != null && nodeB.Neighbors.Remove(a));
            if (!existed)
            {
                if (nodeA.Neighbors == null)
                {
                    nodeA.Neighbors = new List<string>();
                }
                nodeA.Neighbors.Add(b);
            }
            EditorUtility.SetDirty(_grid);
            _serializedGrid?.Update();
        }

        private void OnNodePointerDown(string key, PointerDownEvent evt)
        {
            if (evt.button != 0 || _grid == null)
            {
                return;
            }

            if (_connectToggle != null && _connectToggle.value)
            {
                return; // the click handler owns connect mode
            }

            var node = SphereGridOps.FindNode(_grid, key);
            if (node == null)
            {
                return;
            }

            Select(key);

            // Offset anchoring: remember where inside the node the grab happened, so dragging
            // never snaps the node's origin to the cursor (same fix as the manual layout editor).
            _dragKey = key;
            _dragOffset = node.Position - _view.ToGridSpace(evt.position);
            _dragPointerId = evt.pointerId;
            Undo.RecordObject(_grid, "Move Sphere Grid Node");
            _view.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnViewPointerMove(PointerMoveEvent evt)
        {
            if (_dragKey != null && evt.pointerId == _dragPointerId)
            {
                var node = SphereGridOps.FindNode(_grid, _dragKey);
                if (node != null)
                {
                    node.Position = _view.ToGridSpace(evt.position) + _dragOffset;
                    _view.SetNodePosition(_dragKey, node.Position);
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
                _dragKey = null;
                _dragPointerId = -1;
                EditorUtility.SetDirty(_grid);
                _serializedGrid?.Update();
                RebuildInspector();
            }
        }

        private void OnBackgroundPointerDown(Vector2 gridPosition, PointerDownEvent evt)
        {
            if (evt.button != 0 || _grid == null)
            {
                return;
            }

            if (_addToggle != null && _addToggle.value)
            {
                AddNodeAt(gridPosition);
                evt.StopPropagation();
                return;
            }

            Select(null);
        }

        private void AddNodeAt(Vector2 gridPosition)
        {
            Undo.RecordObject(_grid, "Add Sphere Grid Node");
            if (_grid.Nodes == null)
            {
                _grid.Nodes = new List<SphereGridNode>();
            }

            var node = new SphereGridNode
            {
                Key = NextKey(),
                Position = gridPosition,
                XpCost = 25
            };
            _grid.Nodes.Add(node);
            if (string.IsNullOrEmpty(_grid.StartNodeKey))
            {
                _grid.StartNodeKey = node.Key;
            }
            EditorUtility.SetDirty(_grid);
            _serializedGrid?.Update();
            _selectedKey = node.Key;
            RebuildAll();
        }

        private string NextKey()
        {
            for (int i = 1; ; i++)
            {
                string candidate = "node-" + i;
                if (SphereGridOps.FindNode(_grid, candidate) == null)
                {
                    return candidate;
                }
            }
        }

        private void DeleteSelected()
        {
            var node = SphereGridOps.FindNode(_grid, _selectedKey);
            if (node == null)
            {
                return;
            }

            Undo.RecordObject(_grid, "Delete Sphere Grid Node");
            _grid.Nodes.Remove(node);
            foreach (var other in _grid.Nodes)
            {
                other?.Neighbors?.RemoveAll(k => k == node.Key);
            }
            if (_grid.StartNodeKey == node.Key)
            {
                _grid.StartNodeKey = _grid.Nodes.Count > 0 && _grid.Nodes[0] != null
                    ? _grid.Nodes[0].Key
                    : "";
            }
            EditorUtility.SetDirty(_grid);
            _serializedGrid?.Update();
            _selectedKey = null;
            RebuildAll();
        }

        private void SetStartToSelected()
        {
            if (_grid == null || SphereGridOps.FindNode(_grid, _selectedKey) == null)
            {
                return;
            }

            Undo.RecordObject(_grid, "Set Sphere Grid Start Node");
            _grid.StartNodeKey = _selectedKey;
            EditorUtility.SetDirty(_grid);
            _serializedGrid?.Update();
            RebuildAll();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CancelConnect();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete)
            {
                DeleteSelected();
                evt.StopPropagation();
            }
        }

        // --- inspector pane ----------------------------------------------------------

        private void RebuildInspector()
        {
            if (_inspector == null)
            {
                return;
            }
            _inspector.Clear();

            if (_grid == null)
            {
                _inspector.Add(new HelpBox("Pick a SphereGridSO (or a hero with one assigned).", HelpBoxMessageType.Info));
                return;
            }

            int index = IndexOfSelected();
            if (index < 0)
            {
                _inspector.Add(new HelpBox(
                    $"{_grid.name}: {(_grid.Nodes != null ? _grid.Nodes.Count : 0)} node(s), "
                    + $"{SphereGridOps.TotalGridCost(_grid)} XP to complete.\n\nSelect a node to edit it.",
                    HelpBoxMessageType.Info));
                return;
            }

            _serializedGrid ??= new SerializedObject(_grid);
            _serializedGrid.Update();
            var element = _serializedGrid.FindProperty("Nodes").GetArrayElementAtIndex(index);

            var keyLabel = new Label($"Key: {_selectedKey}");
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.marginTop = 6f;
            _inspector.Add(keyLabel);
            _inspector.Add(new HelpBox(
                "Keys are save data — write-once. Renaming a key orphans every save that bought it.",
                HelpBoxMessageType.Warning));

            AddField(element, "DisplayName");
            AddField(element, "Kind");
            AddField(element, "XpCost");
            AddField(element, "Position");
            AddField(element, "Gains");
            AddField(element, "ResistType");
            AddField(element, "ResistPercent");

            // Edge list with per-edge disconnect, the reliable disconnect UI (painted lines are
            // not hit-testable) — mirrors the manual layout editor's door list.
            var node = SphereGridOps.FindNode(_grid, _selectedKey);
            var edgesLabel = new Label("Edges");
            edgesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            edgesLabel.style.marginTop = 8f;
            _inspector.Add(edgesLabel);

            var adjacency = SphereGridOps.BuildAdjacency(_grid);
            if (node != null && adjacency.TryGetValue(node.Key, out var neighbors) && neighbors.Count > 0)
            {
                foreach (var neighbor in neighbors)
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    row.Add(new Label(neighbor) { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleLeft } });
                    var captured = neighbor;
                    var remove = new Button(() =>
                    {
                        ToggleEdge(node.Key, captured);
                        RebuildAll();
                    })
                    { text = "X" };
                    remove.style.width = 24f;
                    row.Add(remove);
                    _inspector.Add(row);
                }
            }
            else
            {
                _inspector.Add(new Label("(none — use Connect mode)"));
            }

            _inspector.Bind(_serializedGrid);
            // Payload edits (position, kind, gains) should reflect in the graph without reselecting.
            _inspector.TrackSerializedObjectValue(_serializedGrid, _ => OnInspectorEdited());
        }

        private void OnInspectorEdited()
        {
            var node = SphereGridOps.FindNode(_grid, _selectedKey);
            if (node != null && _view != null)
            {
                _view.SetNodePosition(node.Key, node.Position);
            }
            RefreshStates();
        }

        private void AddField(SerializedProperty element, string relativeName)
        {
            var property = element.FindPropertyRelative(relativeName);
            if (property != null)
            {
                _inspector.Add(new PropertyField(property));
            }
        }

        private int IndexOfSelected()
        {
            if (_grid == null || _grid.Nodes == null || string.IsNullOrEmpty(_selectedKey))
            {
                return -1;
            }
            for (int i = 0; i < _grid.Nodes.Count; i++)
            {
                if (_grid.Nodes[i] != null && _grid.Nodes[i].Key == _selectedKey)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    /// <summary>One-button inspector so a grid asset opens straight into the graph editor.</summary>
    [CustomEditor(typeof(SphereGridSO))]
    public class SphereGridSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Sphere Grid Editor"))
            {
                SphereGridEditorWindow.Open();
                GetWindow().SetTarget((SphereGridSO)target);
            }
            EditorGUILayout.Space(4f);
            DrawDefaultInspector();
        }

        private static SphereGridEditorWindow GetWindow()
        {
            return EditorWindow.GetWindow<SphereGridEditorWindow>("Sphere Grid Editor");
        }
    }
}
