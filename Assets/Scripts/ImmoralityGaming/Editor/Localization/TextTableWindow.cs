using System;
using System.Collections.Generic;
using System.Linq;
using ImmoralityGaming.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Editor.Localization
{
    /// <summary>
    /// <c>Tools ▸ Texts</c>: every player-facing text in one grid (<see cref="TextTableSO"/>), a column
    /// per language, edited in place.
    ///
    /// <para>Search matches keys and wording; the area dropdown narrows to a key prefix
    /// (<c>ui.hub</c>, <c>hero</c>); the filter shows the texts not yet reviewed, the ones missing a
    /// language, the keys nothing uses, and the keys code uses that the table lacks (type their
    /// wording to add them). Selecting a row lists where its key is used; click one to open it.</para>
    ///
    /// <para>Every edit goes through <c>Undo.RecordObject</c>; the table is saved when the window
    /// closes, loses focus, or on Save.</para>
    /// </summary>
    public class TextTableWindow : EditorWindow
    {
        private const string TablePath = "Assets/Resources/" + TextTableSO.ResourcePath + ".asset";
        private const string AllAreas = "All areas";

        private enum Filter
        {
            All,
            NotReviewed,
            Untranslated,
            Unused,
            MissingFromTable,
        }

        /// <summary>A grid row: an entry in the table, or a key code uses that the table lacks.</summary>
        private class Row
        {
            public TextEntry Entry;
            public string MissingKey;

            public string Key => Entry != null ? Entry.Key : MissingKey;
        }

        private TextTableSO _table;
        private readonly List<Row> _rows = new List<Row>();
        private Dictionary<string, List<LocUsage>> _usages;

        private string _search = "";
        private string _area = AllAreas;
        private Filter _filter = Filter.All;

        private MultiColumnListView _list;
        private DropdownField _areaField;
        private Label _countLabel;
        private VisualElement _detail;

        [MenuItem("Tools/Texts")]
        public static void Open()
        {
            var window = GetWindow<TextTableWindow>();
            window.titleContent = new GUIContent("Texts");
            window.minSize = new Vector2(700, 400);
        }

        private void OnEnable()
        {
            _table = AssetDatabase.LoadAssetAtPath<TextTableSO>(TablePath);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Save();
        }

        private void OnLostFocus()
        {
            Save();
        }

        private void OnUndoRedo()
        {
            if (_table == null)
            {
                return;
            }
            _table.RebuildIndex();
            Refresh();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            if (_table == null)
            {
                rootVisualElement.Add(new HelpBox($"No text table at {TablePath}.", HelpBoxMessageType.Info));
                rootVisualElement.Add(new Button(CreateTable) { text = "Create the text table" });
                return;
            }

            rootVisualElement.Add(BuildToolbar());

            _list = new MultiColumnListView
            {
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = _rows,
                style = { flexGrow = 1 },
            };
            _list.selectionChanged += _ => ShowDetail();
            rootVisualElement.Add(_list);

            _detail = new VisualElement
            {
                style =
                {
                    minHeight = 90,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4,
                    borderTopWidth = 1, borderTopColor = new Color(0f, 0f, 0f, 0.4f),
                },
            };
            rootVisualElement.Add(_detail);

            BuildColumns();
            Scan();
        }

        private void CreateTable()
        {
            _table = CreateInstance<TextTableSO>();
            AssetDatabase.CreateAsset(_table, TablePath);
            AssetDatabase.SaveAssets();
            CreateGUI();
        }

        // ---- toolbar ----------------------------------------------------------------------------

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();

            var search = new ToolbarSearchField { style = { flexGrow = 1, flexShrink = 1, minWidth = 150 } };
            search.RegisterValueChangedCallback(e =>
            {
                _search = e.newValue ?? "";
                Refresh();
            });
            toolbar.Add(search);

            _areaField = new DropdownField(new List<string> { AllAreas }, 0) { style = { width = 140 } };
            _areaField.RegisterValueChangedCallback(e =>
            {
                _area = e.newValue;
                Refresh();
            });
            toolbar.Add(_areaField);

            var filter = new EnumField(Filter.All) { style = { width = 130 } };
            filter.RegisterValueChangedCallback(e =>
            {
                _filter = (Filter)e.newValue;
                Refresh();
            });
            toolbar.Add(filter);

            toolbar.Add(new ToolbarButton(AddEntry) { text = "Add key" });
            toolbar.Add(new ToolbarButton(AddLanguage) { text = "Add language" });
            toolbar.Add(new ToolbarButton(Scan) { text = "Scan usages" });
            toolbar.Add(new ToolbarButton(Save) { text = "Save" });

            _countLabel = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, minWidth = 90 } };
            toolbar.Add(_countLabel);
            return toolbar;
        }

        // ---- grid -------------------------------------------------------------------------------

        private void BuildColumns()
        {
            _list.columns.Clear();

            _list.columns.Add(new Column
            {
                name = "key",
                title = "Key",
                width = 220,
                makeCell = () => MakeTextCell(false, OnKeyEdited),
                bindCell = (cell, i) => BindTextCell(cell, i, _rows[i].Key),
            });

            for (int lang = 0; lang < _table.Languages.Count; lang++)
            {
                int column = lang;
                _list.columns.Add(new Column
                {
                    name = "lang-" + column,
                    title = _table.Languages[column] + (column == 0 ? " (default)" : ""),
                    width = 320,
                    stretchable = true,
                    makeCell = () => MakeTextCell(true, (row, value) => OnValueEdited(row, column, value)),
                    bindCell = (cell, i) => BindTextCell(cell, i, _rows[i].Entry?.ValueAt(column) ?? ""),
                });
            }

            _list.columns.Add(new Column
            {
                name = "reviewed",
                title = "Reviewed",
                width = 70,
                makeCell = () =>
                {
                    var toggle = new Toggle { style = { alignSelf = Align.Center } };
                    toggle.RegisterValueChangedCallback(e =>
                    {
                        if (toggle.userData is Row row && row.Entry != null)
                        {
                            Undo.RecordObject(_table, "Mark text reviewed");
                            row.Entry.Reviewed = e.newValue;
                            EditorUtility.SetDirty(_table);
                        }
                    });
                    return toggle;
                },
                bindCell = (cell, i) =>
                {
                    var toggle = (Toggle)cell;
                    toggle.userData = _rows[i];
                    toggle.SetEnabled(_rows[i].Entry != null);
                    toggle.SetValueWithoutNotify(_rows[i].Entry != null && _rows[i].Entry.Reviewed);
                },
            });
        }

        private TextField MakeTextCell(bool multiline, Action<Row, string> onEdited)
        {
            var field = new TextField { multiline = multiline, isDelayed = true };
            field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterValueChangedCallback(e =>
            {
                if (field.userData is Row row)
                {
                    onEdited(row, e.newValue);
                }
            });
            return field;
        }

        private void BindTextCell(VisualElement cell, int index, string value)
        {
            var field = (TextField)cell;
            field.userData = _rows[index];
            field.SetValueWithoutNotify(value);
        }

        private void OnKeyEdited(Row row, string newKey)
        {
            newKey = (newKey ?? "").Trim();
            if (row.Entry == null || newKey == row.Entry.Key)
            {
                return;
            }
            if (newKey.Length == 0 || _table.Find(newKey) != null)
            {
                Debug.LogWarning($"[Texts] '{newKey}' is empty or already in the table; the key stays '{row.Entry.Key}'.");
                _list.RefreshItems();
                return;
            }
            Undo.RecordObject(_table, "Rename text key");
            row.Entry.Key = newKey;
            _table.RebuildIndex();
            EditorUtility.SetDirty(_table);
            Refresh();
        }

        private void OnValueEdited(Row row, int language, string value)
        {
            Undo.RecordObject(_table, "Edit text");
            if (row.Entry == null)
            {
                // A key code uses but the table lacked: typing its wording adds it.
                row.Entry = new TextEntry { Key = row.MissingKey };
                row.MissingKey = null;
                _table.Entries.Add(row.Entry);
                _table.RebuildIndex();
            }
            row.Entry.SetValue(language, value);
            EditorUtility.SetDirty(_table);
        }

        // ---- rows -------------------------------------------------------------------------------

        private void Refresh()
        {
            if (_list == null)
            {
                return;
            }
            RefreshAreas();
            _rows.Clear();

            if (_filter == Filter.MissingFromTable)
            {
                if (_usages != null)
                {
                    foreach (string key in _usages.Where(u => _table.Find(u.Key) == null && u.Value.Any(x => !x.Optional)).Select(u => u.Key).OrderBy(k => k))
                    {
                        if (Matches(key, null))
                        {
                            _rows.Add(new Row { MissingKey = key });
                        }
                    }
                }
            }
            else
            {
                foreach (var entry in _table.Entries.OrderBy(e => e.Key, StringComparer.Ordinal))
                {
                    if (Matches(entry.Key, entry) && PassesFilter(entry))
                    {
                        _rows.Add(new Row { Entry = entry });
                    }
                }
            }

            _list.RefreshItems();
            _countLabel.text = $"{_rows.Count} / {_table.Entries.Count}";
            ShowDetail();
        }

        private bool Matches(string key, TextEntry entry)
        {
            if (_area != AllAreas && !(key == _area || key.StartsWith(_area + ".", StringComparison.Ordinal)))
            {
                return false;
            }
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }
            if (key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return entry != null && entry.Values.Any(v => v != null && v.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool PassesFilter(TextEntry entry)
        {
            switch (_filter)
            {
                case Filter.NotReviewed:
                    return !entry.Reviewed;
                case Filter.Untranslated:
                    for (int i = 0; i < _table.Languages.Count; i++)
                    {
                        if (string.IsNullOrEmpty(entry.ValueAt(i)))
                        {
                            return true;
                        }
                    }
                    return false;
                case Filter.Unused:
                    return _usages != null && !_usages.ContainsKey(entry.Key);
                default:
                    return true;
            }
        }

        /// <summary>Areas are the first segment of every key: <c>ability</c>, <c>tutorial</c>, <c>ui</c>.
        /// Narrower than that is what the search box is for.</summary>
        private void RefreshAreas()
        {
            var areas = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in _table.Entries)
            {
                string key = entry.Key ?? "";
                int dot = key.IndexOf('.');
                if (dot > 0)
                {
                    areas.Add(key.Substring(0, dot));
                }
            }
            var choices = new List<string> { AllAreas };
            choices.AddRange(areas);
            _areaField.choices = choices;
            if (!choices.Contains(_area))
            {
                _area = AllAreas;
                _areaField.SetValueWithoutNotify(AllAreas);
            }
        }

        // ---- detail pane ------------------------------------------------------------------------

        private void ShowDetail()
        {
            _detail.Clear();
            if (!(_list.selectedItem is Row row))
            {
                _detail.Add(new Label("Select a text to see its note and where it is used."));
                return;
            }

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.Add(new Label(row.Key) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            if (row.Entry != null)
            {
                header.Add(new Button(() => DeleteEntry(row.Entry)) { text = "Delete" });
            }
            _detail.Add(header);

            if (row.Entry != null)
            {
                var note = new TextField("Note") { value = row.Entry.Note, isDelayed = true };
                note.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(_table, "Edit text note");
                    row.Entry.Note = e.newValue;
                    EditorUtility.SetDirty(_table);
                });
                _detail.Add(note);
            }

            if (_usages == null)
            {
                return;
            }
            if (!_usages.TryGetValue(row.Key, out var usages) || usages.Count == 0)
            {
                _detail.Add(new Label("Not used anywhere the scan can see (keys built at runtime need a provider)."));
                return;
            }
            var scroll = new ScrollView { style = { maxHeight = 120 } };
            foreach (var usage in usages)
            {
                var target = usage;
                string label = target.Line > 0 ? $"{target.Path}:{target.Line}" : target.Path;
                scroll.Add(new Button(() => OpenUsage(target)) { text = label, style = { unityTextAlign = TextAnchor.MiddleLeft } });
            }
            _detail.Add(scroll);
        }

        private static void OpenUsage(LocUsage usage)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(usage.Path);
            if (asset == null)
            {
                return;
            }
            if (usage.Line > 0)
            {
                AssetDatabase.OpenAsset(asset, usage.Line);
            }
            else
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        // ---- commands ---------------------------------------------------------------------------

        private void AddEntry()
        {
            string key = _area != AllAreas ? _area + ".new" : "new.key";
            string candidate = key;
            for (int n = 2; _table.Find(candidate) != null; n++)
            {
                candidate = key + n;
            }
            Undo.RecordObject(_table, "Add text");
            var entry = new TextEntry { Key = candidate };
            _table.Entries.Add(entry);
            _table.RebuildIndex();
            EditorUtility.SetDirty(_table);

            _filter = Filter.All;
            Refresh();
            int index = _rows.FindIndex(r => r.Entry == entry);
            if (index >= 0)
            {
                _list.SetSelection(index);
                _list.ScrollToItem(index);
            }
        }

        private void DeleteEntry(TextEntry entry)
        {
            if (!EditorUtility.DisplayDialog("Delete text", $"Delete '{entry.Key}'?", "Delete", "Cancel"))
            {
                return;
            }
            Undo.RecordObject(_table, "Delete text");
            _table.Entries.Remove(entry);
            _table.RebuildIndex();
            EditorUtility.SetDirty(_table);
            Refresh();
        }

        private void AddLanguage()
        {
            LanguagePrompt.Show(code =>
            {
                code = (code ?? "").Trim().ToLowerInvariant();
                if (code.Length == 0 || _table.Languages.Contains(code))
                {
                    return;
                }
                Undo.RecordObject(_table, "Add language");
                _table.Languages.Add(code);
                EditorUtility.SetDirty(_table);
                BuildColumns();
                Refresh();
            });
        }

        private void Scan()
        {
            _usages = LocUsageScanner.Index(LocUsageScanner.ScanAll(), _table);
            Refresh();
        }

        private void Save()
        {
            if (_table != null)
            {
                AssetDatabase.SaveAssetIfDirty(_table);
            }
        }

        /// <summary>A one-field popup asking for a language code.</summary>
        private class LanguagePrompt : EditorWindow
        {
            private Action<string> _done;

            public static void Show(Action<string> done)
            {
                var prompt = CreateInstance<LanguagePrompt>();
                prompt._done = done;
                prompt.titleContent = new GUIContent("Add language");
                prompt.ShowUtility();
                prompt.minSize = prompt.maxSize = new Vector2(260, 70);
            }

            private void CreateGUI()
            {
                var field = new TextField("Code (e.g. nl, de)");
                rootVisualElement.Add(field);
                rootVisualElement.Add(new Button(() =>
                {
                    _done?.Invoke(field.value);
                    Close();
                }) { text = "Add" });
                field.Focus();
            }
        }
    }
}
