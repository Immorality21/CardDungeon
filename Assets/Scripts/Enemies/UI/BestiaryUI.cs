using System;
using System.Collections.Generic;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Enemies.UI
{
    /// <summary>
    /// Hub "Bestiary" (UI Toolkit view-controller, not a MonoBehaviour - same shape as
    /// <c>MagicForgeUI</c> and <c>MerchantUI</c>, constructed by <c>MainMenuManager</c> from the
    /// <c>bestiary-view</c> subtree).
    ///
    /// <para>It is the permanent home of everything the in-combat Inspect page shows, plus the
    /// things only a collection screen can: how much of the roster has been met, and how many of
    /// each the party has killed. Every enemy in <see cref="EnemyCatalogSO"/> is listed even when
    /// unmet - a collection with invisible gaps is not a collection - but an unmet row shows no name
    /// and no icon.</para>
    ///
    /// <para>All wording and colour come from <see cref="BestiaryPresenter"/> and
    /// <see cref="BestiaryLineView"/>, which the combat page uses too, so the two can never
    /// disagree about what a 120% fire resistance is called.</para>
    /// </summary>
    public class BestiaryUI
    {
        private const string UnknownName = "? ? ?";

        private readonly VisualElement _root;
        private readonly Label _progress;
        private readonly ScrollView _list;
        private readonly ScrollView _detail;
        private readonly Button _closeButton;
        private readonly VisualElement _portrait;
        private readonly Label _name;
        private readonly Label _sub;

        // Only the met enemies are rows the cursor walks; _rowEntries maps a row to its catalog index.
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<int> _rowEntries = new List<int>();
        private List<EnemySO> _catalog = new List<EnemySO>();
        private int _selected = -1;
        private bool _isShown;

        public event Action OnClosed;

        public BestiaryUI(VisualElement root)
        {
            _root = root;
            _progress = root.Q<Label>("bestiary-progress");
            _list = root.Q<ScrollView>("bestiary-list");
            _detail = root.Q<ScrollView>("bestiary-detail");
            _closeButton = root.Q<Button>("bestiary-close");
            _portrait = root.Q<VisualElement>("bestiary-portrait");
            _name = root.Q<Label>("bestiary-name");
            _sub = root.Q<Label>("bestiary-sub");

            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
                _closeButton.focusable = false;
            }
            if (_list != null)
            {
                _list.focusable = false;
            }

            // Arrow keys walk the list, like every other screen. The cursor is this screen's own
            // rather than the shared KeyboardNavigator's because the rows are not Buttons and because
            // moving the cursor here has to re-render the detail column beside it, not just highlight.
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationSubmitEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            _catalog = LoadCatalog();
            _selected = -1;
            RefreshList();
            // Open on the first enemy met rather than a "Select an enemy." page beside a live list.
            if (_rows.Count > 0)
            {
                Select(0);
            }
            else
            {
                ShowEmptyDetail();
            }

            _root.focusable = true;
            if (_root.panel != null)
            {
                _root.Focus();
            }
        }

        public void Hide()
        {
            _isShown = false;
            _root.focusable = false; // stop being a focus/nav target once closed
            _root.style.display = DisplayStyle.None;
            _list?.Clear();
            _detail?.Clear();
            _rows.Clear();
            _rowEntries.Clear();
            OnClosed?.Invoke();
        }

        // ============================================================
        //  KEYBOARD
        // ============================================================

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isShown)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    Hide();
                    evt.StopPropagation();
                    break;
            }
        }

        /// <summary>
        /// Moves the cursor and reads the entry in one step - there is nothing to confirm on this
        /// screen, so making Enter a second, separate press would only add a key that does nothing.
        /// </summary>
        private void MoveSelection(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            int index = _selected < 0
                ? (delta > 0 ? 0 : _rows.Count - 1)
                : (_selected + delta + _rows.Count) % _rows.Count;

            Select(index);
            _list?.ScrollTo(_rows[index]);
        }

        /// <summary>
        /// The catalog from Resources. Missing asset degrades to an empty screen with a line saying
        /// so, rather than a null reference on a hub screen the player can always open.
        /// </summary>
        private static List<EnemySO> LoadCatalog()
        {
            var catalog = EnemyCatalogSO.Load();
            if (catalog == null || catalog.Enemies == null)
            {
                Debug.LogWarning(
                    "EnemyCatalog not found at Resources/EnemyCatalog. The bestiary will be empty.");
                return new List<EnemySO>();
            }

            var result = new List<EnemySO>(catalog.Enemies.Count);
            foreach (var definition in catalog.Enemies)
            {
                if (definition != null)
                {
                    result.Add(definition);
                }
            }
            return result;
        }

        // ============================================================
        //  LIST
        // ============================================================

        /// <summary>
        /// Met enemies as full rows the cursor walks, then every enemy not yet met as a compact
        /// "? ? ?" row at the bottom - the collection still shows its gaps (a collection with invisible
        /// gaps is not a collection) without the unknowns being four fifths of the list.
        /// </summary>
        private void RefreshList()
        {
            _list.Clear();
            _rows.Clear();
            _rowEntries.Clear();

            var knowledge = MetaProgressManager.Instance.GetBestiary();
            int seenCount = BestiaryPresenter.SeenCount(_catalog, knowledge);
            _progress.text = _catalog.Count == 0
                ? "No enemy catalog found."
                : $"{seenCount} of {_catalog.Count} discovered";
            // Read: the town stops flagging the Bestiary until someone new is met.
            MetaProgressManager.Instance.MarkBestiaryViewed(seenCount);

            int unknown = 0;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var definition = _catalog[i];
                var known = BestiaryOps.Find(knowledge, definition.SaveKey);
                if (known == null)
                {
                    unknown++;
                    continue;
                }

                int rowIndex = _rows.Count;
                var row = MakeRow(definition.Sprite, definition.Label,
                    known.Kills > 0 ? $"Slain x{known.Kills}" : "Seen, not yet slain");
                row.RegisterCallback<ClickEvent>(_ => Select(rowIndex));
                _list.Add(row);
                _rows.Add(row);
                _rowEntries.Add(i);
            }

            for (int i = 0; i < unknown; i++)
            {
                var row = MakeRow(null, UnknownName, null);
                row.AddToClassList("cd-inv-row--static");
                row.AddToClassList("cd-bestiary-row--unknown");
                _list.Add(row);
            }

            RenderSelection();
        }

        private static VisualElement MakeRow(Sprite sprite, string name, string caption)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-inv-row");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("cd-inv-row__icon");
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("cd-inv-row__name");
            text.Add(nameLabel);
            if (!string.IsNullOrEmpty(caption))
            {
                var captionLabel = new Label(caption) { pickingMode = PickingMode.Ignore };
                captionLabel.AddToClassList("cd-inv-row__caption");
                text.Add(captionLabel);
            }
            row.Add(text);
            return row;
        }

        private void Select(int rowIndex)
        {
            _selected = rowIndex;
            RenderSelection();
            ShowDetail(_catalog[_rowEntries[rowIndex]]);
        }

        private void RenderSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].EnableInClassList("cd-inv-row--selected", i == _selected);
            }
        }

        // ============================================================
        //  DETAIL
        // ============================================================

        private void ShowEmptyDetail()
        {
            _detail.Clear();
            SetHead(null, "No enemies met yet", "Everything the party survives is written down here.");
        }

        private void SetHead(Sprite portrait, string name, string sub)
        {
            if (_portrait != null)
            {
                _portrait.style.backgroundImage = portrait != null ? new StyleBackground(portrait) : StyleKeyword.None;
            }
            if (_name != null)
            {
                _name.text = name;
            }
            if (_sub != null)
            {
                _sub.text = sub;
            }
        }

        /// <summary>
        /// One enemy's page: portrait, name and health on top, then two columns - what hurts it
        /// (resistances, immunities) and what it is (stats, abilities) - with kills and drops under
        /// them, so a normal entry fits without scrolling.
        /// </summary>
        private void ShowDetail(EnemySO definition)
        {
            _detail.Clear();
            _detail.scrollOffset = Vector2.zero;
            var known = MetaProgressManager.Instance.GetBestiaryEntry(definition.SaveKey);
            if (known == null)
            {
                SetHead(null, UnknownName, "Not yet encountered.");
                return;
            }

            // Health comes from the definition's base stats here, not from a live unit: the hub has
            // no fight in progress, and a level's enemy tuning scales this per floor anyway.
            SetHead(definition.Sprite, definition.Label,
                $"Health {definition.BaseStats[UnitStats.StatType.MaxHealth]}  ·  Slain x{known.Kills}");

            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.AttackLine(definition, known)));

            var page = new VisualElement();
            page.AddToClassList("cd-bestiary__page");
            var left = new VisualElement();
            left.AddToClassList("cd-bestiary__page-col");
            var right = new VisualElement();
            right.AddToClassList("cd-bestiary__page-col");
            page.Add(left);
            page.Add(right);
            _detail.Add(page);

            BestiaryLineView.AddSection(left, "Resistances", BestiaryPresenter.ResistanceLines(definition, known));
            BestiaryLineView.AddSection(left, "Immune to", BestiaryPresenter.ImmunityLines(definition, known));
            BestiaryLineView.AddSection(right, "Base stats", BestiaryPresenter.StatLines(definition, known));
            BestiaryLineView.AddSection(right, "Abilities", BestiaryPresenter.CompactSpellLines(definition, known));

            // One row, the same as the combat Inspect page: the names seen, then how many are left.
            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.LootSummary(definition, known)));
        }
    }
}
